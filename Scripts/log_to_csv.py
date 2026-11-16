#!/usr/bin/env python3
"""
log_to_csv.py — convert a Battleground battle log into R-friendly CSVs.

Two modes:
  • Single-file: python log_to_csv.py battle.txt out/
  • Library:     import extract_battle; used by batch_export_csvs.py

Every CSV is keyed on battle_id + source_file so multiple logs can be
appended into master CSVs without losing provenance.

Schema constants (BATTLE_FIELDS, ACTOR_FIELDS, etc.) are the SINGLE
SOURCE OF TRUTH for column order. If a row dict contains a key not in
the corresponding fieldnames list, that column is silently dropped by
csv.DictWriter(extrasaction="ignore"). If you add a column, add it here.
"""

import argparse
import csv
import json
import os
import re
import statistics
import sys
from collections import Counter, defaultdict
from datetime import datetime
from pathlib import Path

# ---------------------------------------------------------------------------
# Schema — single source of truth
# ---------------------------------------------------------------------------

BATTLE_FIELDS = [
    "battle_id", "source_file",
    "map", "randomSeed", "maxTurns", "startTime", "finalTime",
    "duration_s", "extraText", "player_id", "session_tag",
    "winner_raw", "winner_faction", "goalMessage", "final_turn",
    "llm_side", "llm_won",
    "llm_opponent_kind", "llm_opponent_factions",
    "n_llm", "n_au", "n_human",
    "n_red_initial_board", "n_green_initial_board",
    "n_neutral_initial_board", "n_waves_initial_board",
    "min_dist_RG_initial",
    "n_resn", "n_attk", "n_damg", "n_dead", "n_nvld", "n_events_total",
]

ACTOR_FIELDS = [
    "battle_id", "source_file",
    "name", "kind", "display_name", "actor_index", "faction",
    "opponent_kind", "opponent_factions",
    "ship_prefab", "ship_data",
    "llm_type", "llm_model", "llm_prompt",
    "machine_brain", "gene_short_name", "gene_name",
    "initial_hp", "final_hp",
    "initial_x", "initial_y", "final_x", "final_y",
    "moves", "tiles_walked", "commands",
    "attacks_total", "attacks_on_enemy", "attacks_on_ally",
    "attacks_on_neutral", "attacks_on_wave", "attacks_on_self",
    "damage_dealt_attk", "damage_taken",
    "kills", "deaths", "moves_failed", "attacks_failed",
    "distinct_targets", "top_target_share",
    "reasoning_count", "reasoning_total_words", "reasoning_mean_words",
    "response_count", "response_mean_ms", "response_median_ms",
    "response_min_ms", "response_max_ms", "response_stdev_ms",
]

ACTION_FIELDS = [
    "battle_id", "source_file",
    "turn", "timeStamp", "actor_id",
    "kind", "display_name", "actor_index", "faction",
    "llm_type", "llm_model", "llm_prompt",
    "machine_brain", "gene_short_name", "gene_name",
    "event_type",
    "mov_x", "mov_y", "atk_x", "atk_y", "after_x", "after_y",
    "move_from_x", "move_from_y", "move_to_x", "move_to_y", "move_dist",
    "target_id", "target_kind", "attack_x", "attack_y", "damage",
    "nvld_type", "comment",
    "target_pos_x", "target_pos_y", "position_x", "position_y",
    "killed_by_actor_id", "killed_by_faction", "killed_by_turn",
]

BOARD_FIELDS = [
    "battle_id", "source_file",
    "turn", "timeStamp",
    "n_red", "n_green", "n_neutral", "n_obstacle",
    "n_wave_up", "n_wave_down", "min_dist_RG",
    "red_positions", "green_positions", "board_ascii",
]

REASONING_TERMS = [
    "attack", "move", "retreat", "kill", "target",
    "rule 1", "rule 2", "rule 3",
    "safe", "unsafe", "danger", "enemy", "ally", "friend",
    "neutral", "wave", "hit", "damage", "hp", "health",
    "distance", "tile", "range", "reach", "line",
]


