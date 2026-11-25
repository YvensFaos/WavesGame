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
    ap.add_argument("--actions", action="store_true",
                    help="Show every action in the selected turn(s): legend, the "
                         "board after it, and for LLM actions the CMMD + reasoning.")
    ap.add_argument("--state", nargs=2, metavar=("TURN", "ACTOR"),
                    help="Enumerate the answer key for one actor's decision: "
                         "every reachable tile (3 orthogonal steps, shared with "
                         "moveAfterAttack) and every valid attack (enemy ships, "
                         "@ targets and & obstacles at exactly Manhattan "
                         "distance 2; distance 1 is a dead zone) "
                         "from each. ACTOR is a case-insensitive substring of the "
                         "actor id.")
    ap.add_argument("--json", action="store_true",
                    help="With --state: print the answer key as JSON instead of "
                         "the report.")
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



NOISE_EVENTS = {"INPT", "STFX", "GSTT", "TURN", "INFO", "OVER"}


def short_name(actor_id):
    """'LLM|Ollama|ministral-3:8b|Max X.|Green|1' -> 'Max X. [Green]';
    'AU|Audo Mc F.|HA|Red|4' -> 'Audo Mc F. [Red]';
    'PlayerSimpleRedBoat Variant(Clone)|Red|3' -> 'Human [Red|3]'."""
    parts = str(actor_id).split("|")
    try:
        if parts[0].startswith("Player"):
            return f"Human [{parts[1]}|{parts[2]}]"
        if parts[0] == "AU":
            return f"{parts[1]} [{parts[3]}]"
        return f"{parts[3]} [{parts[-2]}]"
    except IndexError:
        return str(actor_id)


def wrap_text(text, width=64, indent="    "):
    import textwrap
    return "\n".join(indent + line for line in
                     __import__("textwrap").wrap(str(text), width=width))


def coord(pos):
    return f"({pos.get('x')},{pos.get('y')})" if isinstance(pos, dict) else str(pos)


SHIP_SYMBOLS = set("GR@T")


def to_grid(board_rows):
    """SCNR rows (strings) -> mutable grid (list of lists of chars)."""
    return [list(row) for row in board_rows]


def grid_to_rows(grid):
    return ["".join(row) for row in grid]


def tile(grid, pos):
    """Log coordinates (x, y) -> grid cell. The ASCII board is vertically
    flipped relative to the logged y: display_row = len(board) - 2 - y."""
    try:
        return grid[len(grid) - 1 - pos["y"]][pos["x"]]
    except (IndexError, TypeError, KeyError):
        return None


def set_tile(grid, pos, char):
    try:
        grid[len(grid) - 1 - pos["y"]][pos["x"]] = char
    except (IndexError, TypeError, KeyError):
        pass


def apply_move(grid, move_from, move_to):
    ch = tile(grid, move_from)
    if ch is not None and ch != ".":
        set_tile(grid, move_from, ".")
        set_tile(grid, move_to, ch)


def print_board(grid, label):
    print("-" * 70)
    print(f"  {label}")
    print("-" * 70)
    for row in grid_to_rows(grid):
        print("  " + row)
    print()


def consequences_of(events, idx, target_id):
    """(died, summary) for damage/death lines right after an attack/push.
    Uses HEALTH (pre-damage, since the DAMG event has not been walked yet)
    to include the hp left after the hit."""
    parts, died, dmg_total = [], False, 0
    for ev in events[idx + 1:idx + 6]:
        et = ev.get("eventType")
        if et == "DAMG" and ev.get("actorId") == target_id:
            dmg_total += ev.get("damage", 0)
            parts.append(f"takes {ev.get('damage')} damage")
        elif et == "DEAD" and ev.get("actorId") == target_id:
            parts.append("DIES")
            died = True
        elif et == "DAMG" and ev.get("actorId") != target_id:
            break
        elif et not in ("DAMG", "DEAD"):
            break
    hp0 = HEALTH.get(target_id)
    if hp0 is not None and dmg_total and "DIES" not in parts:
        hp_left = max(0, hp0 - dmg_total)
        parts.append(f"{hp_left} hp left")
    summary = ""
    if parts:
        name = short_name(target_id) if "|" in str(target_id) else target_id
        summary = f"{name} " + ", ".join(parts)
    return died, summary


