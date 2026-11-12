"""Kontrol bermakna: verifier tidak boleh menghijaukan kegagalan yang salah."""
import unittest
from ci_test import verify


class VerifyResultTests(unittest.TestCase):
    def summary(self, failed=(), selected="latency", error=0, valid=True):
        checks = {name: name not in failed for name in ("error_rate", "p95", "p99")}
        return {"executionValid": valid, "checks": checks, "sloPassed": not failed,
                "selected": selected, "errorRate": error}

    def test_accepts_healthy_service(self):
        verify(self.summary(), 0, [])

    def test_accepts_expected_tail_failure(self):
        verify(self.summary(["p99"]), 1, ["p99"])

    def test_rejects_invalid_run_even_with_expected_failure(self):
        with self.assertRaises(RuntimeError):
            verify(self.summary(["p99"], valid=False), 1, ["p99"])

    def test_rejects_missing_negative_control(self):
        with self.assertRaises(RuntimeError):
            verify(self.summary(), 0, ["p99"])

    def test_rejects_unexpected_additional_failure(self):
        with self.assertRaises(RuntimeError):
            verify(self.summary(["p95", "p99"]), 1, ["p99"])

    def test_rejects_crash_exit_code(self):
        with self.assertRaises(RuntimeError):
            verify(self.summary(["p99"]), 2, ["p99"])

    def test_rejects_inconsistent_slo_status(self):
        summary = self.summary(["p99"])
        summary["sloPassed"] = True
        with self.assertRaises(RuntimeError):
            verify(summary, 1, ["p99"])

    def test_accepts_expected_checkout_error_rate(self):
        verify(self.summary(["error_rate"], selected="error-rate", error=10), 1, ["error_rate"])

    def test_rejects_checkout_total_failure(self):
        with self.assertRaises(RuntimeError):
            verify(self.summary(["error_rate"], selected="error-rate", error=100), 1, ["error_rate"])


if __name__ == "__main__":
    unittest.main()
