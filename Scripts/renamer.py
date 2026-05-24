#!/usr/bin/env python3
"""
Convert old Green faction log lines from dash-separated to pipe-separated format.

Old format:
  2026-05-24 11:22:57;[SYSTEM];[AIAgent-Utility-RandomGenes-Green-9];INFO Start turn

New format:
  2026-05-24 11:22:57;[SYSTEM];[LLMAgent|DeepSeek|deepseek-v4-flash|Green|9];INFO Start turn
"""

import re
import shutil
import argparse
from pathlib import Path

# Regex to match the old Green agent tag
# It captures the number (e.g., 9) as group 1
OLD_GREEN_PATTERN = re.compile(r'\[AIAgent-.*?-Green-(\d+)\]')


def extract_agent_type(tag: str) -> str:
    """
    Extract the third dash-separated component from a string.

    Example:
        Input:  "AIAgent-Utility-RandomGenes-Green-9]"
        Output: "RandomGenes"
    """
    parts = tag.split('-')
    if len(parts) >= 3:
        return parts[2]  # index 2 = third element
    return ""  # or raise an exception if not found


def transform_line(line: str) -> str:
    """Replace the old Green agent tag with the new pipe-separated format."""
    match = OLD_GREEN_PATTERN.search(line)
    if not match:
        return line  # no change

    number = match.group(1)
    agent_type = extract_agent_type(match.group(0))
    new_tag = f"[AIAgent|Utility|{agent_type}|Green|{number}]"
    # Replace only the matched bracket part
    new_line = OLD_GREEN_PATTERN.sub(new_tag, line)
    return new_line


def process_file(file_path: Path, dry_run: bool = False, backup: bool = True) -> None:
    """Read a file, transform Green lines, and optionally write back."""
    print(f"Processing: {file_path}")

    # Read original content
    with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
        lines = f.readlines()

    # Transform lines
    new_lines = [transform_line(line) for line in lines]

    # Check if any change occurred
    if new_lines == lines:
        print(f"  No Green lines found in {file_path.name}")
        return

    if dry_run:
        # Show a sample of what would change
        for old, new in zip(lines, new_lines):
            if old != new:
                print(f"  Would change:\n    OLD: {old.strip()}\n    NEW: {new.strip()}\n")
        return

    # Write changes
    if backup:
        backup_path = file_path.with_suffix(file_path.suffix + '.backup')
        shutil.copy2(file_path, backup_path)
        print(f"  Backup created: {backup_path}")

    with open(file_path, 'w', encoding='utf-8') as f:
        f.writelines(new_lines)
    print(f"  Updated: {file_path}")


def main():
    parser = argparse.ArgumentParser(
        description="Convert dash-separated Green faction log entries to pipe-separated format."
    )
    parser.add_argument(
        "folder",
        help="Path to the folder containing log files"
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Preview changes without modifying files"
    )
    parser.add_argument(
        "--no-backup",
        action="store_true",
        help="Do not create backup files (use with caution)"
    )
    parser.add_argument(
        "--ext",
        default=".log",
        help="File extension to process (default: .log); use '*' for all files"
    )

    args = parser.parse_args()

    folder = Path(args.folder)
    if not folder.is_dir():
        print(f"Error: '{folder}' is not a valid directory.")
        return

    # Determine which files to process
    if args.ext == '*':
        files = list(folder.iterdir())
    else:
        ext = args.ext if args.ext.startswith('.') else f'.{args.ext}'
        files = list(folder.glob(f"*{ext}"))

    if not files:
        print(f"No files with extension {args.ext} found in {folder}")
        return

    print(f"Found {len(files)} file(s) to check.")
    if args.dry_run:
        print("DRY RUN MODE – no files will be modified.")

    for file_path in files:
        if file_path.is_file():
            process_file(file_path, dry_run=args.dry_run, backup=not args.no_backup)

    print("Done.")


if __name__ == "__main__":
    main()