def render_turn_actions(events, turn_no, rows=None):
    """Walk one turn's events, printing the board before the turn, then each
    action with the computed board after it. Logged SCNRs resync the grid so
    pushes/deaths never drift."""
    # board state persists across turns via module-level carry
    resn_by_actor, cmmd_by_actor = {}, {}
    computed = REPLAY_STATE.get("grid")
    reset_health_to_turn(rows, turn_no)
    reset_positions_to_turn(rows, turn_no)

    print("=" * 70)
    print(f"TURN {turn_no} - ACTIONS")
    print("=" * 70)

    # seed the board with the first SCNR of this turn if we have none yet
    if computed is None:
        seed = next((e for e in events if e.get("eventType") == "SCNR"), None)
        if seed is None:
            print("  [no SCNR snapshot in this turn - nothing to draw]")
            return
        computed = to_grid(seed.get("scenarioOverview", []))

    print_board(computed, f"board at start of turn {turn_no}")
    print("  ship health at start of turn:")
    for name, hp in HEALTH.items():
        if hp is not None:
            print(f"    {short_name(name)}: {hp}")

    for idx, e in enumerate(events):
        etype = e.get("eventType")
        if etype in NOISE_EVENTS:
            continue
        if etype == "SCNR":
            computed = to_grid(e.get("scenarioOverview", []))
            REPLAY_STATE["grid"] = computed
            continue

        actor = e.get("actorId", "?")
        who = short_name(actor)

        if etype == "RESN":
            resn_by_actor[actor] = e
            continue
        if etype == "CMMD":
            cmmd_by_actor[actor] = e
            continue
        if etype in ("DAMG", "DEAD"):
            if etype == "DAMG" and e.get("actorId") in HEALTH:
                HEALTH[e["actorId"]] = max(0,
                    HEALTH[e["actorId"]] - e.get("damage", 0))
            elif etype == "DEAD":
                HEALTH.pop(e.get("actorId"), None)
            continue  # folded into ATTK/PPGT legends

        if etype in ("MOVE", "ATTK") and (actor in resn_by_actor or actor in cmmd_by_actor):
            resn = resn_by_actor.pop(actor, None)
            cmmd = cmmd_by_actor.pop(actor, None)
            if resn:
                print(f"  LLM {who} - reasoning ({resn.get('responseTime', '?')} "
                      f"ms response time):")
                print(wrap_text(resn.get("reasoning", "")))
                print(f"    reasoning-raw field from log: {resn.get('reasoning', '')}")
            if cmmd:
                mv, at, maa = (cmmd.get("movement") or {}, cmmd.get("attack") or {},
                               cmmd.get("moveAfterAttack") or {})
                print(f"  CMMD: movement={coord(mv)} attack={coord(at)} "
                      f"moveAfterAttack={coord(maa)}")

        if etype == "MOVE":
            apply_move(computed, e.get("moveFrom"), e.get("moveTo"))
            if actor in POSITIONS and e.get("moveTo"):
                POSITIONS[actor] = dict(e["moveTo"])
            print(f"  MOVE: {who} from {coord(e.get('moveFrom'))} "
                  f"to {coord(e.get('moveTo'))}")
        elif etype == "ATTK":
            target = e.get("targetId", "?")
            target_name = short_name(target) if "|" in str(target) else target
            died, summary = consequences_of(events, idx, target)
            print(f"  ATTACK: {who} strikes {target_name} at "
                  f"{coord(e.get('attackPosition'))} for {e.get('damage', '?')} damage"
                  + (f" -> {summary}" if summary else ""))
            if died:
                set_tile(computed, e.get("attackPosition"), ".")
        elif etype == "PPGT":
            target = e.get("targetId", "?")
            target_name = short_name(target) if "|" in str(target) else target
            died, summary = consequences_of(events, idx, target)
            print(f"  WAVE PUSH: {e.get('originId', '?')} hits {target_name} "
                  f"for {e.get('damage', '?')} damage"
                  + (f" -> {summary}" if summary else ""))
        elif etype == "NVLD":
            cur = POSITIONS.get(actor)
            tried = e.get("position")
            vtype = str(e.get("type", "?"))
            if vtype == "FailedToMove":
                # position may be the blocked destination, targetPosition may
                # be the same; either way this is a failed MOVE, not an attack
                dest = tried if tried != cur else e.get("targetPosition")
                print(f"  INVALID (FailedToMove): {who} "
                      f"(staying at {coord(cur) if cur else '?'}) "
                      f"could not move to {coord(dest)}")
            else:
                bits = []
                if tried and tried != cur:
                    bits.append(f"move to {coord(tried)}")
                elif tried and tried == cur:
                    bits.append("no movement needed")
                if e.get("targetId") or e.get("targetPosition"):
                    tname = (short_name(e["targetId"]) if "|" in str(e.get("targetId", ""))
                             else e.get("targetId") or "target")
                    bits.append(f"attack {tname} at {coord(e.get('targetPosition'))}")
                print(f"  INVALID ({vtype}): {who} "
                      f"(currently at {coord(cur) if cur else '?'}) "
                      + (", ".join(bits) if bits else "unspecified action"))
            # flush the pending decision block so a pure invalid decision
            # (stay + failed attack, like a dead-zone attack) also shows
            # the model's reasoning and CMMD, not just MOVE/ATTK flows
            resn_p = resn_by_actor.pop(actor, None)
            cmmd_p = cmmd_by_actor.pop(actor, None)
            reasoning = (resn_p or {}).get("reasoning") or e.get("reasoning")
            if reasoning:
                if resn_p:
                    print(f"  LLM {who} - reasoning "
                          f"({resn_p.get('responseTime', '?')} ms response time):")
                print(wrap_text(reasoning))
                print(f"    reasoning-raw field from log: {reasoning}")
            if cmmd_p:
                mv, at, maa = (cmmd_p.get("movement") or {},
                              cmmd_p.get("attack") or {},
                              cmmd_p.get("moveAfterAttack") or {})
                print(f"  CMMD: movement={coord(mv)} attack={coord(at)} "
                      f"moveAfterAttack={coord(maa)}")
        else:
            continue

        print_board(computed, f"board after t={e.get('timeStamp', '?')} "
                              f"(turn {turn_no})")

    REPLAY_STATE["grid"] = computed


