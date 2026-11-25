#!/usr/bin/env python3
"""
situation_miner.py — mine battle logs for decision states worth benchmarking.

Walks one or more battle logs (old SCNR-based or new PTNF-based) the same way
battle_replay.py does, stops at EVERY decision event (RESN, CMMD, MOVE, ATTK,
NVLD, PTNF — including retries and re-decisions after invalid moves), scores
how interesting each state is as a prompt-benchmarking situation, and prints
a ranked report. For the top picks it also prints the full state so you can
reproduce the scenario in the game and export its PTNF:

  - the ASCII board (SCNR) with the actor marked as *
  - every ship and target with its health
  - the wave tiles and obstacles
  - the features that earned the score
  - the battle_replay.py --state command to inspect the same moment again

Usage:
  python3 situation_miner.py battle1.txt
  python3 situation_miner.py battle1.txt battle2.txt --top 25 --detail 5
  python3 situation_miner.py battle1.txt --actor luana
  python3 situation_miner.py battle1.txt --no-color     # plain ASCII (piping)
  python3 situation_miner.py battle1.txt --color        # force colours

Output is coloured when stdout is a terminal (iTerm or any ANSI console):
Red ships in red, Green ships in green, the actor highlighted, waves cyan,
obstacles dim grey, targets yellow. Piped/redirected output stays plain
unless --color is forced.

"Interesting" (highest score) means: the option space gives genuinely
different outcomes. Low score means the state is solvable by echoing the
ATTACK NOW line.
"""

import argparse
import json
import os
import sys


# ------------------------------------------------------------------ tuning

MOVEMENT_BUDGET = 3   # orthogonal steps shared with moveAfterAttack
ATTACK_RANGE = 2      # EXACT Manhattan distance (1 is a dead zone)
SHIP_DAMAGE = 7       # damage an attack does to a ship after defense (PPGT: 9 raw -> 7)
OBSTACLE_DAMAGE = 5    # damage an attack does to an obstacle

# score weights (tune freely)
W_ENEMY_TARGET = 10    # per distinct enemy ship attackable from some move
W_KILLSHOT = 8         # per attackable enemy whose hp <= SHIP_DAMAGE
W_HP_SPREAD = 3        # hp difference between the strongest and weakest attackable enemy
W_MOVE_WITH_ATTACK = 2 # per reachable position that has at least one attack
W_WAVE_SHOT = 2        # per wave attack option
W_OBSTACLE_SHOT = 1    # per obstacle attack option
W_ALLY_FIRE = 4        # per ally ship in attack range (friendly fire possible)
W_DEAD_ZONE_TRAP = 5   # per enemy adjacent to a reachable/own tile (dead-zone bait)
W_TRIVIAL_PENALTY = 15 # subtracted when an enemy is attackable from the origin
                       # (the ATTACK NOW echo exists)


FACTION_SYMBOLS = {"Green": "G", "Red": "R"}
WAVE_SYMBOLS = set("^V<>")
TARGET_SYMBOL = "@"
OBSTACLE_SYMBOL = "&"
SHIP_SYMBOLS = set("GR")


def parse_args():
    ap = argparse.ArgumentParser(
        description="Rank battle-log decision states by how interesting they "
                    "are as prompt-benchmarking situations.")
    ap.add_argument("logs", nargs="+", metavar="LOG", help="Battle log files (JSONL).")
    ap.add_argument("--top", type=int, default=20,
                    help="Size of the ranked list (default 20).")
    ap.add_argument("--detail", type=int, default=5,
                    help="Full state blocks for the top N picks (default 5).")
    ap.add_argument("--actor", help="Only decisions of actors matching this "
                                    "(case-insensitive substring).")
    ap.add_argument("--color", action="store_true",
                    help="Force ANSI colours even when output is piped.")
    ap.add_argument("--no-color", action="store_true",
                    help="Plain ASCII, no ANSI colours.")
    ap.add_argument("--quiet", action="store_true",
                    help="Only the ranked list, no detail blocks.")
    return ap.parse_args()


USE_COLOR = False


def paint(text, code):
    """ANSI-paint text (iTerm/console) or return it unchanged."""
    if not USE_COLOR or not text:
        return text
    return f"\033[{code}m{text}\033[0m"


