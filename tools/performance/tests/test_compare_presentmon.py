import csv
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from compare_presentmon import metrics, percentile, read_capture, report


class PresentMonTest(unittest.TestCase):
    def test_percentiles(self):
        self.assertEqual(percentile([10, 20, 30], 50), 20)
        self.assertEqual(percentile([10, 20, 30], 100), 30)

    def test_fps_uses_mean_interval_not_mean_inverse(self):
        data = [10., 20.] * 100
        self.assertAlmostEqual(metrics(data)["fps"], 1000 / 15)
        self.assertAlmostEqual(metrics(data)["low_1_fps"], 50)

    def test_capture_filters_process(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "capture.csv"
            with path.open("w", newline="", encoding="utf-8") as file:
                writer = csv.writer(file)
                writer.writerow(["Application", "MsBetweenPresents"])
                writer.writerows([["game.exe", "16.7"]] * 120)
                writer.writerows([["other.exe", "33"]] * 120)
            self.assertEqual(len(read_capture(path, "game.exe")), 120)
            self.assertRaises(ValueError, read_capture, path, "missing.exe")

    def test_missing_metric_fails(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "bad.csv"
            path.write_text("Application,Unrelated\nfoo,1\n", encoding="utf-8")
            self.assertRaises(ValueError, read_capture, path)

    def test_small_sample_fails(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "tiny.csv"
            path.write_text("FrameTime\n16\n", encoding="utf-8")
            self.assertRaises(ValueError, read_capture, path)

    def test_report_is_cautious_with_one_run(self):
        data = [metrics([16.7] * 200)]
        self.assertIn("INSUFFICIENT REPETITIONS", report(data, data))


if __name__ == "__main__":
    unittest.main()