REPLAY_STATE = {}
POSITIONS = {}  # actorId -> current {"x":..,"y":..}, seeded from INFO roster
HEALTH = {}     # actorId -> current hp (ships only, not targets/obstacles)


def seed_positions(info):
    for entry in info.get("navalActorEntryJsons", []):
        if entry.get("position"):
            POSITIONS[entry.get("name", "")] = dict(entry["position"])
        HEALTH[entry.get("name", "")] = entry.get("currentHealth")


def reset_health_to_turn(rows, turn_no):
    """Rebuild HEALTH as it was at the START of turn_no, by walking every
    DAMG/DEAD event that happened in earlier turns."""
    if rows is None:
        return
    first = next((r for r in rows if r.get("eventType") == "INFO"), {})
    HEALTH.clear()
    seed_positions(first)
    for e in rows:
        # stop at the first event of the target turn (any type), so turns
        # with no damage/moves never run past the turn boundary
        if e.get("eventType") != "INFO" and e.get("turn", -1) >= turn_no:
            break
        if e.get("eventType") == "DAMG" and e.get("actorId") in HEALTH:
            HEALTH[e["actorId"]] = max(0,
                HEALTH[e["actorId"]] - e.get("damage", 0))
        elif e.get("eventType") == "DEAD":
            HEALTH.pop(e.get("actorId"), None)
            POSITIONS.pop(e.get("actorId"), None)


def reset_positions_to_turn(rows, turn_no):
    """Rebuild POSITIONS as they were at the START of turn_no, by walking
    every MOVE of earlier turns (so filtered --turns runs stay accurate)."""
    if rows is None:
        return
    first = next((r for r in rows if r.get("eventType") == "INFO"), {})
    POSITIONS.clear()
    seed_positions(first)
    reset_health_to_turn(rows, turn_no)  # DEAD events also pop positions
    for e in rows:
        if e.get("eventType") != "INFO" and e.get("turn", -1) >= turn_no:
            break
        if e.get("eventType") == "MOVE" and e.get("actorId") in POSITIONS \
                and e.get("moveTo"):
            POSITIONS[e["actorId"]] = dict(e["moveTo"])


# A wave hitting a ship is damage only (see snapshot_state); direction is
# kept for reference in answer keys.
WAVE_DIRECTIONS = {"~V": (0, -1), "~^": (0, 1), "~<": (1, 0), "~>": (-1, 0)}
FACTION_SYMBOLS = {"Green": "G", "Red": "R"}
WAVE_SYMBOLS = set("^V<>")
BLOCKING_SYMBOLS = set("&@GR")  # obstacles, targets, ships
MOVEMENT_BUDGET = 3             # orthogonal steps, shared with moveAfterAttack
ATTACK_RANGE = 2                # EXACT Manhattan distance (1 is a dead zone)


