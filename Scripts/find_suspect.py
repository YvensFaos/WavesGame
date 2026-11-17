#!/usr/bin/env python3
"""
find_suspect.py — list all battles for one session and flag incomplete ones.

Usage:
    python3 find_suspect.py <file>.jsonl <sessionId>
"""

import json
import sys
from collections import Counter

path    = sys.argv[1] if len(sys.argv) > 1 else "file.jsonl"
target  = sys.argv[2] if len(sys.argv) > 2 else None
if not target:
    print("usage: find_suspect.py <jsonl> <session_tag>")
    sys.exit(1)

rows = []
for line in open(path, encoding="utf-8-sig"):
    line = line.strip()
    if not line:
        continue
    b = json.loads(line)
    s = b.get("summary", {})
    if s.get("session_tag") != target:
        continue

    winner   = s.get("winner_side")
    is_draw  = bool(s.get("is_draw"))
    reason   = s.get("win_reason")
    turns    = s.get("final_turn") or 0
    real_s   = s.get("real_duration_s")
    final_iso = s.get("final_time_iso")
    commits  = sum(len(v) for v in (s.get("human_commit_times") or {}).values())
    evt      = s.get("event_counts") or {}

    flags = []
    if winner is None and not is_draw:
        flags.append("NO_WINNER")
    if final_iso is None:
        flags.append("NO_OVER")
    if reason not in ("elimination", "timeout_survivors", "timeout_tie"):
        flags.append(f"reason={reason!r}")
    if "OVER" not in evt:
        flags.append("no_OVER_event")

    rows.append({
        "source":    b.get("source_file") or "?",
        "map":       s.get("map") or "?",
        "variant":   None,  # filled below if needed
        "winner":    winner,
        "draw":      is_draw,
        "reason":    reason,
        "turns":     turns,
        "real_s":    real_s,
        "commits":   commits,
        "flags":     flags,
    })

rows.sort(key=lambda r: (r["real_s"] if r["real_s"] is not None else -1))
print(f"{len(rows)} battles for session {target}\n")
print(f"{'source_file':<60} {'turns':>5} {'real_s':>8} {'commits':>7}  winner   flags")
print("-" * 160)
for r in rows:
    print(f"{r['source']:<90} {r['turns']:>5} "
          f"{(f'{r[chr(114)+chr(101)+chr(97)+chr(108)+chr(95)+chr(115)]:.1f}' if r['real_s'] is not None else '-'):>8} "
          f"{r['commits']:>7}  "
          f"{(r['winner'] or '-'):<8} {' '.join(r['flags'])}")