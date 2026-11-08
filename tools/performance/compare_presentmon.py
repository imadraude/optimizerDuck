#!/usr/bin/env python3
"""Compare repeated PresentMon frame interval captures without vendor tweak folklore.

This measures frame presentation intervals, not end-to-end input or display latency.
"""
from __future__ import annotations

import argparse
import csv
import math
from pathlib import Path
import statistics

FRAME_COLUMNS = ("MsBetweenPresents", "FrameTime")


def percentile(values: list[float], percent: float) -> float:
    if not values:
        raise ValueError("No frame intervals")
    values = sorted(values)
    x = (len(values) - 1) * percent / 100
    i = int(x)
    if i + 1 >= len(values):
        return values[i]
    weight = x - i
    return values[i] * (1 - weight) + values[i + 1] * weight


def read_capture(path: Path, process: str | None = None) -> list[float]:
    with path.open(encoding="utf-8-sig", newline="") as stream:
        rows = csv.DictReader(stream)
        if not rows.fieldnames:
            raise ValueError(f"{path}: missing headers")
        column = next((key for key in FRAME_COLUMNS if key in rows.fieldnames), None)
        if column is None:
            raise ValueError(f"{path}: expected {FRAME_COLUMNS}, got {rows.fieldnames}")
        if process is not None and "Application" not in rows.fieldnames:
            raise ValueError(f"{path}: process filter requires Application column")

        frames: list[float] = []
        for row in rows:
            if process and (row.get("Application") or "").casefold() != process.casefold():
                continue
            try:
                interval = float(row[column])
            except (ValueError, TypeError):
                continue
            if math.isfinite(interval) and 0 < interval < 10_000:
                frames.append(interval)
        if len(frames) < 100:
            raise ValueError(f"{path}: only {len(frames)} valid frames, require >= 100")
        return frames


def metrics(frames: list[float]) -> dict[str, float]:
    count = len(frames)
    slowest = sorted(frames, reverse=True)[:max(1, math.ceil(count * .01))]
    return {
        "fps": 1000 / statistics.mean(frames),
        "low_1_fps": 1000 / statistics.mean(slowest),
        "p95_ms": percentile(frames, 95),
        "p99_ms": percentile(frames, 99),
        "over_50ms_pct": 100 * sum(frame > 50 for frame in frames) / count,
    }


def analyze(paths: list[Path], process: str | None) -> list[dict[str, float]]:
    return [metrics(read_capture(path, process)) for path in paths]


def report(before: list[dict[str, float]], after: list[dict[str, float]]) -> str:
    keys = {
        "fps": "Mean-rate FPS (higher is better)",
        "low_1_fps": "1% low FPS (higher is better)",
        "p95_ms": "P95 present interval (ms; lower is better)",
        "p99_ms": "P99 present interval (ms; lower is better)",
        "over_50ms_pct": "Intervals > 50 ms (%; lower is better)",
    }
    result = [
        "# PresentMon A/B comparison",
        "",
        f"Before runs: {len(before)}; after runs: {len(after)}.",
        "All values are medians of per-run statistics; do not pool frames across runs.",
        "",
        "| Metric | Before | After | Difference |",
        "|---|---:|---:|---:|",
    ]
    for key, label in keys.items():
        a = statistics.median(row[key] for row in before)
        b = statistics.median(row[key] for row in after)
        relative = 100 * (b - a) / a if a else float("nan")
        change = f"{relative:+.2f}%" if math.isfinite(relative) else "n/a"
        result.append(f"| {label} | {a:.2f} | {b:.2f} | {change} |")
    result.extend([
        "",
        "Positive change in FPS is desirable; positive change in frame time is a regression.",
        "PresentMon present intervals do not measure click-to-photon latency.",
        "Match scene, settings, power mode, frame limit and temperature; test one change at a time.",
        "Use three or more repetitions per configuration, interleaving conditions where possible.",
    ])
    if min(len(before), len(after)) < 3:
        result.append("**INSUFFICIENT REPETITIONS: do not claim an improvement.**")
    return "\n".join(result) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--before", nargs="+", type=Path, required=True)
    parser.add_argument("--after", nargs="+", type=Path, required=True)
    parser.add_argument("--process", help="Exact Application value in PresentMon CSV")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    text = report(analyze(args.before, args.process), analyze(args.after, args.process))
    if args.output:
        args.output.write_text(text, encoding="utf-8")
        print(f"Wrote {args.output}")
    else:
        print(text)


if __name__ == "__main__":
    main()
