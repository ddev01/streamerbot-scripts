#!/usr/bin/env python3
"""
Merge Streamerbot Twitch watchtime export (seconds) with TAWMAE points export.
Output: CSV with username, points, watchtime in minutes.
"""

from __future__ import annotations

import argparse
import csv
from pathlib import Path


def load_watchtime_seconds(path: Path) -> dict[str, int]:
    by_login: dict[str, int] = {}
    with path.open(encoding="utf-8-sig", newline="") as f:
        reader = csv.DictReader(f, delimiter="\t")
        for row in reader:
            login = (row.get("User Login") or "").strip().lower()
            if not login:
                continue
            raw = (row.get("Watchtime Value") or "").strip()
            if not raw:
                continue
            value = int(float(raw))
            if login in by_login and by_login[login] != value:
                raise ValueError(f"Duplicate User Login in watchtime file: {login!r}")
            by_login[login] = value
    return by_login


def load_points(path: Path) -> dict[str, int]:
    by_login: dict[str, int] = {}
    with path.open(encoding="utf-8-sig", newline="") as f:
        reader = csv.DictReader(f, delimiter="\t")
        for row in reader:
            login = (row.get("User Login") or "").strip().lower()
            if not login:
                continue
            raw = (row.get("Points Value") or "").strip()
            if not raw:
                continue
            value = int(float(raw))
            if login in by_login and by_login[login] != value:
                raise ValueError(f"Duplicate User Login in points file: {login!r}")
            by_login[login] = value
    return by_login


def main() -> None:
    base = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "-w",
        "--watchtime",
        type=Path,
        default=base / "sbwt.txt",
        help="Watchtime export (tab-separated, Watchtime Value in seconds)",
    )
    parser.add_argument(
        "-p",
        "--points",
        type=Path,
        default=base / "tawmae_TWITCH_POINTS_Export_20260510_213525.txt",
        help="Points export (tab-separated)",
    )
    parser.add_argument(
        "-o",
        "--output",
        type=Path,
        default=base / "combined_points_watchtime.csv",
        help="Output CSV path",
    )
    args = parser.parse_args()

    wt = load_watchtime_seconds(args.watchtime)
    pts = load_points(args.points)

    logins = sorted(wt.keys() | pts.keys())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(["username", "points", "watchtime(minutes)"])
        for login in logins:
            points_val = pts.get(login, 0)
            seconds = wt.get(login, 0)
            minutes = int(round(seconds / 60.0))
            writer.writerow([login, points_val, minutes])

    print(f"Wrote {len(logins)} rows to {args.output}")
    print(f"  watchtime rows: {len(wt)}, points rows: {len(pts)}")


if __name__ == "__main__":
    main()