def turn_fieldnames(all_factions):
    """Column list for turns.csv, with per-faction activity columns."""
    base = [
        "battle_id", "source_file",
        "turn", "realTimestamp", "battleTimestamp",
        "wallclock_duration_s",
        "n_resn", "n_attk", "n_move", "n_cmmd", "n_dead", "n_nvld",
        "board_red", "board_green", "board_neutral",
        "board_wave_up", "board_wave_down", "board_min_dist_RG",
    ]
    for fac in all_factions:
        lc = fac.lower()
        base.extend([f"n_attk_{lc}", f"n_move_{lc}",
                     f"n_resn_{lc}", f"total_hp_{lc}"])
    return base


def reasoning_fieldnames():
    term_cols = [f"n_{t.replace(' ', '_')}" for t in REASONING_TERMS]
    return (
        ["battle_id", "source_file",
         "turn", "timeStamp", "actor_id",
         "kind", "display_name", "actor_index", "faction",
         "opponent_kind",
         "llm_type", "llm_model", "llm_prompt",
         "machine_brain", "gene_short_name", "gene_name",
         "responseTime_ms",
         "reasoning_words", "reasoning_chars", "n_coordinate_refs"]
        + term_cols
        + ["reasoning_text"]
    )


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def load_events(path):
    out = []
    with open(path, "r", encoding="utf-8-sig") as f:
        for n, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                out.append(json.loads(line))
            except json.JSONDecodeError as e:
                print(f"[warn] line {n}: {e}", file=sys.stderr)
    return out


def parse_iso_utc(s):
    if not s:
        return None
    try:
        return datetime.fromisoformat(s)
    except ValueError:
        try:
            return datetime.fromisoformat(s.replace("Z", "+00:00"))
        except ValueError:
            return None


def kind_from_name(name):
    if not name:
        return "Unknown"
    if name.startswith("Target"):
        return "Target"
    if name.startswith("Wave"):
        return "Wave"
    if name.startswith("LLM|"):
        return "LLM"
    if name.startswith("AU|"):
        return "AU"
    if "|" in name:
        return "Human"
    return "Unknown"


def write_csv(path, fieldnames, rows, append=False):
    """Write rows. extrasaction='ignore' means any key not in fieldnames
    is silently dropped — see module docstring."""
    file_exists = os.path.exists(path) and os.path.getsize(path) > 0
    if append and file_exists:
        with open(path, newline="", encoding="utf-8") as f:
            existing_header = next(csv.reader(f), None)
        if existing_header != list(fieldnames):
            raise ValueError(
                f"Cannot append to {path}: header mismatch.\n"
                f"  Expected: {list(fieldnames)}\n"
                f"  Found:    {existing_header}\n"
                f"Delete the file or run without --append."
            )
        mode, write_header = "a", False
    else:
        mode, write_header = "w", True

    with open(path, mode, newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fieldnames, extrasaction="ignore")
        if write_header:
            w.writeheader()
        for r in rows:
            w.writerow(r)


def parse_actor_id(actor_id):
    if not actor_id:
        return {"kind": None, "display_name": None}
    parts = actor_id.split("|")
    kind = parts[0] if parts else None
    out = {"kind": kind}
    if kind == "LLM" and len(parts) >= 6:
        out.update({"llm_type": parts[1], "llm_model": parts[2],
                    "display_name": parts[3], "actor_index": parts[5]})
    elif kind == "AU" and len(parts) >= 5:
        out.update({"display_name": parts[1], "gene_short_name": parts[2],
                    "actor_index": parts[4]})
    elif kind == "Human" and len(parts) >= 4:
        out.update({"display_name": parts[1], "actor_index": parts[3]})
    else:
        out["display_name"] = "|".join(parts[1:]) if len(parts) > 1 else actor_id
    return out


def parse_extra_text(raw):
    if not raw:
        return (None, None)
    m = re.match(r"^(?P<player>[^\[]+)\[(?P<session>[^\]]+)\]", raw.strip())
    if m:
        return (m.group("player").strip(), m.group("session").strip())
    return (raw.strip(), None)


