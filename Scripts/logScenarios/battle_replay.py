#!/usr/bin/env python3
"""
battle_replay.py — print the ASCII scenario snapshots (SCNR) of a battle
log, one board per turn.

The log format is JSONL: one JSON object per line, starting with an INFO
line (map, seed, roster). TURN lines mark turn boundaries; SCNR lines carry
the scenarioOverview ASCII grid. Multiple SCNR can occur inside one turn
(one per action resolution).

Usage:
    python3 battle_replay.py <logfile> [options]
    python3 battle_replay.py battle.txt               # all turns
    python3 battle_replay.py battle.txt -n 2           # first 2 turns only
    python3 battle_replay.py battle.txt --last         # end-of-turn board instead of start
    python3 battle_replay.py battle.txt --turns 1,3   # specific turns only
"""

import argparse
import json
import sys


def parse_args():
    ap = argparse.ArgumentParser(
        description="Print ASCII scenario snapshots of a Waves battle log, one per turn."
    )
    ap.add_argument("logfile", help="Battle log file (JSONL).")
    ap.add_argument("-n", "--num-turns", type=int, metavar="N",
                    help="Print only the first N turns.")
    ap.add_argument("--last", action="store_true",
                    help="Use the LAST SCNR of each turn (end-of-turn board) "
                         "instead of the first (start-of-turn).")
    ap.add_argument("--turns", metavar="T,T,...",
                    help="Print only these turns (e.g. 0,2,4).")
    ap.add_argument("--no-header", action="store_true",
                    help="Do not print the INFO banner or per-turn titles.")
    return ap.parse_args()


def load_log(path):
    rows = []
    with open(path, encoding="utf-8-sig") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                rows.append(json.loads(line))
            except json.JSONDecodeError as exc:
                print(f"WARNING: skipping unparseable line: {exc}", file=sys.stderr)
    return rows


def group_by_turn(rows):
    turns = {}
    order = []
    for row in rows:
        t = row.get("turn")
        if t is None:
            continue
        if t not in turns:
            turns[t] = []
            order.append(t)
        turns[t].append(row)
    return turns, order


def print_banner(info):
    print("=" * 70)
    print(f"{info.get('map', '?')}   seed {info.get('randomSeed', '?')}   "
          f"maxTurns {info.get('maxTurns', '?')}")
    print(f"start {info.get('startTime', '?')[:19]}   extra: {info.get('extraText', '')}")
    print("=" * 70)


def print_grid(board, label):
    print("-" * 70)
    print(label)
    print("-" * 70)
    for row in board:
        print("  " + row)
    print()


def pick_scnr(events, use_last):
    scnrs = [e for e in events if e.get("eventType") == "SCNR"]
    if not scnrs:
        return None
    return scnrs[-1] if use_last else scnrs[0]


def main():
    args = parse_args()
    rows = load_log(args.logfile)
    if not rows:
        print(f"ERROR: no events found in {args.logfile}", file=sys.stderr)
        return 1

    info = next((r for r in rows if r.get("eventType") == "INFO"), {})
    turns, order = group_by_turn(rows)

    if not args.no_header:
        print_banner(info)

    if args.turns:
        wanted = set()
        for part in args.turns.split(","):
            part = part.strip()
            if part.isdigit():
                wanted.add(int(part))
            else:
                print(f"ERROR: --turns expects integers, got '{part}'.", file=sys.stderr)
                return 1
    else:
        wanted = None

    printed = 0
    for t in order:
        if wanted is not None and t not in wanted:
            continue
        if args.num_turns is not None and printed >= args.num_turns:
            break
        events = turns[t]
        scnr = pick_scnr(events, args.last)
        printed += 1

        if not args.no_header:
            ts = [e.get("timeStamp") for e in events if e.get("timeStamp") is not None]
            span = f"   t={min(ts)}..{max(ts)}   ({len(events)} events)" if ts else ""
            if scnr is None:
                print(f"TURN {t}{span}   [no SCNR snapshot in this turn]")
                print("-" * 70)
                print("  (turn has no board state logged, e.g. game ended here)")
                print()
                continue
            print_grid(scnr.get("scenarioOverview", []), f"TURN {t}{span}")
        else:
            if scnr is None:
                continue
            for row in scnr.get("scenarioOverview", []):
                print("  " + row)

    if not args.no_header:
        over = next((r for r in rows if r.get("eventType") == "OVER"), None)
        if over:
            print("=" * 70)
            print(f"OVER: {over.get('winningFaction', '?')}   "
                  f"final {over.get('finalTime', '?')[:19]}")
            print("=" * 70)

    return 0


if __name__ == "__main__":
    sys.exit(main())