#!/usr/bin/env python3
"""
prompt_validator.py — parse Waves prompts and validate a model's JSON answer.

Shared module in the style of game_parser.py: imported by
ollama_prompt_test.py (--validate) and batch_prompt_test.py.

Supports both prompt generations:
  - current game format   (ATTACK NOW / MOVE + ATTACK PAIRS / SAFE MOVE TILES)
  - ENIAC-paper format    (Immediate Targets / Move + Attack / Safe Move-To)

Extracts from the prompt text:
  - own ship position          "Your ship ...; at (7,6)."
  - immediate attack targets   ATTACK NOW / Immediate Targets section
  - move -> attack pairs       "move (8, 6) → attacks (9, 7) ~V ..."
  - safe move tiles            SAFE MOVE TILES / Safe Move-To Positions
  - forbidden tiles            DO NOT MOVE / Risky positions

validate_answer(prompt_text, answer_text) returns
  (verdict, issues, answer_dict):
    verdict: "OK" | "INVALID_ACTION" | "INVALID_JSON" | "PROMPT_UNPARSED"
"""

import json
import re

COORD_RE    = re.compile(r"`?\(\s*(\d+)\s*,\s*(\d+)\s*\)`?")
SHIP_POS_RE = re.compile(r"Your [Ss]hip.*?at\s*\(`?(\d+)`?\s*,\s*`?(\d+)`?\)", re.S)
NO_MOVE     = (-1, -1)

# Section markers per prompt generation; first match per set wins.
ATTACK_NOW_MARKERS  = ["ATTACK NOW", "Immediate Targets"]
PAIRS_MARKERS       = ["MOVE + ATTACK PAIRS", "Move + Attack ("]
SAFE_MARKERS        = ["SAFE MOVE TILES", "Safe Move-To Positions"]
FORBIDDEN_MARKERS   = ["DO NOT MOVE", "Risky/Hazardous Positions"]
LATER_SECTIONS      = ["MOVEMENT BUDGET", "Movement Options", "STRATEGIC DECISION",
                       "RULES", "RESPONSE FORMAT", "Reply JSON only"]


def _coords(text):
    return [(int(m.group(1)), int(m.group(2))) for m in COORD_RE.finditer(text)]


def _section(text, start_markers, stop_markers):
    """Lines between the first matching start marker and the first stop
    marker / EOF. Supports several marker spellings per set."""
    lines = text.splitlines()
    out, capture = [], False
    for line in lines:
        if not capture:
            if any(m in line for m in start_markers):
                capture = True
            continue
        if any(m in line for m in stop_markers):
            break
        out.append(line)
    return out


def parse_prompt(prompt_text):
    """Extract the machine-checkable constraint sets from a Waves prompt."""
    p = {
        "ship_pos":   None,
        "attack_now": [],
        "pairs":      {},  # move tile -> list of attackable tiles
        "safe":       [],
        "forbidden":  [],
    }

    m = SHIP_POS_RE.search(prompt_text)
    if m:
        p["ship_pos"] = (int(m.group(1)), int(m.group(2)))

    for line in _section(prompt_text, ATTACK_NOW_MARKERS,
                         LATER_SECTIONS + PAIRS_MARKERS + SAFE_MARKERS
                         + FORBIDDEN_MARKERS):
        p["attack_now"].extend(_coords(line))

    for line in _section(prompt_text, PAIRS_MARKERS,
                         SAFE_MARKERS + FORBIDDEN_MARKERS + LATER_SECTIONS):
        m = re.match(r"\s*move\s*`?\(`?(\d+)`?`?,\s*`?(\d+)`?`?\)\s*→\s*(.+)", line)
        if not m:
            continue
        move_tile = (int(m.group(1)), int(m.group(2)))
        attacks = _coords(m.group(3))
        p["pairs"][move_tile] = p["pairs"].get(move_tile, []) + attacks

    for line in _section(prompt_text, SAFE_MARKERS,
                         PAIRS_MARKERS + FORBIDDEN_MARKERS + LATER_SECTIONS):
        p["safe"].extend(_coords(line))

    for line in _section(prompt_text, FORBIDDEN_MARKERS, LATER_SECTIONS):
        p["forbidden"].extend(_coords(line))

    return p


def extract_json(answer_text):
    """Pull the JSON object out of a model answer; None if unparseable."""
    if not answer_text:
        return None
    fence = re.search(r"```(?:json)?\s*(\{.*?\})\s*```", answer_text, re.S)
    if fence:
        answer_text = fence.group(1)
    else:
        brace = re.search(r"\{.*\}", answer_text, re.S)
        if brace:
            answer_text = brace.group(0)
    try:
        obj = json.loads(answer_text)
        return obj if isinstance(obj, dict) else None
    except (json.JSONDecodeError, ValueError):
        return None


def _as_tile(value):
    """Answer coords come as [x, y] lists (or sometimes strings)."""
    if value is None:
        return None
    try:
        tile = (int(value[0]), int(value[1]))
        return tile
    except (TypeError, ValueError, IndexError):
        return None


def validate_answer(prompt_text, answer_text):
    """Return (verdict, issues, answer_dict)."""
    parsed = parse_prompt(prompt_text)

    has_constraints = (parsed["ship_pos"] is not None or parsed["safe"]
                       or parsed["pairs"] or parsed["attack_now"])
    if not has_constraints:
        answer = extract_json(answer_text)
        if answer is None:
            return "INVALID_JSON", ["answer is not valid JSON"], None
        return ("PROMPT_UNPARSED",
                ["validator could not parse this prompt format "
                 "(no ship position / SAFE / PAIRS sections found)"],
                answer)

    answer = extract_json(answer_text)
    issues = []

    if answer is None:
        return "INVALID_JSON", ["answer is not valid JSON"], None

    for key in ("movement", "attack", "move_after_attack"):
        if key not in answer:
            issues.append(f"missing key '{key}'")

    movement_tile = _as_tile(answer.get("movement"))
    attack_tile   = _as_tile(answer.get("attack"))
    retreat_tile  = _as_tile(answer.get("move_after_attack"))

    for key, tile in (("movement", movement_tile),
                      ("attack", attack_tile),
                      ("move_after_attack", retreat_tile)):
        if tile is None and key not in issues:
            issues.append(f"'{key}' is not a [x, y] pair")

    if issues:
        return "INVALID_JSON", issues, answer

    legal_moves   = set(parsed["safe"]) | {parsed["ship_pos"], NO_MOVE}
    legal_retreat = set(parsed["safe"]) | {NO_MOVE}

    if movement_tile not in legal_moves:
        if movement_tile in set(parsed["forbidden"]):
            issues.append(f"movement {movement_tile} is on the DO NOT MOVE list")
        else:
            issues.append(f"movement {movement_tile} is not on the SAFE list")

    valid_attacks = set(parsed["pairs"].get(movement_tile, [])) | set(parsed["attack_now"])
    if attack_tile != NO_MOVE and attack_tile not in valid_attacks:
        issues.append(f"attack {attack_tile} is not a valid target from {movement_tile}")

    if retreat_tile not in legal_retreat:
        issues.append(f"move_after_attack {retreat_tile} is not on the SAFE list")

    return ("OK" if not issues else "INVALID_ACTION"), issues, answer