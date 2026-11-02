#!/usr/bin/env python3
"""
Batch-parse a folder of Battleground logs into one compiled JSONL file.

Usage:
    python batch_parse.py <input_folder> <output_file>
    python batch_parse.py logs/ battles.jsonl
    python batch_parse.py logs/ battles.jsonl --lean
    python batch_parse.py logs/ battles.jsonl --recursive --pattern "*.log"
    python batch_parse.py logs/ battles.jsonl --append

Each output line is a full summary record, same shape as
`game_parser.py --format jsonl`, but with `source_file` set to the
input log's filename (not the full path) so the JSONL stays portable.
"""

import argparse
import sys
import traceback
from pathlib import Path

# Ensure game_parser can be imported regardless of the current directory.
_THIS_DIR = Path(__file__).resolve().parent
if str(_THIS_DIR) not in sys.path:
    sys.path.insert(0, str(_THIS_DIR))

try:
    import game_parser
except ImportError as exc:
    print(f"ERROR: could not import game_parser.py next to this script.\n{exc}")
    sys.exit(1)


def find_log_files(folder: Path, pattern: str, recursive: bool):
    if not folder.is_dir():
        raise NotADirectoryError(f"Not a directory: {folder}")
    globber = folder.rglob if recursive else folder.glob
    return sorted(p for p in globber(pattern) if p.is_file())


def main():
    ap = argparse.ArgumentParser(
        description="Compile a folder of Battleground logs into one JSONL file."
    )
    ap.add_argument("input_folder",
                    help="Folder containing the log files.")
    ap.add_argument("output_file",
                    help="Path to the compiled JSONL file.")
    ap.add_argument("--pattern", default="*.txt",
                    help="Glob pattern for log files (default: *.txt).")
    ap.add_argument("--recursive", action="store_true",
                    help="Search subfolders recursively.")
    ap.add_argument("--lean", action="store_true",
                    help="Drop per-actor and per-kill data from each summary.")
    ap.add_argument("--append", action="store_true",
                    help="Append to the output file instead of overwriting.")
    ap.add_argument("--quiet", action="store_true",
                    help="Only print the final summary, not each file.")
    args = ap.parse_args()

    input_dir = Path(args.input_folder)
    output_path = Path(args.output_file)

    try:
        files = find_log_files(input_dir, args.pattern, args.recursive)
    except NotADirectoryError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1

    if not files:
        print(f"No files matching '{args.pattern}' in {input_dir}",
              file=sys.stderr)
        return 1

    mode = "a" if args.append else "w"
    output_path.parent.mkdir(parents=True, exist_ok=True)

    ok_count = 0
    failed = []
    skipped = []

    with open(output_path, mode, encoding="utf-8") as fout:
        for i, path in enumerate(files, 1):
            prefix = f"[{i}/{len(files)}] {path.name}"
            try:
                events = game_parser.load_events(str(path))
                if not events:
                    skipped.append((path.name, "no events"))
                    if not args.quiet:
                        print(f"{prefix}: skipped (empty)")
                    continue

                summary = game_parser.summarise(events)
                line = game_parser.summary_to_jsonl(
                    summary,
                    source_file=path.name,   # filename only, not full path
                    lean=args.lean,
                )
                fout.write(line + "\n")
                ok_count += 1

                if not args.quiet:
                    winner = summary.get("winner_side") or "?"
                    matchup = summary.get("matchup_type") or "?"
                    print(f"{prefix}: ok "
                          f"({len(events)} events, "
                          f"winner={winner}, "
                          f"matchup={matchup})")

            except Exception as exc:
                failed.append((path.name, str(exc)))
                print(f"{prefix}: FAILED - {exc}", file=sys.stderr)
                if not args.quiet:
                    traceback.print_exc()

    # Final report
    print()
    print(f"Input folder:  {input_dir}")
    print(f"Output file:   {output_path}  ({mode})")
    print(f"Files found:   {len(files)}")
    print(f"Succeeded:     {ok_count}")
    if skipped:
        print(f"Skipped:       {len(skipped)}")
        for name, reason in skipped:
            print(f"  - {name}: {reason}")
    if failed:
        print(f"Failed:        {len(failed)}")
        for name, err in failed:
            print(f"  - {name}: {err}")
        return 2

    return 0


if __name__ == "__main__":
    sys.exit(main())