# ------------------------------------------------------- log reading / state

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
                print(f"WARNING: {path}: skipping unparseable line: {exc}",
                      file=sys.stderr)
    return rows


def short_name(actor_id):
    parts = str(actor_id).split("|")
    try:
        if parts[0].startswith("Player"):
            return f"Human{parts[2]}"
        if parts[0] == "AU":
            return f"{parts[1]} [{parts[3]}]"
        return f"{parts[3]} [{parts[-2]}]"
    except IndexError:
        return str(actor_id)


def faction_of(actor_id):
    parts = str(actor_id).split("|")
    return parts[-2] if len(parts) >= 2 else "Neutral"


def to_grid(board_rows):
    return [list(row) for row in board_rows]


def grid_to_rows(grid):
    return ["".join(row) for row in grid]


def tile(grid, pos):
    try:
        return grid[len(grid) - 1 - pos["y"]][pos["x"]]
    except (IndexError, TypeError, KeyError):
        return None


def apply_move(grid, move_from, move_to):
    ch = tile(grid, move_from)
    if ch is not None and ch != ".":
        try:
            grid[len(grid) - 1 - move_from["y"]][move_from["x"]] = "."
            grid[len(grid) - 1 - move_to["y"]][move_to["x"]] = ch
        except (IndexError, TypeError, KeyError):
            pass


def manhattan(a, b):
    return abs(a[0] - b[0]) + abs(a[1] - b[1])


# ------------------------------------------------------------ option space


def reachable(grid, start, budget=MOVEMENT_BUDGET):
    """Tiles reachable with orthogonal steps, blockers per the SCNR legend.
    wave tiles are passable. Returns {tile: steps}."""
    height, width = len(grid), len(grid[0]) if grid else 0

    def ch_at(p):
        if 0 <= p[0] < width and 0 <= p[1] < height:
            return grid[height - 1 - p[1]][p[0]]
        return "#"

    blocked = set()
    for y in range(height):
        for x, ch in enumerate(grid[height - 1 - y]):
            if ch in SHIP_SYMBOLS or ch in (OBSTACLE_SYMBOL, TARGET_SYMBOL) \
                    or ch == "#":
                blocked.add((x, y))
    blocked.discard(start)

    dist = {start: 0}
    frontier = [start]
    while frontier:
        nxt = []
        for x, y in frontier:
            if dist[(x, y)] >= budget:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                n = (x + dx, y + dy)
                if n in dist or n in blocked:
                    continue
                dist[n] = dist[(x, y)] + 1
                nxt.append(n)
        frontier = nxt
    return dist


def scnr_objects(grid):
    """{(x, y): symbol} for tiles the state cares about, y in log coords."""
    out = {}
    height = len(grid)
    for y in range(height):
        for x, ch in enumerate(grid[height - 1 - y]):
            if ch in SHIP_SYMBOLS or ch in WAVE_SYMBOLS \
                    or ch in (OBSTACLE_SYMBOL, TARGET_SYMBOL):
                out[(x, y)] = ch
    return out


def classify(grid, pos, own_symbol, enemy_symbol):
    ch = scnr_objects(grid).get(pos)
    if ch == own_symbol:
        return "ally"
    if ch == enemy_symbol:
        return "enemy"
    if ch == TARGET_SYMBOL:
        return "target"
    if ch == OBSTACLE_SYMBOL:
        return "obstacle"
    if ch in WAVE_SYMBOLS:
        return "wave"
    return None


