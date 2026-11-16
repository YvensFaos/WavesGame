#!/usr/bin/env python3
"""
batch_export_csvs.py — convert a folder of Battleground logs into six
master CSVs (one per battle dimension), with battle_id keys on every row.

Usage:
    python batch_export_csvs.py logs/ --out analysis/data/
    python batch_export_csvs.py logs/ --out analysis/data/ --recursive
    python batch_export_csvs.py newlogs/ --out analysis/data/ --append
    python batch_export_csvs.py logs/ --out /tmp/test/ --limit 5
"""

import argparse
import json
import sys
from pathlib import Path

from log_to_csv import (
    extract_battle, write_csv,
    BATTLE_FIELDS, ACTOR_FIELDS, ACTION_FIELDS, BOARD_FIELDS,
    reasoning_fieldnames, turn_fieldnames,
)


def find_logs(folder, pattern, recursive):
    g = folder.rglob if recursive else folder.glob
    return sorted(p for p in g(pattern) if p.is_file())


def scan_factions(logfiles):
    """Read the first (INFO) line of each log; collect all faction names."""
    factions = set()
    for f in logfiles:
        try:
            with open(f, encoding="utf-8-sig") as fh:
                line = fh.readline().strip()
                if not line:
                    continue
                ev = json.loads(line)
                for a in ev.get("navalActorEntryJsons") or []:
                    if a.get("faction"):
                        factions.add(a["faction"])
        except Exception as e:
            print(f"[warn] faction scan {f}: {e}", file=sys.stderr)
    return sorted(factions)


def assign_battle_ids(logfiles, folder, id_mode):
    """Assign unique IDs; fail loudly on collision before parsing."""
    ids = {}
    for f in logfiles:
        if id_mode == "stem":
            bid = f.stem
        elif id_mode == "relative":
            rel = f.relative_to(folder).with_suffix("")
            bid = str(rel).replace("\\", "/")
        elif id_mode == "parent-stem":
            bid = f"{f.parent.name}/{f.stem}"
        else:
            raise ValueError(f"unknown --id-from: {id_mode}")
        ids[f] = bid

    by_id = {}
    for f, bid in ids.items():
        by_id.setdefault(bid, []).append(f)
    collisions = {k: v for k, v in by_id.items() if len(v) > 1}
    if collisions:
        lines = [f"battle_id collisions detected ({len(collisions)}):"]
        for bid, fs in list(collisions.items())[:5]:
            lines.append(f"  {bid!r}:")
            for f in fs[:3]:
                lines.append(f"    {f}")
        if len(collisions) > 5:
            lines.append(f"  ... and {len(collisions) - 5} more")
        lines.append("")
        lines.append("Fix: rename files, or pass --id-from=relative "
                     "or --id-from=parent-stem.")
        raise SystemExit("\n".join(lines))
    return ids


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("folder", help="Folder of raw .txt battle logs")
    ap.add_argument("--out", required=True, help="Output directory")
    ap.add_argument("--pattern", default="*.txt")
    ap.add_argument("--recursive", action="store_true")
    ap.add_argument("--id-from", default="stem",
                    choices=["stem", "relative", "parent-stem"])
    ap.add_argument("--append", action="store_true")
    ap.add_argument("--keep-non-llm-reasonings", action="store_true")
    ap.add_argument("--strict", action="store_true",
                    help="Fail on first parse error (default: skip and log)")
    ap.add_argument("--limit", type=int, default=None,
                    help="Process only the first N logs (for testing)")
    ap.add_argument("--quiet", action="store_true")
    args = ap.parse_args()

    folder = Path(args.folder)
    if not folder.is_dir():
        raise SystemExit(f"Not a directory: {folder}")
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)

    logs = find_logs(folder, args.pattern, args.recursive)
    if args.limit:
        logs = logs[:args.limit]
    if not logs:
        raise SystemExit(f"No files matching {args.pattern} in {folder}")

    print(f"Found {len(logs)} log files in {folder}")
    try:
        ids = assign_battle_ids(logs, folder, args.id_from)
    except SystemExit as e:
        print(str(e), file=sys.stderr)
        return 2
    print(f"  assigned {len(set(ids.values()))} unique battle_ids")

    print("Scanning factions (INFO prepass)...")
    factions = scan_factions(logs)
    print(f"  factions: {factions}")

    accum = {"battle": [], "actors": [], "turns": [],
             "actions": [], "reasonings": [], "board_snapshots": []}
    failed, skipped_non_llm = [], 0

    print(f"Parsing {len(logs)} logs...")
    for i, f in enumerate(logs, 1):
        if not args.quiet and (i == 1 or i % 50 == 0 or i == len(logs)):
            print(f"  [{i}/{len(logs)}] {f.name}")
        try:
            res = extract_battle(
                str(f), battle_id=ids[f], all_factions=factions,
                keep_non_llm_reasonings=args.keep_non_llm_reasonings)
            for k in accum:
                accum[k].extend(res[k])
            skipped_non_llm += res["meta"]["skipped_non_llm"]
        except Exception as e:
            failed.append((str(f), str(e)))
            print(f"[FAIL] {f}: {e}", file=sys.stderr)
            if args.strict:
                raise

    print(f"Writing to {out}...")
    files = [
        ("battles.csv", BATTLE_FIELDS, accum["battle"]),
        ("actors.csv", ACTOR_FIELDS, accum["actors"]),
        ("turns.csv", turn_fieldnames(factions), accum["turns"]),
        ("actions.csv", ACTION_FIELDS, accum["actions"]),
        ("reasonings.csv", reasoning_fieldnames(), accum["reasonings"]),
        ("board_snapshots.csv", BOARD_FIELDS, accum["board_snapshots"]),
    ]
    for fname, fields, rows in files:
        if not rows and not args.append:
            print(f"  {fname:<24} (0 rows — skipped)")
            continue
        write_csv(str(out / fname), fields, rows, append=args.append)
        print(f"  {fname:<24} ({len(rows)} rows{'  [appended]' if args.append else ''})")

    n_ok = len(logs) - len(failed)
    print(f"\nDone. {n_ok}/{len(logs)} battles parsed.")
    if skipped_non_llm:
        print(f"Skipped {skipped_non_llm} non-LLM reasonings.")
    if failed:
        print(f"\n{len(failed)} failures:")
        for path, err in failed[:10]:
            print(f"  {path}: {err}")
        if len(failed) > 10:
            print(f"  ... and {len(failed) - 10} more")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())