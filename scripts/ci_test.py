"""Menjalankan satu eksperimen beserta API lokal di Linux/macOS/Windows."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import time
import urllib.error
import urllib.request


def verify(summary, actual_exit, expected_failures):
    if not summary["executionValid"]:
        raise RuntimeError("Run invalid: sampel kurang atau jenis error tidak sesuai latihan")
    failed = sorted(key for key, passed in summary["checks"].items() if not passed)
    if failed != sorted(expected_failures):
        raise RuntimeError(f"Gagal pada {failed}; seharusnya {expected_failures}")
    expected_exit = 1 if expected_failures else 0
    if actual_exit != expected_exit or summary["sloPassed"] != (expected_exit == 0):
        raise RuntimeError(f"Exit/status SLO tidak sesuai: exit={actual_exit}")
    # Negative control checkout harus mendekati injeksi 10%, bukan gagal total.
    if summary["selected"] == "error-rate" and expected_failures:
        if not 5 <= summary["errorRate"] <= 15:
            raise RuntimeError("Error rate di luar rentang demonstrasi 5..15%")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("case", choices=["load", "stress", "throughput", "latency", "error-rate"])
    parser.add_argument("--value")
    parser.add_argument("--expect-fail", default="", help="Nama check, dipisahkan koma")
    options = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    os.chdir(root)
    log_dir = root / "logs"
    log_dir.mkdir(exist_ok=True)
    existing = set((root / "reports").glob("*/summary.json"))
    command = ["dotnet", "PerformanceTests/bin/Release/net10.0/PerformanceTests.dll", options.case]
    if options.value is not None:
        command.append(options.value)
    expected = [item for item in options.expect_fail.split(",") if item]

    # Tolak port yang sudah dipakai agar tidak menguji server yang keliru.
    import socket
    with socket.socket() as probe:
        try:
            probe.bind(("127.0.0.1", 5080))
        except OSError as error:
            raise RuntimeError("Port 5080 sudah dipakai; hentikan API sebelumnya") from error

    with (log_dir / "api.log").open("w", encoding="utf-8") as api_log:
        api = subprocess.Popen(
            ["dotnet", "DemoApi/bin/Release/net10.0/DemoApi.dll", "--urls", "http://127.0.0.1:5080"],
            stdout=api_log, stderr=subprocess.STDOUT,
        )
        try:
            # Tidak memakai proxy untuk health check localhost.
            opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
            for _ in range(60):
                if api.poll() is not None:
                    raise RuntimeError("API berhenti; baca logs/api.log")
                try:
                    with opener.open("http://127.0.0.1:5080/health", timeout=1) as response:
                        if response.status == 200:
                            break
                except (urllib.error.URLError, TimeoutError):
                    pass
                time.sleep(0.5)
            else:
                raise RuntimeError("API belum siap setelah sekitar 30 detik")

            with (log_dir / "nbomber.log").open("w", encoding="utf-8") as test_log:
                result = subprocess.run(command, stdout=test_log, stderr=subprocess.STDOUT, timeout=300)
            print((log_dir / "nbomber.log").read_text(encoding="utf-8", errors="replace"))
            fresh = set((root / "reports").glob("*/summary.json")) - existing
            if len(fresh) != 1:
                raise RuntimeError("Runner tidak menghasilkan tepat satu summary baru")
            summary = json.loads(fresh.pop().read_text(encoding="utf-8"))
            if summary["selected"] != options.case:
                raise RuntimeError("Summary berasal dari kasus yang berbeda")
            verify(summary, result.returncode, expected)
            message = "SLO FAIL sesuai rancangan latihan" if expected else "SLO PASS"
            print(f"CI PASS: {message}")
            if os.environ.get("GITHUB_STEP_SUMMARY"):
                with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as output:
                    output.write(
                        f"### {options.case}: {message}\n\n"
                        f"| OK RPS | p95 (ms) | p99 (ms) | Error (%) |\n|---|---|---|---|\n"
                        f"| {summary['successfulRps']:.2f} | {summary['p95']:.2f} | "
                        f"{summary['p99']:.2f} | {summary['errorRate']:.2f} |\n"
                    )
        finally:
            if api.poll() is None:
                api.terminate()
                try:
                    api.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    api.kill()
                    api.wait(timeout=10)


if __name__ == "__main__":
    main()