def score_state(grid, origin, own_symbol, enemy_symbol, hp_of):
    """Score one decision state. Returns (score, features dict)."""
    reach = reachable(grid, origin)
    objs = scnr_objects(grid)
    enemy_targets, ally_targets, wave_targets = {}, {}, 0
    obstacle_targets = target_targets = 0
    moves_with_attack = 0
    for pos in reach:
        attacks = False
        for o in objs:
            if manhattan(pos, o) != ATTACK_RANGE:
                continue
            k = classify(grid, o, own_symbol, enemy_symbol)
            if k == "enemy":
                enemy_targets[o] = hp_of.get(o)
            elif k == "ally":
                ally_targets[o] = hp_of.get(o)
            elif k == "wave":
                wave_targets += 1
            elif k == "obstacle":
                obstacle_targets += 1
            elif k == "target":
                target_targets += 1
            if k:
                attacks = True
        if attacks:
            moves_with_attack += 1

    killshots = {p for p, hp in enemy_targets.items()
                 if hp is not None and hp <= SHIP_DAMAGE}
    hps = [hp for hp in enemy_targets.values() if hp is not None]
    spread = (max(hps) - min(hps)) if len(hps) >= 2 else 0

    # dead-zone bait: enemy adjacent to the origin or to a reachable tile
    any_reach = set(reach)
    traps = {p for p in enemy_targets
             if any(manhattan(p, q) == 1 for q in any_reach)}
    trapped_enemies = {p for p, ch in objs.items()
                       if ch == enemy_symbol and p not in enemy_targets
                       and any(manhattan(p, q) == 1 for q in any_reach)}

    origin_attacks = any(manhattan(origin, o) == ATTACK_RANGE
                         and classify(grid, o, own_symbol, enemy_symbol) == "enemy"
                         for o in objs)

    score = (W_ENEMY_TARGET * len(enemy_targets)
             + W_KILLSHOT * len(killshots)
             + W_HP_SPREAD * spread
             + W_MOVE_WITH_ATTACK * moves_with_attack
             + W_WAVE_SHOT * wave_targets
             + W_OBSTACLE_SHOT * obstacle_targets
             + W_ALLY_FIRE * len(ally_targets)
             + W_DEAD_ZONE_TRAP * (len(traps) + len(trapped_enemies)))
    if origin_attacks:
        score -= W_TRIVIAL_PENALTY

    feats = {
        "position": origin,
        "reachable": len(reach) - 1,
        "moves_with_attack": moves_with_attack,
        "enemy_targets": {p: hp_of.get(p) for p in enemy_targets},
        "killshots": sorted(killshots),
        "hp_spread": spread,
        "ally_targets": {p: hp_of.get(p) for p in ally_targets},
        "wave_options": wave_targets,
        "obstacle_options": obstacle_targets,
        "target_options": target_targets,
        "dead_zone_bait": sorted(traps | trapped_enemies),
        "trivial_echo": origin_attacks,
    }
    return score, feats


# ------------------------------------------------------------- the mining


def mine_log(path, actor_filter):
    """Yield one scored entry per decision event in the log."""
    rows = load_log(path)
    info = next((r for r in rows if r.get("eventType") == "INFO"), {})
    hp = {e.get("name", ""): e.get("currentHealth")
          for e in info.get("navalActorEntryJsons", [])}
    positions = {e.get("name", ""): dict(e["position"])
                 for e in info.get("navalActorEntryJsons", [])
                 if e.get("position")}
    grid = None
    dead = set()
    seen = {}

    for e in rows:
        k = e.get("eventType")
        if k == "SCNR":
            grid = to_grid(e.get("scenarioOverview", []))
        elif k == "MOVE" and e.get("actorId") in positions and e.get("moveTo"):
            if grid:
                apply_move(grid, e.get("moveFrom"), e.get("moveTo"))
            positions[e["actorId"]] = dict(e["moveTo"])
        elif k == "DEAD":
            dead.add(e.get("actorId"))
            positions.pop(e.get("actorId"), None)
            hp.pop(e.get("actorId"), None)
        elif k == "DAMG" and e.get("actorId") in hp:
            hp[e["actorId"]] = max(0, hp[e["actorId"]] - e.get("damage", 0))

        is_decision = k in ("RESN", "CMMD", "MOVE", "ATTK", "NVLD", "PTNF")
        if not is_decision or grid is None:
            continue
        actor = e.get("actorId")
        faction = faction_of(actor)
        if faction not in FACTION_SYMBOLS or actor in dead:
            continue
        if actor_filter and actor_filter.lower() not in str(actor).lower():
            continue

        own_symbol = FACTION_SYMBOLS[faction]
        enemy_symbol = FACTION_SYMBOLS.get(
            "Red" if faction == "Green" else "Green")
        pos = positions.get(actor)
        if pos is None:
            continue
        origin = (pos["x"], pos["y"])
        # snap to the board if wave displacement lost the tracked actor
        if tile(grid, pos) not in (own_symbol, None):
            claimed = {(p["x"], p["y"])
                       for name, p in positions.items()
                       if name != actor and name not in dead
                       and faction_of(name) in FACTION_SYMBOLS}
            board_ships = {p for p, ch in scnr_objects(grid).items()
                           if ch == own_symbol}
            candidates = sorted(board_ships - claimed - {origin})
            if len(candidates) == 1:
                origin = candidates[0]
                positions[actor] = {"x": origin[0], "y": origin[1]}
            else:
                print(f"WARNING: {path}: cannot locate {short_name(actor)} "
                      f"on the board at turn {e.get('turn')}, skipped.",
                      file=sys.stderr)
                continue

        hp_of = {(p["x"], p["y"]): hp.get(name)
                 for name, p in positions.items()
                 if name not in dead}
        score, feats = score_state(grid, origin, own_symbol, enemy_symbol,
                                   hp_of)

        ships, targets, waves, obstacles = [], [], [], []
        for (x, y), ch in sorted(scnr_objects(grid).items()):
            if ch == TARGET_SYMBOL:
                targets.append((x, y))
            elif ch == OBSTACLE_SYMBOL:
                obstacles.append((x, y))
            elif ch in WAVE_SYMBOLS:
                waves.append((x, y, ch))
        for name, p in positions.items():
            if name not in dead and faction_of(name) in FACTION_SYMBOLS:
                ships.append({"id": name, "faction": faction_of(name),
                              "hp": hp.get(name), "position": [p["x"], p["y"]]})
        targets.sort()
        obstacles.sort()

        entry = {
            "score": score, "features": feats,
            "log": path, "turn": e.get("turn"), "time": e.get("timeStamp"),
            "actor": actor, "faction": faction, "pos": origin,
            "board": [row[:] for row in grid], "ships": ships,
            "targets": targets, "waves": sorted(waves),
            "obstacles": obstacles,
        }
        key = (path, e.get("turn"), actor, origin)
        if key in seen and seen[key]:
            continue   # identical retry of the same decision: keep one
        seen[key] = True
        yield entry