def opponent_info(faction, actor_meta):
    if not faction:
        return (None, [])
    opp_factions = {
        m["faction"] for m in actor_meta.values()
        if m.get("faction") and m["faction"] != faction
        and m.get("kind") not in ("Target", "Wave", "Unknown", None)
    }
    if not opp_factions:
        return (None, [])
    kinds = {m["kind"] for m in actor_meta.values()
             if m.get("faction") in opp_factions
             and m.get("kind") not in ("Target", "Wave", "Unknown", None)}
    kind = None if not kinds else (next(iter(kinds)) if len(kinds) == 1 else "Mixed")
    return (kind, sorted(opp_factions))


def count_terms(text):
    if not text:
        return {f"n_{t.replace(' ', '_')}": 0 for t in REASONING_TERMS}
    low = text.lower()
    out = {}
    for t in REASONING_TERMS:
        key = f"n_{t.replace(' ', '_')}"
        if " " in t:
            out[key] = low.count(t)
        else:
            out[key] = len(re.findall(rf"\b{re.escape(t)}\b", low))
    return out


def parse_board(ascii_rows):
    if not ascii_rows:
        return {}
    pos = defaultdict(list)
    for y, row in enumerate(ascii_rows):
        for x, ch in enumerate(row):
            if ch not in (".", "#"):
                pos[ch].append((x, y))
    R, G = pos.get("R", []), pos.get("G", [])
    min_rg = None
    for rx, ry in R:
        for gx, gy in G:
            d = abs(rx - gx) + abs(ry - gy)
            if min_rg is None or d < min_rg:
                min_rg = d
    return {
        "n_red": len(R), "n_green": len(G),
        "n_neutral": len(pos.get("@", [])),
        "n_obstacle": len(pos.get("&", [])),
        "n_wave_up": len(pos.get("^", [])),
        "n_wave_down": len(pos.get("V", [])),
        "min_dist_RG": min_rg,
        "red_positions": " ".join(f"{x},{y}" for x, y in R),
        "green_positions": " ".join(f"{x},{y}" for x, y in G),
    }


# ---------------------------------------------------------------------------
# Core: extract one battle into row-lists
# ---------------------------------------------------------------------------

