"""Pastikan blok kode lengkap README sama dengan file yang dijalankan CI."""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
readme = (root / "README.md").read_text(encoding="utf-8")
files = ["DemoApi/DemoApi.csproj", "DemoApi/Program.cs",
         "PerformanceTests/PerformanceTests.csproj", "PerformanceTests/Program.cs",
         ".github/workflows/nbomber.yml", "scripts/ci_test.py"]
for name in files:
    start = f"<!-- source:{name} -->\n"
    end = f"<!-- /source:{name} -->"
    block = readme.split(start, 1)[1].split(end, 1)[0]
    source = block.split("\n", 1)[1].rsplit("```", 1)[0]
    expected = (root / name).read_text(encoding="utf-8")
    if source != expected:
        raise SystemExit(f"Kode README tidak sama: {name}")
print("README: seluruh kode lengkap sama dengan file sumber.")