def feature_line(f):
    bits = []
    et = {tuple(k): v for k, v in f["enemy_targets"].items()}
    bits.append(f"{len(f['enemy_targets'])} enemy target(s)")
    if f["killshots"]:
        bits.append(f"KILLSHOT at {f['killshots']}")
    if f["hp_spread"]:
        bits.append(f"hp spread {f['hp_spread']}")
    if f["ally_targets"]:
        bits.append(f"ally fire possible ({len(f['ally_targets'])})")
    if f["dead_zone_bait"]:
        bits.append(f"dead-zone bait at {f['dead_zone_bait']}")
    if f["wave_options"]:
        bits.append(f"{f['wave_options']} wave shot(s)")
    if f["obstacle_options"]:
        bits.append(f"{f['obstacle_options']} obstacle shot(s)")
    if f["target_options"]:
        bits.append(f"{f['target_options']} neutral target(s)")
    bits.append(f"{f['moves_with_attack']}/{f['reachable']} moves with attacks")
    if f["trivial_echo"]:
        bits.append("TRIVIAL: enemy attackable from origin (ATTACK NOW echo)")
    return ", ".join(bits)


def detail_block(entry, rank):
    f = entry["features"]
    print("=" * 70)
    print(f"#{rank}  score {entry['score']}  —  {short_name(entry['actor'])} "
          f"at {entry['pos']}  turn {entry['turn']}  t={entry['time']}")
    print("=" * 70)
    print(f"  log      : {entry['log']}")
    print(f"  inspect  : python3 battle_replay.py {entry['log']} "
          f"--state {entry['turn']} '{entry['actor']}'")
    # readable labels: A,B,C for Red ships, 1,2,3 for Green ships
    def pos_key(s):
        return (s["position"][1], s["position"][0])
    reds = sorted([s for s in entry["ships"] if s["faction"] == "Red"],
                  key=pos_key)
    greens = sorted([s for s in entry["ships"] if s["faction"] == "Green"],
                    key=pos_key)
    LETTERS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
    DIGITS = "1234567890"
    labels = {}
    for i, s in enumerate(reds):
        labels[tuple(s["position"])] = LETTERS[i] if i < len(LETTERS) else "?"
    for i, s in enumerate(greens):
        labels[tuple(s["position"])] = DIGITS[i] if i < len(DIGITS) else "?"
    actor = (entry["pos"][0], entry["pos"][1])

    g = [row[:] for row in entry["board"]]
    height, width = len(g), len(g[0]) if g else 0
    RED_CODE = "1;31"          # bold red letters
    GREEN_CODE = "1;32"        # bold green digits
    ACTOR_CODE = "1;4;33;44"   # bold underline yellow on blue
    WAVE_CODE = "36"
    OBSTACLE_CODE = "90"
    TARGET_CODE = "33"
    for row_i, row in enumerate(g):
        y = height - 1 - row_i
        for x in range(min(width, len(row))):
            lab = labels.get((x, y))
            if lab:
                code = RED_CODE if (x, y) in {tuple(s["position"]) for s in reds} \
                    else GREEN_CODE
                if (x, y) == actor:
                    code = ACTOR_CODE
                row[x] = paint(lab, code)
            elif row[x] in WAVE_SYMBOLS:
                row[x] = paint(row[x], WAVE_CODE)
            elif row[x] == OBSTACLE_SYMBOL:
                row[x] = paint(row[x], OBSTACLE_CODE)
            elif row[x] == TARGET_SYMBOL:
                row[x] = paint(row[x], TARGET_CODE)
    print("  " + "-" * 66)
    print(f"  board (letters = Red, digits = Green, "
          f"actor is {labels.get(actor, '?')} at {actor}):")
    print("  " + "-" * 66)
    for row in grid_to_rows(g):
        print(f"  {row}")
    ships = entry["ships"]
    print("  ships (letters = Red, digits = Green; Red first, then Green):")
    for s in reds + greens:
        lab = labels.get(tuple(s["position"]), "?")
        is_red = s["faction"] == "Red"
        code = RED_CODE if is_red else GREEN_CODE
        if tuple(s["position"]) == actor:
            code = ACTOR_CODE
        mark = "   <- ACTOR (this decision)" if tuple(s["position"]) == actor else ""
        print(f"    {paint(lab, code):>2}  ({s['position'][0]:>2},{s['position'][1]:>2})  "
              f"[{s['faction']:<5}] hp {s['hp'] if s['hp'] is not None else '?':<3} "
              f" {short_name(s['id'])}{mark}")
    if entry["targets"]:
        print("  neutral targets:")
        for x, y in entry["targets"]:
            print(f"    ({x:>2},{y:>2})  hp ?")
    if entry["waves"]:
        print("  waves:")
        for x, y, ch in entry["waves"]:
            print(f"    ({x:>2},{y:>2})  ~{ch}")
    if entry["obstacles"]:
        print(f"  obstacles: {entry['obstacles']}")
    print("  features:")
    print(f"    {feature_line(f)}")
    print()