def extract_battle(logfile, battle_id=None, all_factions=None,
                   keep_non_llm_reasonings=False):
    """
    Parse a single battle log; return dict of row-lists + meta.

    battle_id:    stable unique key for this battle. Defaults to the
                  log filename without its extension.
    all_factions: list of faction names to emit columns for in turns.csv.
                  If None, uses factions discovered in this log (single-
                  file mode). Batch mode passes the union across all logs
                  so every turns.csv row has the same schema.
    """
    source_file = os.path.basename(logfile)
    if battle_id is None:
        battle_id = Path(logfile).stem
    battle_key = {"battle_id": battle_id, "source_file": source_file}

    events = load_events(logfile)
    if not events:
        raise ValueError(f"No events in {logfile}")

    battle_info, actor_meta = {}, {}
    turns_raw, scnr_raw, gstt_raw = [], [], []
    resn_recs, attack_recs, damage_recs, death_recs, nvld_recs = [], [], [], [], []
    action_recs, last_attacker = [], {}

    for ev in events:
        et = ev.get("eventType")
        turn, ts = ev.get("turn"), ev.get("timeStamp")
        aid, fac = ev.get("actorId"), ev.get("faction")

        if et == "INFO":
            battle_info = {k: ev.get(k) for k in
                           ("map", "randomSeed", "maxTurns", "startTime", "extraText")}
            for a in ev.get("navalActorEntryJsons") or []:
                name = a.get("name") or ""
                genes = a.get("genesData") or {}
                actor_meta[name] = {
                    "name": name, "kind": kind_from_name(name),
                    "faction": a.get("faction"),
                    "ship_prefab": a.get("shipPrefabType"),
                    "ship_data": a.get("shipData"),
                    "llm_type": a.get("llmType"), "llm_model": a.get("llmModel"),
                    "llm_prompt": a.get("basePrompt"),
                    "machine_brain": a.get("machineBrain"),
                    "gene_short_name": genes.get("shortName"),
                    "gene_name": genes.get("geneName"),
                    "initial_hp": a.get("currentHealth"),
                    "initial_x": (a.get("position") or {}).get("x"),
                    "initial_y": (a.get("position") or {}).get("y"),
                }
        elif et == "SCNR":
            scnr_raw.append({"turn": turn, "timeStamp": ts,
                             "overview": ev.get("scenarioOverview") or []})
        elif et == "TURN":
            turns_raw.append({"turn": turn,
                              "realTimestamp": ev.get("realTimestamp"),
                              "battleTimestamp": ts})
        elif et == "OVER":
            battle_info["finalTime"] = ev.get("finalTime")
            battle_info["winningFaction"] = ev.get("winningFaction")
            battle_info["goalMessage"] = ev.get("goalMessage")
            battle_info["over_turn"] = turn
        elif et == "GSTT":
            snap = {}
            for a in ev.get("navalActorEntryJsons") or []:
                n = a.get("name")
                if not n:
                    continue
                snap[n] = {"hp": a.get("currentHealth"),
                           "x": (a.get("position") or {}).get("x"),
                           "y": (a.get("position") or {}).get("y"),
                           "faction": a.get("faction")}
            gstt_raw.append({"turn": turn, "snapshot": snap})
        elif et == "RESN":
            resn_recs.append({"turn": turn, "timeStamp": ts, "actor_id": aid,
                              "faction": fac, "responseTime": ev.get("responseTime"),
                              "reasoning": ev.get("reasoning") or ""})
        elif et == "CMMD":
            action_recs.append({
                "turn": turn, "timeStamp": ts, "actor_id": aid, "faction": fac,
                "event_type": "CMMD",
                "mov_x": (ev.get("movement") or {}).get("x"),
                "mov_y": (ev.get("movement") or {}).get("y"),
                "atk_x": (ev.get("attack") or {}).get("x"),
                "atk_y": (ev.get("attack") or {}).get("y"),
                "after_x": (ev.get("moveAfterAttack") or {}).get("x"),
                "after_y": (ev.get("moveAfterAttack") or {}).get("y")})
        elif et == "MOVE":
            mf = ev.get("moveFrom") or {}
            mt = ev.get("moveTo") or {}
            d = (abs(mt["x"] - mf["x"]) + abs(mt["y"] - mf["y"])
                 if "x" in mf and "x" in mt else None)
            action_recs.append({
                "turn": turn, "timeStamp": ts, "actor_id": aid, "faction": fac,
                "event_type": "MOVE",
                "move_from_x": mf.get("x"), "move_from_y": mf.get("y"),
                "move_to_x": mt.get("x"), "move_to_y": mt.get("y"),
                "move_dist": d})
        elif et == "ATTK":
            tid = ev.get("targetId")
            rec = {"turn": turn, "timeStamp": ts, "actor_id": aid, "faction": fac,
                   "event_type": "ATTK", "target_id": tid,
                   "attack_x": (ev.get("attackPosition") or {}).get("x"),
                   "attack_y": (ev.get("attackPosition") or {}).get("y"),
                   "damage": ev.get("damage")}
            action_recs.append(rec)
            attack_recs.append(rec)
            if tid:
                last_attacker[tid] = (aid, fac, turn)
        elif et == "DAMG":
            rec = {"turn": turn, "timeStamp": ts, "actor_id": aid, "faction": fac,
                   "event_type": "DAMG", "damage": ev.get("damage")}
            action_recs.append(rec)
            damage_recs.append(rec)
        elif et == "DEAD":
            k = last_attacker.get(aid)
            rec = {"turn": turn, "timeStamp": ts, "actor_id": aid, "faction": fac,
                   "event_type": "DEAD",
                   "killed_by_actor_id": k[0] if k else None,
                   "killed_by_faction": k[1] if k else None,
                   "killed_by_turn": k[2] if k else None}
            action_recs.append(rec)
            death_recs.append(rec)
        elif et == "NVLD":
            rec = {"turn": turn, "timeStamp": ts, "actor_id": aid, "faction": fac,
                   "event_type": "NVLD", "nvld_type": ev.get("type"),
                   "comment": ev.get("comment") or "",
                   "target_id": ev.get("targetId") or "",
                   "target_pos_x": (ev.get("targetPosition") or {}).get("x"),
                   "target_pos_y": (ev.get("targetPosition") or {}).get("y"),
                   "position_x": (ev.get("position") or {}).get("x"),
                   "position_y": (ev.get("position") or {}).get("y")}
            action_recs.append(rec)
            nvld_recs.append(rec)

    actor_identity = {name: parse_actor_id(name) for name in actor_meta}

    def identity_for(aid):
        ident = actor_identity.get(aid, {})
        meta = actor_meta.get(aid, {})
        return {
            "kind": ident.get("kind") or meta.get("kind"),
            "display_name": ident.get("display_name"),
            "actor_index": ident.get("actor_index"),
            "llm_type": ident.get("llm_type") or meta.get("llm_type"),
            "llm_model": ident.get("llm_model") or meta.get("llm_model"),
            "llm_prompt": meta.get("llm_prompt"),
            "machine_brain": meta.get("machine_brain"),
            "gene_short_name": ident.get("gene_short_name") or meta.get("gene_short_name"),
            "gene_name": meta.get("gene_name"),
        }

    if all_factions is None:
        all_factions = sorted({
            a["faction"] for a in actor_meta.values() if a.get("faction") is not None
        })

    # ---- battle row ----
    start_dt = parse_iso_utc(battle_info.get("startTime"))
    end_dt = parse_iso_utc(battle_info.get("finalTime"))
    dur_s = (end_dt - start_dt).total_seconds() if start_dt and end_dt else None
    first_board = parse_board(scnr_raw[0]["overview"]) if scnr_raw else {}
    winner_raw = (battle_info.get("winningFaction") or "").strip()
    winner_fac = winner_raw.rsplit(" is ", 1)[1].rstrip("!").strip() if " is " in winner_raw else None
    llm_side = next((a["faction"] for a in actor_meta.values() if a["kind"] == "LLM"), None)
    player_id, session_tag = parse_extra_text(battle_info.get("extraText"))
    llm_opp_kind, llm_opp_facs = opponent_info(llm_side, actor_meta)
    kind_totals = Counter(a["kind"] for a in actor_meta.values())

    battle_row = {**battle_key,
        "map": battle_info.get("map"), "randomSeed": battle_info.get("randomSeed"),
        "maxTurns": battle_info.get("maxTurns"),
        "startTime": battle_info.get("startTime"),
        "finalTime": battle_info.get("finalTime"),
        "duration_s": round(dur_s, 3) if dur_s is not None else None,
        "extraText": battle_info.get("extraText"),
        "player_id": player_id, "session_tag": session_tag,
        "winner_raw": winner_raw, "winner_faction": winner_fac,
        "goalMessage": battle_info.get("goalMessage"),
        "final_turn": battle_info.get("over_turn"),
        "llm_side": llm_side,
        "llm_won": (llm_side is not None and winner_fac is not None and llm_side == winner_fac),
        "llm_opponent_kind": llm_opp_kind,
        "llm_opponent_factions": "|".join(llm_opp_facs) if llm_opp_facs else None,
        "n_llm": kind_totals.get("LLM", 0),
        "n_au": kind_totals.get("AU", 0),
        "n_human": kind_totals.get("Human", 0),
        "n_red_initial_board": first_board.get("n_red"),
        "n_green_initial_board": first_board.get("n_green"),
        "n_neutral_initial_board": first_board.get("n_neutral"),
        "n_waves_initial_board": (first_board.get("n_wave_up", 0) + first_board.get("n_wave_down", 0)),
        "min_dist_RG_initial": first_board.get("min_dist_RG"),
        "n_resn": len(resn_recs), "n_attk": len(attack_recs),
        "n_damg": len(damage_recs), "n_dead": len(death_recs),
        "n_nvld": len(nvld_recs), "n_events_total": len(events),
    }

    # ---- actor rows ----
    per = {n: {"moves": 0, "tiles_walked": 0, "commands": 0,
               "attacks_total": 0, "attacks_on_enemy": 0, "attacks_on_ally": 0,
               "attacks_on_neutral": 0, "attacks_on_wave": 0, "attacks_on_self": 0,
               "damage_dealt_attk": 0.0, "damage_taken": 0.0,
               "kills": 0, "deaths": 0, "moves_failed": 0, "attacks_failed": 0,
               "response_times": [], "reasonings": [], "attack_targets": Counter()}
           for n in actor_meta}

    for rec in action_recs:
        aid = rec.get("actor_id")
        if aid not in per:
            continue
        s = per[aid]
        et = rec["event_type"]
        if et == "MOVE":
            s["moves"] += 1
            if rec.get("move_dist") is not None:
                s["tiles_walked"] += rec["move_dist"]
        elif et == "CMMD":
            s["commands"] += 1
        elif et == "ATTK":
            s["attacks_total"] += 1
            s["damage_dealt_attk"] += rec.get("damage") or 0
            tid = rec.get("target_id")
            if tid:
                s["attack_targets"][tid] += 1
                if tid == aid: s["attacks_on_self"] += 1
                elif tid.startswith("Target"): s["attacks_on_neutral"] += 1
                elif tid.startswith("Wave"): s["attacks_on_wave"] += 1
                else:
                    tgt = actor_meta.get(tid)
                    if tgt and tgt["faction"] == actor_meta[aid]["faction"]:
                        s["attacks_on_ally"] += 1
                    else:
                        s["attacks_on_enemy"] += 1
        elif et == "DEAD":
            s["deaths"] += 1

    for rec in death_recs:
        k = rec.get("killed_by_actor_id")
        if k and k in per: per[k]["kills"] += 1
    for rec in damage_recs:
        aid = rec.get("actor_id")
        if aid in per: per[aid]["damage_taken"] += rec.get("damage") or 0
    for rec in nvld_recs:
        aid = rec.get("actor_id")
        if aid in per:
            if rec.get("nvld_type") == "FailedToMove": per[aid]["moves_failed"] += 1
            elif rec.get("nvld_type") == "OutOfReach": per[aid]["attacks_failed"] += 1
    for rec in resn_recs:
        aid = rec.get("actor_id")
        if aid in per:
            if rec.get("responseTime") is not None:
                per[aid]["response_times"].append(rec["responseTime"])
            per[aid]["reasonings"].append(rec.get("reasoning") or "")

    final_state = gstt_raw[-1]["snapshot"] if gstt_raw else {}
    actor_rows = []
    for name, m in actor_meta.items():
        s = per[name]
        ident = identity_for(name)
        opp_kind, opp_facs = opponent_info(m["faction"], actor_meta)
        rt = s["response_times"]
        rwords = [len(r.split()) for r in s["reasonings"]]
        top = s["attack_targets"]
        tot = sum(top.values())
        top_share = top.most_common(1)[0][1] / tot if tot else 0.0
        fin = final_state.get(name, {})
        actor_rows.append({**battle_key,
            "name": name, "kind": ident["kind"],
            "display_name": ident["display_name"], "actor_index": ident["actor_index"],
            "faction": m["faction"],
            "opponent_kind": opp_kind,
            "opponent_factions": "|".join(opp_facs) if opp_facs else None,
            "ship_prefab": m["ship_prefab"], "ship_data": m["ship_data"],
            "llm_type": ident["llm_type"], "llm_model": ident["llm_model"],
            "llm_prompt": ident["llm_prompt"], "machine_brain": ident["machine_brain"],
            "gene_short_name": ident["gene_short_name"], "gene_name": ident["gene_name"],
            "initial_hp": m["initial_hp"], "final_hp": fin.get("hp"),
            "initial_x": m["initial_x"], "initial_y": m["initial_y"],
            "final_x": fin.get("x"), "final_y": fin.get("y"),
            "moves": s["moves"], "tiles_walked": s["tiles_walked"],
            "commands": s["commands"],
            "attacks_total": s["attacks_total"],
            "attacks_on_enemy": s["attacks_on_enemy"],
            "attacks_on_ally": s["attacks_on_ally"],
            "attacks_on_neutral": s["attacks_on_neutral"],
            "attacks_on_wave": s["attacks_on_wave"],
            "attacks_on_self": s["attacks_on_self"],
            "damage_dealt_attk": round(s["damage_dealt_attk"], 2),
            "damage_taken": round(s["damage_taken"], 2),
            "kills": s["kills"], "deaths": s["deaths"],
            "moves_failed": s["moves_failed"], "attacks_failed": s["attacks_failed"],
            "distinct_targets": len(top), "top_target_share": round(top_share, 3),
            "reasoning_count": len(s["reasonings"]),
            "reasoning_total_words": sum(rwords),
            "reasoning_mean_words": round(statistics.fmean(rwords), 1) if rwords else 0,
            "response_count": len(rt),
            "response_mean_ms": round(statistics.fmean(rt), 2) if rt else None,
            "response_median_ms": round(statistics.median(rt), 2) if rt else None,
            "response_min_ms": min(rt) if rt else None,
            "response_max_ms": max(rt) if rt else None,
            "response_stdev_ms": round(statistics.pstdev(rt), 2) if len(rt) > 1 else None,
        })

    # ---- turn rows ----
    turns_sorted = sorted(turns_raw, key=lambda t: t["turn"])
    durations = {}
    for i, t in enumerate(turns_sorted):
        cur = parse_iso_utc(t["realTimestamp"])
        nxt = (parse_iso_utc(turns_sorted[i+1]["realTimestamp"])
               if i+1 < len(turns_sorted) else parse_iso_utc(battle_info.get("finalTime")))
        if cur and nxt:
            durations[t["turn"]] = round((nxt - cur).total_seconds(), 3)
    boards_by_turn = {}
    for s in scnr_raw:
        boards_by_turn.setdefault(s["turn"], s)
    gstt_by_turn = {g["turn"]: g["snapshot"] for g in gstt_raw}

    turn_rows = []
    for t in turns_sorted:
        tn = t["turn"]
        board = boards_by_turn.get(tn)
        bm = parse_board(board["overview"]) if board else {}
        snap = gstt_by_turn.get(tn) or {}
        row = {**battle_key,
            "turn": tn, "realTimestamp": t["realTimestamp"],
            "battleTimestamp": t["battleTimestamp"],
            "wallclock_duration_s": durations.get(tn),
            "n_resn": sum(1 for r in resn_recs if r["turn"] == tn),
            "n_attk": sum(1 for r in attack_recs if r["turn"] == tn),
            "n_move": sum(1 for r in action_recs
                          if r["event_type"] == "MOVE" and r["turn"] == tn),
            "n_cmmd": sum(1 for r in action_recs
                          if r["event_type"] == "CMMD" and r["turn"] == tn),
            "n_dead": sum(1 for r in death_recs if r["turn"] == tn),
            "n_nvld": sum(1 for r in nvld_recs if r["turn"] == tn),
            "board_red": bm.get("n_red"), "board_green": bm.get("n_green"),
            "board_neutral": bm.get("n_neutral"),
            "board_wave_up": bm.get("n_wave_up"),
            "board_wave_down": bm.get("n_wave_down"),
            "board_min_dist_RG": bm.get("min_dist_RG")}
        for fac in all_factions:
            lc = fac.lower()
            row[f"n_attk_{lc}"] = sum(1 for r in attack_recs
                                      if r["turn"] == tn and r.get("faction") == fac)
            row[f"n_move_{lc}"] = sum(1 for r in action_recs
                                      if r["event_type"] == "MOVE"
                                      and r["turn"] == tn and r.get("faction") == fac)
            row[f"n_resn_{lc}"] = sum(1 for r in resn_recs
                                      if r["turn"] == tn and r.get("faction") == fac)
            row[f"total_hp_{lc}"] = (sum(v["hp"] or 0 for v in snap.values()
                                         if v.get("faction") == fac)
                                     if snap else None)
        turn_rows.append(row)

    # ---- action rows ----
    def enrich_action(rec):
        ident = identity_for(rec.get("actor_id"))
        tid = rec.get("target_id") or ""
        return {**rec,
            "kind": ident["kind"], "display_name": ident["display_name"],
            "actor_index": ident["actor_index"],
            "llm_type": ident["llm_type"], "llm_model": ident["llm_model"],
            "llm_prompt": ident["llm_prompt"], "machine_brain": ident["machine_brain"],
            "gene_short_name": ident["gene_short_name"], "gene_name": ident["gene_name"],
            "target_kind": kind_from_name(tid) if tid else None}
    action_recs.sort(key=lambda r: (
        r.get("turn") if r.get("turn") is not None else -1,
        r.get("timeStamp") if r.get("timeStamp") is not None else 0))
    action_rows = [{**battle_key, **enrich_action(r)} for r in action_recs]

    # ---- reasoning rows (LLM only by default) ----
    reasoning_rows = []
    skipped_non_llm = 0
    for r in resn_recs:
        ident = identity_for(r["actor_id"])
        if ident["kind"] != "LLM" and not keep_non_llm_reasonings:
            skipped_non_llm += 1
            continue
        opp_kind, _ = opponent_info(r["faction"], actor_meta)
        reasoning_rows.append({**battle_key,
            "turn": r["turn"], "timeStamp": r["timeStamp"],
            "actor_id": r["actor_id"], "kind": ident["kind"],
            "display_name": ident["display_name"], "actor_index": ident["actor_index"],
            "faction": r["faction"], "opponent_kind": opp_kind,
            "llm_type": ident["llm_type"], "llm_model": ident["llm_model"],
            "llm_prompt": ident["llm_prompt"], "machine_brain": ident["machine_brain"],
            "gene_short_name": ident["gene_short_name"], "gene_name": ident["gene_name"],
            "responseTime_ms": r["responseTime"],
            "reasoning_words": len(r["reasoning"].split()),
            "reasoning_chars": len(r["reasoning"]),
            "n_coordinate_refs": len(re.findall(r"\(\s*-?\d+\s*,\s*-?\d+\s*\)", r["reasoning"])),
            **count_terms(r["reasoning"]),
            "reasoning_text": r["reasoning"]})

    # ---- board snapshot rows ----
    board_rows = []
    for s in scnr_raw:
        bm = parse_board(s["overview"])
        board_rows.append({**battle_key,
            "turn": s["turn"], "timeStamp": s["timeStamp"],
            **{k: bm.get(k) for k in ("n_red", "n_green", "n_neutral",
                                       "n_obstacle", "n_wave_up", "n_wave_down",
                                       "min_dist_RG")},
            "red_positions": bm.get("red_positions"),
            "green_positions": bm.get("green_positions"),
            "board_ascii": "|".join(s["overview"])})

    return {
        "battle": [battle_row],
        "actors": actor_rows,
        "turns": turn_rows,
        "actions": action_rows,
        "reasonings": reasoning_rows,
        "board_snapshots": board_rows,
        "meta": {"battle_id": battle_id, "source_file": source_file,
                 "n_events": len(events), "skipped_non_llm": skipped_non_llm,
                 "all_factions": all_factions},
    }