def faction_of(actor_id):
    parts = str(actor_id).split("|")
    if len(parts) < 2:
        return "Neutral"
    return parts[-2]


def wave_directions(info):
    out = {}
    for w in info.get("waveActorEntryJsons", []):
        out[w.get("name", "")] = WAVE_DIRECTIONS.get(w.get("direction", ""), (0, 0))
    return out


def find_actor_id(rows, info, hint):
    ids = {entry.get("name", "") for entry in info.get("navalActorEntryJsons", [])}
    for e in rows:
        if e.get("actorId"):
            ids.add(e["actorId"])
    hits = [i for i in ids if hint.lower() in str(i).lower()]
    if not hits:
        print(f"ERROR: no actor id matches '{hint}'.", file=sys.stderr)
        return None
    if len(hits) > 1:
        print(f"ERROR: '{hint}' is ambiguous between:", file=sys.stderr)
        for h in hits:
            print(f"  {h}", file=sys.stderr)
        return None
    return hits[0]


def snapshot_state(rows, info, turn_wanted, actor_id):
    """Walk the whole log up to (not including) the actor's first decision
    event in turn_wanted. Returns the state (grid, positions, hp, dead) at
    that decision point, or None if the actor never acts in that turn."""
    hp = {e.get("name", ""): e.get("currentHealth")
          for e in info.get("navalActorEntryJsons", [])}
    positions = dict(POSITIONS)
    grid = None
    dead = set()
    for e in rows:
        k = e.get("eventType")
        if k == "SCNR":
            grid = to_grid(e.get("scenarioOverview", []))
        elif k == "MOVE" and e.get("actorId") in positions:
            if grid:
                apply_move(grid, e.get("moveFrom"), e.get("moveTo"))
            positions[e["actorId"]] = dict(e["moveTo"])
        elif k == "DEAD":
            dead.add(e.get("actorId"))
            positions.pop(e.get("actorId"), None)
        elif k == "DAMG":
            if e.get("actorId") in hp:
                hp[e["actorId"]] = max(0, hp[e["actorId"]] - e.get("damage", 0))
        elif k == "PPGT":
            # wave hit: damage only (a DAMG event follows); the log shows
            # ships do NOT get displaced by waves
            pass
        is_decision = (k in ("RESN", "CMMD", "MOVE", "ATTK", "NVLD")
                       and e.get("turn") == turn_wanted
                       and e.get("actorId") == actor_id)
        if is_decision and grid is not None:
            return {"grid": [row[:] for row in grid],
                    "positions": {a: dict(p) for a, p in positions.items()},
                    "hp": dict(hp), "dead": dead,
                    "timeStamp": e.get("timeStamp")}
    return None


def enumerate_answer_key(grid, actor_pos, own_symbol, enemy_symbol):
    """BFS over reachable tiles (orthogonal steps, budget MOVEMENT_BUDGET,
    blockers BLOCKING_SYMBOLS, wave tiles passable). For every reachable
    tile list the attackable objects at EXACTLY Manhattan distance
    ATTACK_RANGE (distance 1 is a dead zone)."""
    height = len(grid)

    def sym(p):
        return grid[len(grid) - 1 - p["y"]][p["x"]]

    occupied = {}
    for y in range(height):
        for x, ch in enumerate(grid[len(grid) - 1 - y]):
            if ch in BLOCKING_SYMBOLS:
                occupied[(x, y)] = ch

    start = (actor_pos["x"], actor_pos["y"])
    if start in occupied and occupied[start] not in "GR":
        pass  # actor never stands on obstacles/targets, but ignore anyway
    blocked = set(k for k, ch in occupied.items()
                  if ch in BLOCKING_SYMBOLS and k != start)

    dist = {start: 0}
    frontier = [start]
    while frontier:
        nxt = []
        for x, y in frontier:
            if dist[(x, y)] >= MOVEMENT_BUDGET:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                n = (x + dx, y + dy)
                if n in dist or n in blocked:
                    continue
                nx, ny = n
                if not (0 <= nx < len(grid[0]) and 0 <= ny < height):
                    continue
                ch = grid[height - 1 - ny][nx]
                if ch == "#":
                    continue
                dist[n] = dist[(x, y)] + 1
                nxt.append(n)
        frontier = nxt

    attackable = {}
    for (x, y), ch in occupied.items():
        if (x, y) == start:
            continue  # the actor cannot attack itself
        kind = {enemy_symbol: "enemy", own_symbol: "ally", "@": "target",
                "&": "obstacle"}.get(ch)
        if kind:
            attackable[(x, y)] = kind

    moves = []
    for pos in sorted(dist, key=lambda p: (dist[p], p[0], p[1])):
        x, y = pos
        ch = grid[height - 1 - y][x]
        attacks = []
        for (tx, ty), kind in sorted(attackable.items()):
            if abs(x - tx) + abs(y - ty) == ATTACK_RANGE:
                attacks.append({"kind": kind, "position": [tx, ty]})
        moves.append({"position": [x, y], "steps": dist[pos],
                      "wave_tile": ch in WAVE_SYMBOLS,
                      "wave_direction": ch if ch in WAVE_SYMBOLS else None,
                      "attacks": attacks})
    return moves


