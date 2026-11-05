#!/usr/bin/env python3
"""
move_logs.py — move a set of log files to a backup folder.

The list file should contain one filename per line (the basenames that appear
in the JSONL `source_file` field). Files are matched by name, case-sensitively,
against every file under the --from folder.

Usage:
  # 1. Dry run first — always
  python3 move_logs.py --from logs/alllogs --list delete_list.txt \
                       --to logs/_backup/moved --dry-run

  # 2. Actually move
  python3 move_logs.py --from logs/alllogs --list delete_list.txt \
                       --to logs/_backup/moved

  # 3. Recursive search (if your logs live in subfolders)
  python3 move_logs.py --from logs/alllogs --list delete_list.txt \
                       --to logs/_backup/moved --recursive

  # 4. Undo — same script, swapped --from and --to
  python3 move_logs.py --from logs/_backup/moved --list delete_list.txt \
                       --to logs/alllogs
"""

import argparse
import shutil
import sys
from collections import defaultdict
from pathlib import Path


def load_list(path):
    """Return a list of non-empty, non-comment lines (whitespace-stripped)."""
    out = []
    with open(path, "r", encoding="utf-8-sig") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            out.append(line)
    return out


def build_index(src_dir, recursive):
    """Map basename -> list of full paths (one entry per match)."""
    index = defaultdict(list)
    glob = src_dir.rglob if recursive else src_dir.glob
    for p in glob("*"):
        if p.is_file():
            index[p.name].append(p)
    return index


def main():
    ap = argparse.ArgumentParser(
        description="Move a set of log files to a backup folder."
    )
    ap.add_argument("--from", dest="source", required=True,
                    help="Folder containing the files to move.")
    ap.add_argument("--list", dest="list_file", required=True,
                    help="Text file with one filename per line.")
    ap.add_argument("--to", dest="dest", required=True,
                    help="Destination folder (created if missing).")
    ap.add_argument("--recursive", action="store_true",
                    help="Also look inside subfolders of --from.")
    ap.add_argument("--overwrite", action="store_true",
                    help="Overwrite files already present in --to.")
    ap.add_argument("--dry-run", action="store_true",
                    help="Show what would be moved, but do nothing.")
    args = ap.parse_args()

    src = Path(args.source)
    dst = Path(args.dest)

    if not src.is_dir():
        print(f"ERROR: source folder does not exist: {src}", file=sys.stderr)
        return 1

    if not Path(args.list_file).is_file():
        print(f"ERROR: list file does not exist: {args.list_file}", file=sys.stderr)
        return 1

    names = load_list(args.list_file)
    print(f"List file:   {args.list_file}")
    print(f"Entries:     {len(names)}")
    print(f"Source:      {src}")
    print(f"Destination: {dst}")
    print(f"Mode:        {'DRY RUN' if args.dry_run else 'MOVE'}")
    print()

    index = build_index(src, args.recursive)
    print(f"Indexed {sum(len(v) for v in index.values())} files "
          f"({len(index)} unique basenames)")
    print()

    if not args.dry_run:
        dst.mkdir(parents=True, exist_ok=True)

    moved, missing, ambiguous, skipped = 0, [], [], []

    for name in names:
        hits = index.get(name, [])
        if not hits:
            missing.append(name)
            continue
        if len(hits) > 1:
            ambiguous.append((name, [str(p) for p in hits]))
            continue

        src_path = hits[0]
        dst_path = dst / src_path.name

        if dst_path.exists() and not args.overwrite:
            skipped.append(name)
            continue

        if args.dry_run:
            print(f"  WOULD MOVE  {src_path.name}")
        else:
            shutil.move(str(src_path), str(dst_path))
            print(f"  MOVED       {src_path.name}")
        moved += 1

    print()
    print("=" * 70)
    print("SUMMARY")
    print("=" * 70)
    print(f"  Moved:      {moved}")
    print(f"  Missing:    {len(missing)}")
    print(f"  Ambiguous:  {len(ambiguous)}")
    print(f"  Skipped:    {len(skipped)}")

    if missing:
        print()
        print("Missing (listed but not found in source):")
        for n in missing:
            print(f"  {n}")
    if ambiguous:
        print()
        print("Ambiguous (multiple files match; not moved):")
        for n, paths in ambiguous:
            print(f"  {n}")
            for p in paths:
                print(f"      {p}")
    if skipped:
        print()
        print("Skipped (already in destination; use --overwrite):")
        for n in skipped:
            print(f"  {n}")

    return 0


if __name__ == "__main__":
    sys.exit(main())