def main():
    args = parse_args()
    global USE_COLOR
    USE_COLOR = (not args.no_color) and (args.color or sys.stdout.isatty())
    entries = []
    for path in args.logs:
        n_before = len(entries)
        entries.extend(mine_log(path, args.actor))
        if len(entries) == n_before:
            print(f"WARNING: no decisions found in {path}.", file=sys.stderr)
    if not entries:
        print("ERROR: no scored decisions.", file=sys.stderr)
        return 1

    entries.sort(key=lambda e: (-e["score"], str(e["log"]), e["turn"] or 0))

    print("=" * 70)
    print(f"SITUATION MINING — {len(entries)} decision events from "
          f"{len(args.logs)} log(s)")
    print("=" * 70)
    print(f"  {'rank':>4}  {'score':>5}  {'game state':<44} interesting bits")
    print("  " + "-" * 100)
    for i, e in enumerate(entries[:args.top], 1):
        where = (f"{os.path.basename(e['log'])} t{e['turn']:<3} "
                 f"{short_name(e['actor'])[:16]} @{e['pos']}")
        print(f"  {i:>4}  {e['score']:>5}  {where:<44} "
              f"{feature_line(e['features'])[:60]}")
    if len(entries) > args.top:
        print(f"  ... {len(entries) - args.top} more (use --top to list more)")
    print()
    if not args.quiet:
        for i, e in enumerate(entries[:args.detail], 1):
            detail_block(e, i)
    return 0


if __name__ == "__main__":
    sys.exit(main())