# ---------------------------------------------------------------------------
# Single-file writer + CLI
# ---------------------------------------------------------------------------

def write_battle_csvs(result, outdir, prefix=""):
    os.makedirs(outdir, exist_ok=True)
    factions = result["meta"]["all_factions"]
    files = [
        ("battle.csv", BATTLE_FIELDS, result["battle"]),
        ("actors.csv", ACTOR_FIELDS, result["actors"]),
        ("turns.csv", turn_fieldnames(factions), result["turns"]),
        ("actions.csv", ACTION_FIELDS, result["actions"]),
        ("reasonings.csv", reasoning_fieldnames(), result["reasonings"]),
        ("board_snapshots.csv", BOARD_FIELDS, result["board_snapshots"]),
    ]
    for fname, fields, rows in files:
        if not rows:
            continue
        write_csv(os.path.join(outdir, f"{prefix}{fname}"), fields, rows)
    return {fname: len(rows) for fname, _, rows in files}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("logfile")
    ap.add_argument("outdir")
    ap.add_argument("--prefix", default="")
    ap.add_argument("--battle-id", default=None,
                    help="Stable ID. Defaults to filename stem.")
    ap.add_argument("--keep-non-llm-reasonings", action="store_true")
    args = ap.parse_args()

    try:
        res = extract_battle(args.logfile, battle_id=args.battle_id,
                             keep_non_llm_reasonings=args.keep_non_llm_reasonings)
    except Exception as e:
        print(f"ERROR: {e}", file=sys.stderr)
        return 1

    counts = write_battle_csvs(res, args.outdir, prefix=args.prefix)
    print(f"Wrote to {args.outdir}  (battle_id={res['meta']['battle_id']}):")
    for name, n in counts.items():
        print(f"  {name:<24} ({n} rows)")
    if res["meta"]["skipped_non_llm"]:
        print(f"  [skipped {res['meta']['skipped_non_llm']} non-LLM reasonings]")
    return 0


if __name__ == "__main__":
    sys.exit(main())