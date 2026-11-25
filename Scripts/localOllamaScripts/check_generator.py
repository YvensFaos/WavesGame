#!/usr/bin/env python3
"""
check_generator.py — turn a PTNF line into a benchmarker check file.

Input: either a JSON file containing one PTNF log line, or the raw PTNF
JSON pasted directly as the argument:

  python3 check_generator.py PTNF.json
  python3 check_generator.py PTNF.json --out check_luana_t0.json
  python3 check_generator.py '{"eventType":"PTNF","selfPosition":...}'

The PTNF line (logged by the game at an actor's decision point) looks like:

  {"eventType":"PTNF","selfPosition":{"x":7,"y":6},
   "movementPositions":[{"x":7,"y":6}, ...],
   "attackablePositions":[...],
   "actorId":"LLM|Ollama|llama3.1:8b|Luana D.|Green|1",
   "faction":"Green","turn":0,"timeStamp":3}

Output: a waves-check-2 JSON keeping the PTNF fields plus movement_positions
(the valid movement destinations, engine-computed) and attackable_positions
(the valid attacks from the current position). The benchmarker then checks:
movement must be in movement_positions; move_after_attack must fit the
remaining budget; the attack is checked against attackable_positions when
the actor stayed, otherwise by the attack rule (exactly Manhattan distance
2, distance 1 is a dead zone).

You can hand-edit the output afterwards to add a "human" block.
"""

import argparse
import json
import sys


MOVEMENT_BUDGET = 3   # orthogonal steps, shared between movement and moveAfterAttack
ATTACK_RANGE = 2      # EXACT Manhattan distance (distance 1 is a dead zone)


def parse_args():
    ap = argparse.ArgumentParser(
        description="Generate a benchmarker check file from a PTNF log line.")
    ap.add_argument("ptnf", metavar="PTNF",
                    help="PTNF JSON file, or the raw PTNF JSON string.")
    ap.add_argument("--out", metavar="FILE",
                    help="Write the check to FILE (default: stdout).")
    ap.add_argument("--indent", type=int, default=2, help="JSON indent (default 2).")
    return ap.parse_args()


def load_ptnf(arg):
    """Accept a path to a JSON file or the raw JSON string itself."""
    stripped = arg.strip()
    if stripped.startswith("{"):
        try:
            return json.loads(stripped)
        except json.JSONDecodeError as exc:
            print(f"ERROR: cannot parse the PTNF JSON string: {exc}", file=sys.stderr)
            return None
    try:
        with open(arg, encoding="utf-8-sig") as f:
            return json.load(f)
    except (OSError, json.JSONDecodeError) as exc:
        print(f"ERROR: cannot read PTNF from '{arg}' (neither a readable JSON "
              f"file nor a JSON string): {exc}", file=sys.stderr)
        return None


def as_pairs(raw):
    """[{"x":7,"y":6}, ...] or [[7,6], ...] -> [[x, y], ...], skipping junk."""
    out = []
    for p in raw or []:
        try:
            if isinstance(p, dict):
                out.append([int(p["x"]), int(p["y"])])
            else:
                out.append([int(p[0]), int(p[1])])
        except (TypeError, ValueError, KeyError, IndexError):
            continue
    return out


def main():
    args = parse_args()
    ptnf = load_ptnf(args.ptnf)
    if ptnf is None:
        return 1

    etype = ptnf.get("eventType")
    if etype != "PTNF":
        print(f"WARNING: eventType is '{etype}', not 'PTNF' — building the "
              f"check anyway.", file=sys.stderr)

    self_pos = ptnf.get("selfPosition") or {}
    if "x" not in self_pos or "y" not in self_pos:
        print("ERROR: the PTNF line has no selfPosition.", file=sys.stderr)
        return 1
    origin = [int(self_pos["x"]), int(self_pos["y"])]

    movement_positions = as_pairs(ptnf.get("movementPositions"))
    attackable_positions = as_pairs(ptnf.get("attackablePositions"))
    if origin not in movement_positions:
        movement_positions.insert(0, origin)
        print("WARNING: movementPositions did not include the actor's own tile; "
              "added it as a legal stay.", file=sys.stderr)

    check = {
        "format": "waves-check-2",
        "turn": ptnf.get("turn"),
        "timeStamp": ptnf.get("timeStamp"),
        "actor": ptnf.get("actorId"),
        "faction": ptnf.get("faction") or
            (str(ptnf.get("actorId", "")).split("|")[-2]
             if str(ptnf.get("actorId", "")).count("|") >= 2 else None),
        "position": origin,
        "budget": MOVEMENT_BUDGET,
        "attack_distance": ATTACK_RANGE,
        "movement_positions": movement_positions,
        "attackable_positions": attackable_positions,
    }

    text = json.dumps(check, indent=args.indent)
    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            f.write(text + "\n")
        print(f"wrote {args.out}")
    else:
        print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())