def state_report(rows, info, turn_arg, hint, as_json):
    try:
        turn_wanted = int(turn_arg)
    except ValueError:
        print(f"ERROR: --state TURN must be an integer, got '{turn_arg}'.",
              file=sys.stderr)
        return 1
    actor_id = find_actor_id(rows, info, hint)
    if actor_id is None:
        return 1
    if faction_of(actor_id) == "Neutral":
        print(f"ERROR: --state is for ships; {actor_id} is not a controllable "
              "actor.", file=sys.stderr)
        return 1

    snap = snapshot_state(rows, info, turn_wanted, actor_id)
    if snap is None:
        print(f"ERROR: {actor_id} has no decision event in turn "
              f"{turn_wanted}.", file=sys.stderr)
        return 1

    actor_pos = snap["positions"].get(actor_id)
    if actor_pos is None:
        print(f"ERROR: lost track of {actor_id}'s position.", file=sys.stderr)
        return 1

    faction = faction_of(actor_id)
    own_symbol = FACTION_SYMBOLS.get(faction)
    enemy_symbol = FACTION_SYMBOLS.get("Red" if faction == "Green" else "Green")

    # sanity check: tracked ship positions must match the board tiles
    tracked_ships = {(p["x"], p["y"])
                     for name, p in snap["positions"].items()
                     if name not in snap["dead"]
                     and faction_of(name) in FACTION_SYMBOLS}
    board_ships = set()
    height = len(snap["grid"])
    for y in range(height):
        for x, ch in enumerate(snap["grid"][height - 1 - y]):
            if ch in FACTION_SYMBOLS.values():
                board_ships.add((x, y))
    if tracked_ships != board_ships:
        print(f"WARNING: position tracking drifted from the board "
              f"(tracked {sorted(tracked_ships)}, board shows "
              f"{sorted(board_ships)}); ship names/hp may be stale.",
              file=sys.stderr)

    # if the actor's tracked tile does not show its own symbol (unlogged
    # wave displacement), snap to the only unclaimed tile of its faction
    if own_symbol and tile(snap["grid"], actor_pos) not in (own_symbol, "*"):
        claimed = {(p["x"], p["y"])
                    for name, p in snap["positions"].items()
                    if name != actor_id and name not in snap["dead"]
                    and faction_of(name) in FACTION_SYMBOLS}
        candidates = sorted(board_ships - claimed)
        if len(candidates) == 1:
            old = (actor_pos["x"], actor_pos["y"])
            actor_pos = {"x": candidates[0][0], "y": candidates[0][1]}
            snap["positions"][actor_id] = dict(actor_pos)
            print(f"WARNING: unlogged displacement: {short_name(actor_id)} "
                  f"snapped from {old} to {candidates[0]} (fits the board).",
                  file=sys.stderr)
        else:
            print(f"ERROR: cannot locate {short_name(actor_id)} on the board "
                  f"(candidates: {candidates}).", file=sys.stderr)
            return 1

    # name ships that occupy tiles, for nicer attack labels
    tile_owner = {}
    for name, p in snap["positions"].items():
        if name not in snap["dead"] and faction_of(name) in FACTION_SYMBOLS:
            tile_owner[(p["x"], p["y"])] = name

    moves = enumerate_answer_key(snap["grid"], actor_pos, own_symbol,
                                enemy_symbol)
    for m in moves:
        for atk in m["attacks"]:
            owner = tile_owner.get(tuple(atk["position"]))
            atk["name"] = short_name(owner) if owner else (
                "obstacle" if atk["kind"] == "obstacle"
                else "target" if atk["kind"] == "target"
                else "unidentified ship")
            if owner:
                atk["hp"] = snap["hp"].get(owner)

    key = {"turn": turn_wanted, "actor": actor_id, "faction": faction,
           "position": [actor_pos["x"], actor_pos["y"]],
           "hp": snap["hp"].get(actor_id), "budget": MOVEMENT_BUDGET,
           "attack_range": ATTACK_RANGE, "moves": moves}

    if as_json:
        print(json.dumps(key, indent=2))
        return 0

    print("=" * 70)
    print(f"STATE: TURN {turn_wanted} - {short_name(actor_id)} at "
          f"({actor_pos['x']},{actor_pos['y']})   hp {key['hp']}   "
          f"(decision at t={snap['timeStamp']})")
    print("=" * 70)
    print("-" * 70)
    print(f"  decision board (* = {short_name(actor_id)})")
    print("-" * 70)
    g = [row[:] for row in snap["grid"]]
    set_tile(g, actor_pos, "*")
    for row in grid_to_rows(g):
        print("  " + row)
    print()
    print(f"  budget: {MOVEMENT_BUDGET} orthogonal steps total (movement + "
          f"moveAfterAttack)")
    print(f"  blockers: other ships, & obstacles, @ targets; wave tiles are "
          f"passable but the wave damages the ship every turn it is on or "
          f"next to the wave")
    print(f"  attacks: enemy ships, ally ships (friendly fire!), @ targets, "
          f"& obstacles at EXACTLY Manhattan distance "
          f"{ATTACK_RANGE} (distance 1 is a dead zone); obstacles waste "
          f"the attack")
    print(f"  waves: standing on the wave damages the ship; crossing the "
          f"wave lane is pushed back along the wave's direction ~90% of "
          f"the time (ending ON the wave tile lets the push extend "
          f"movement)")
    print()
    print("-" * 70)
    print("  MOVE + ATTACK ANSWER KEY")
    print("-" * 70)
    for m in moves:
        pos = m["position"]
        stay = " (stay)" if pos == [actor_pos["x"], actor_pos["y"]] else ""
        wave = (f"  [WAVE TILE {m.get('wave_direction')}: pushed along the "
                f"wave's direction ~90% (can extend movement; also wave "
                f"damage)]" if m["wave_tile"] else "")
        heads = ", ".join(
            f"{a['name']}" + (f" hp {a['hp']}" if "hp" in a else "")
            + f" at ({a['position'][0]},{a['position'][1]})"
            + (" (wasted shot)" if a["kind"] == "obstacle" else "")
            + (" (ALLY: friendly fire)" if a["kind"] == "ally" else "")
            for a in m["attacks"]) or "-- no attack possible"
        print(f"  ({pos[0]},{pos[1]}){stay}  {m['steps']}/3 steps: {heads}{wave}")
    print()
    total_attacks = sum(len(m["attacks"]) for m in moves)
    print(f"  {len(moves)} reachable tiles, {total_attacks} attack options")
    return 0


def main():
    args = parse_args()
    rows = load_log(args.logfile)
    if not rows:
        print(f"ERROR: no events found in {args.logfile}", file=sys.stderr)
        return 1

    info = next((r for r in rows if r.get("eventType") == "INFO"), {})
    seed_positions(info)

    if args.state:
        return state_report(rows, info, args.state[0], args.state[1],
                            as_json=args.json)
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
        printed += 1

        if args.actions:
            render_turn_actions(events, t, rows=rows)
            continue
        scnr = pick_scnr(events, args.last)

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
        # suppress the end-of-battle banner when only a subset of turns was
        # rendered, otherwise it looks like the game ended on that turn
        filtered = args.turns is not None or args.num_turns is not None
        if over and not (filtered and printed < len(order)):
            print("=" * 70)
            print(f"OVER: {over.get('winningFaction', '?')}   "
                  f"final {over.get('finalTime', '?')[:19]}")
            print("=" * 70)

    return 0


if __name__ == "__main__":
    sys.exit(main())