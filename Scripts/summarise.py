#!/usr/bin/env python3
"""
Console summariser for compiled battle JSONL files.

Usage:
    python summarise.py battles.jsonl
    python summarise.py battles.jsonl --map BaseBattlegroundScene
    python summarise.py battles.jsonl --model phi3
    python summarise.py battles.jsonl --view wins
"""

import argparse
import json
import math
import sys
import statistics
from collections import defaultdict
from datetime import datetime

# ---------------------------------------------------------------------------
# Loading
# ---------------------------------------------------------------------------

def load_battles(path):
    battles = []
    with open(path, "r", encoding="utf-8-sig") as f:
        for line_no, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                battles.append(json.loads(line))
            except json.JSONDecodeError as exc:
                print(f"[warn] skipping line {line_no}: {exc}", file=sys.stderr)
    return battles


# ---------------------------------------------------------------------------
# Flattening
# ---------------------------------------------------------------------------

def human_records(battles):
    """One record per battle with a Human faction, from the Human's POV."""
    out = []
    for b in battles:
        s = b.get("summary", {})
        roles = s.get("faction_roles", {}) or {}
        human_side = next(
            (f for f, r in roles.items() if r == "Human"), None
        )
        if not human_side:
            continue

        opponent_side = next((f for f in roles if f != human_side), None)
        winner_side = s.get("winner_side")
        is_draw = bool(s.get("is_draw"))
        won = bool(winner_side and winner_side == human_side)

        rs = (s.get("decision_stats_by_faction", {}) or {}).get(human_side, {}) or {}
        invalid_dict = (s.get("invalid_by_faction", {}) or {}).get(human_side, {}) or {}
        invalid_total = sum(invalid_dict.values())

        # --- quality metrics ---
        my_side = human_side  # or llm_side in llm_records
        enemy = opponent_side
        fleet = s.get("fleet_sizes") or {}
        enemy_ships = fleet.get(enemy, 0) if enemy else 0
        enemy_hp = 15 * enemy_ships

        dmg_dealt = (s.get("damage_dealt_actual_by_faction") or {}).get(my_side, 0)
        dmg_taken = (s.get("damage_taken_by_faction") or {}).get(my_side, 0)
        kills = (s.get("kills_by_faction") or {}).get(my_side, 0)
        deaths = (s.get("deaths_by_faction") or {}).get(my_side, 0)

        combat_score = min(1.0, dmg_dealt / enemy_hp) if enemy_hp else 0.0
        damage_efficiency = (dmg_dealt / (dmg_dealt + dmg_taken)) if (dmg_dealt + dmg_taken) > 0 else 0.5
        kill_ratio = (kills / (kills + deaths)) if (kills + deaths) > 0 else 0.5

        my_ships = (s.get("fleet_sizes") or {}).get(my_side, 0)
        my_deaths = (s.get("deaths_by_faction") or {}).get(my_side, 0)
        survival_rate = ((my_ships - my_deaths) / my_ships) if my_ships else 0.0

        # Turn of first kill by my side (None if no kills)
        kills_list = s.get("kills") or []
        my_kill_turns = [
            k["turn"] for k in kills_list
            if k.get("killer_faction") == my_side and k.get("turn") is not None
        ]
        turns_to_first_kill = min(my_kill_turns) if my_kill_turns else None

        out.append({
            "source": b.get("source_file"),
            "map": s.get("map"),
            "player_id": s.get("player_id"),
            "session_id": s.get("session_id"),
            "level_goal": s.get("level_goal"),
            "level_type": s.get("level_type"),
            "matchup_type": s.get("matchup_type"),
            "human_side": human_side,
            "opponent_side": opponent_side,
            "opponent_role": roles.get(opponent_side) if opponent_side else None,
            "opponent_model": (s.get("faction_models") or {}).get(opponent_side) if opponent_side else None,
            "opponent_variant": (s.get("faction_variants") or {}).get(opponent_side) if opponent_side else None,
            "opponent_variant_name": (s.get("faction_variant_names") or {}).get(opponent_side) if opponent_side else None,
            "winner_side": winner_side,
            "won": won,
            "is_draw": is_draw,
            "win_reason": s.get("win_reason"),
            "final_turn": s.get("final_turn"),
            "battle_duration": s.get("battle_duration"),
            "real_duration_s": s.get("real_duration_s"),
            "response_count": rs.get("count", 0),
            "response_mean": rs.get("mean"),
            "response_times": (
                (s.get("human_commit_times") or {}).get(human_side, []) or []
            ),
            "invalid_total": invalid_total,
            "damage_dealt": (s.get("damage_dealt_actual_by_faction") or {}).get(human_side, 0),
            "damage_taken": (s.get("damage_taken_by_faction") or {}).get(human_side, 0),
            "off_turn_commit_count": sum(
                1 for c in (s.get("human_off_turn_commits") or [])
                if c.get("human_faction") == human_side
            ),
            "extra_text_raw": s.get("extra_text_raw"),
            "session_tag": s.get("session_tag"),
            "human_metrics": (s.get("human_metrics") or {}).get(human_side, {}),
            "turn_summaries": _filter_filler_turns(
                s.get("turn_summaries") or [], human_side
            ),
            "turn_durations_s": s.get("turn_durations_s") or {},
            "turn_setup_snapshots": {
                int(k): v for k, v in (s.get("turn_setup_snapshots") or {}).items()
            },
            "kills": kills,
            "deaths": deaths,
            "enemy_ships": enemy_ships,
            "enemy_hp": enemy_hp,
            "combat_score": combat_score,
            "damage_efficiency": damage_efficiency,
            "kill_ratio": kill_ratio,
            "survival_rate": survival_rate,
            "turns_to_first_kill": turns_to_first_kill,
        })
    return out

def llm_records(battles):
    """One record per battle in which an LLM participated, from the LLM's POV."""
    out = []
    for b in battles:
        s = b.get("summary", {})
        llm_side = s.get("llm_side")
        if not llm_side:
            continue

        roles = s.get("faction_roles", {}) or {}
        models = s.get("faction_models", {}) or {}
        prompts = s.get("faction_prompts", {}) or {}
        variants = s.get("faction_variants", {}) or {}
        variant_names = s.get("faction_variant_names", {}) or {}

        opponent_side = next((f for f in roles if f != llm_side), None)

        winner_side = s.get("winner_side")
        is_draw = bool(s.get("is_draw"))
        won = bool(winner_side and winner_side == llm_side)

        rs = (s.get("decision_stats_by_faction", {}) or {}).get(llm_side, {}) or {}
        wave_attacks = (s.get("wave_attacks_by_faction", {}) or {}).get(llm_side, 0)
        invalid_dict = (s.get("invalid_by_faction", {}) or {}).get(llm_side, {}) or {}
        invalid_total = sum(invalid_dict.values())
        damage_dealt = (s.get("damage_dealt_actual_by_faction", {}) or {}).get(llm_side, 0)
        damage_taken = (s.get("damage_taken_by_faction", {}) or {}).get(llm_side, 0)

        # --- quality metrics ---
        my_side = llm_side  # or llm_side in llm_records
        enemy = opponent_side
        fleet = s.get("fleet_sizes") or {}
        enemy_ships = fleet.get(enemy, 0) if enemy else 0
        enemy_hp = 15 * enemy_ships

        dmg_dealt = (s.get("damage_dealt_actual_by_faction") or {}).get(my_side, 0)
        dmg_taken = (s.get("damage_taken_by_faction") or {}).get(my_side, 0)
        kills = (s.get("kills_by_faction") or {}).get(my_side, 0)
        deaths = (s.get("deaths_by_faction") or {}).get(my_side, 0)

        combat_score = min(1.0, dmg_dealt / enemy_hp) if enemy_hp else 0.0
        damage_efficiency = (dmg_dealt / (dmg_dealt + dmg_taken)) if (dmg_dealt + dmg_taken) > 0 else 0.5
        kill_ratio = (kills / (kills + deaths)) if (kills + deaths) > 0 else 0.5

        my_ships = (s.get("fleet_sizes") or {}).get(my_side, 0)
        my_deaths = (s.get("deaths_by_faction") or {}).get(my_side, 0)
        survival_rate = ((my_ships - my_deaths) / my_ships) if my_ships else 0.0

        # Turn of first kill by my side (None if no kills)
        kills_list = s.get("kills") or []
        my_kill_turns = [
            k["turn"] for k in kills_list
            if k.get("killer_faction") == my_side and k.get("turn") is not None
        ]
        turns_to_first_kill = min(my_kill_turns) if my_kill_turns else None

        out.append({
            "source": b.get("source_file"),
            "map": s.get("map"),
            "scenario_shape": s.get("scenario_shape"),
            "matchup_type": s.get("matchup_type"),
            "llm_side": llm_side,
            "model": models.get(llm_side),
            "prompt": prompts.get(llm_side),
            "opponent_side": opponent_side,
            "opponent_role": roles.get(opponent_side) if opponent_side else None,
            "opponent_model": models.get(opponent_side) if opponent_side else None,
            "opponent_variant": variants.get(opponent_side) if opponent_side else None,
            "opponent_variant_name": variant_names.get(opponent_side) if opponent_side else None,
            "llm_variant": variants.get(llm_side),
            "winner_side": winner_side,
            "won": won,
            "is_draw": is_draw,
            "win_reason": s.get("win_reason"),
            "final_turn": s.get("final_turn"),
            "battle_duration": s.get("battle_duration"),
            "response_count": rs.get("count", 0),
            "response_mean": rs.get("mean"),
            "response_min": rs.get("min"),
            "response_max": rs.get("max"),
            "response_stdev": rs.get("stdev"),
            "response_times": (
                    (s.get("response_times_by_faction", {}) or {}).get(llm_side, [])
                    or []
            ),
            "wave_attacks": wave_attacks,
            "invalid_total": invalid_total,
            "damage_dealt": damage_dealt,
            "damage_taken": damage_taken,
            "kills": kills,
            "deaths": deaths,
            "enemy_ships": enemy_ships,
            "enemy_hp": enemy_hp,
            "combat_score": combat_score,
            "damage_efficiency": damage_efficiency,
            "kill_ratio": kill_ratio,
            "survival_rate": survival_rate,
            "turns_to_first_kill": turns_to_first_kill,
        })
    return out


# ---------------------------------------------------------------------------
# Formatting helpers
# ---------------------------------------------------------------------------

def fmt_row(cols, widths):
    return "  ".join(str(c).ljust(w) for c, w in zip(cols, widths))

def percentile(sorted_vals, p):
    """Linear-interpolated percentile. p in [0, 1]."""
    if not sorted_vals:
        return 0.0
    if len(sorted_vals) == 1:
        return float(sorted_vals[0])
    k = (len(sorted_vals) - 1) * p
    f = int(k)
    c = min(f + 1, len(sorted_vals) - 1)
    return sorted_vals[f] + (sorted_vals[c] - sorted_vals[f]) * (k - f)


def compute_timing_stats(values):
    """Full + P95 + trimmed stats for a list of response times."""
    if not values:
        return {
            "count": 0,
            "min": 0.0, "max": 0.0, "mean": 0.0, "stdev": 0.0,
            "p95": 0.0,
            "trimmed_count": 0,
            "trimmed_min": 0.0, "trimmed_max": 0.0,
            "trimmed_mean": 0.0, "trimmed_stdev": 0.0,
        }

    sorted_vals = sorted(values)
    n = len(sorted_vals)
    p95 = percentile(sorted_vals, 0.95)

    # Keep values <= p95; drops roughly the worst 5%
    trimmed = [v for v in sorted_vals if v <= p95]
    if not trimmed:
        trimmed = sorted_vals[:]

    p05 = percentile(sorted_vals, 0.05)
    middle = [v for v in sorted_vals if p05 <= v <= p95]
    if not middle:
        middle = sorted_vals[:]

    return {
        "count": n,
        "min": float(sorted_vals[0]),
        "max": float(sorted_vals[-1]),
        "mean": statistics.fmean(sorted_vals),
        "stdev": statistics.pstdev(sorted_vals) if n > 1 else 0.0,
        "p95": p95,
        "median": statistics.median(sorted_vals),
        "trimmed_count": len(trimmed),
        "trimmed_min": float(trimmed[0]),
        "trimmed_max": float(trimmed[-1]),
        "trimmed_mean": statistics.fmean(trimmed),
        "trimmed_stdev": statistics.pstdev(trimmed) if len(trimmed) > 1 else 0.0,
        "middle_90_count": len(middle),
        "middle_90_min": float(middle[0]),
        "middle_90_max": float(middle[-1]),
        "middle_90_mean": statistics.fmean(middle),
        "middle_90_stdev": (
            statistics.pstdev(middle) if len(middle) > 1 else 0.0
        ),
    }

def fmt_table(headers, rows, aligns=None):
    """Simple fixed-width table."""
    if not rows:
        print("  (no rows)")
        return

    # Column widths
    widths = [len(h) for h in headers]
    for row in rows:
        for i, cell in enumerate(row):
            widths[i] = max(widths[i], len(str(cell)))

    aligns = aligns or ["<"] * len(headers)

    def fmt_cell(cell, width, align):
        s = str(cell)
        return s.rjust(width) if align == ">" else s.ljust(width)

    # Header
    header_line = "  ".join(
        fmt_cell(h, w, "<") for h, w in zip(headers, widths)
    )
    print("  " + header_line)
    print("  " + "  ".join("-" * w for w in widths))

    # Rows
    for row in rows:
        line = "  ".join(
            fmt_cell(c, w, a) for c, w, a in zip(row, widths, aligns)
        )
        print("  " + line)

def pct(num, denom):
    if not denom:
        return "—"
    return f"{100 * num / denom:.1f}%"

def wilson_interval(n, wins, z=1.96):
    """95% Wilson score interval for a proportion. Returns (lower, upper)."""
    if n <= 0:
        return (0.0, 1.0)
    p = wins / n
    denom = 1 + z * z / n
    centre = (p + z * z / (2 * n)) / denom
    half = (z * math.sqrt((p * (1 - p) + z * z / (4 * n)) / n)) / denom
    return (max(0.0, centre - half), min(1.0, centre + half))

def pearson_r(xs, ys):
    """Pearson correlation coefficient. Returns None if degenerate."""
    n = len(xs)
    if n < 3 or len(ys) != n:
        return None
    mx = statistics.fmean(xs)
    my = statistics.fmean(ys)
    num = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
    denom = math.sqrt(
        sum((x - mx) ** 2 for x in xs) *
        sum((y - my) ** 2 for y in ys)
    )
    return (num / denom) if denom > 0 else None

def fmt_ci(wins, total):
    """Format a Wilson CI as 'lo-hi' in percentage points, e.g. '30.4-59.3'."""
    if not total:
        return "—"
    lo, hi = wilson_interval(total, wins)
    return f"{lo * 100:.1f}-{hi * 100:.1f}"


def _empty_quality_bucket():
    return {
        "n": 0, "w": 0, "l": 0, "d": 0,
        "combat": [], "dmg_eff": [], "kill_ratio": [],
        "survival": [], "ttfk": [],
    }


def _aggregate_quality(records, key_fn):
    """Group records by key_fn; accumulate quality metrics into per-key buckets."""
    buckets = defaultdict(_empty_quality_bucket)
    for r in records:
        key = key_fn(r)
        b = buckets[key]
        b["n"] += 1
        if r.get("is_draw"):
            b["d"] += 1
        elif r.get("won"):
            b["w"] += 1
        else:
            b["l"] += 1
        for src_key, dst_key in [
            ("combat_score",        "combat"),
            ("damage_efficiency",   "dmg_eff"),
            ("kill_ratio",          "kill_ratio"),
            ("survival_rate",       "survival"),
            ("turns_to_first_kill", "ttfk"),
        ]:
            v = r.get(src_key)
            if v is not None:
                b[dst_key].append(v)
    return buckets


def _summarize_quality_bucket(b):
    """Reduce a bucket to a flat dict of scalar metrics."""
    def _m(key):
        return statistics.fmean(b[key]) if b[key] else None
    return {
        "n": b["n"], "w": b["w"], "l": b["l"], "d": b["d"],
        "win_pct": (100 * b["w"] / b["n"]) if b["n"] else 0.0,
        "combat":     _m("combat"),
        "dmg_eff":    _m("dmg_eff"),
        "kill_ratio": _m("kill_ratio"),
        "survival":   _m("survival"),
        "ttfk":       _m("ttfk"),
    }


def _quality_row(prefix_cols, s):
    """Format a summary dict into a table row."""
    return list(prefix_cols) + [
        s["n"],
        f"{s['w']}/{s['n']} ({s['win_pct']:.0f}%)",
        f"{s['combat']:.2f}"     if s["combat"]     is not None else "—",
        f"{s['dmg_eff']:.2f}"    if s["dmg_eff"]    is not None else "—",
        f"{s['kill_ratio']:.2f}" if s["kill_ratio"] is not None else "—",
        f"{s['survival']:.2f}"   if s["survival"]   is not None else "—",
        f"{s['ttfk']:.1f}"       if s["ttfk"]       is not None else "—",
    ]


def _print_quality_legend():
    print("  combat    = damage dealt / total enemy HP    (1.0 = wiped enemy)")
    print("  dmgEff    = damage dealt / (dealt + taken)   (0.5 = even trade, 1.0 = flawless)")
    print("  killRt    = kills / (kills + deaths)         (0.5 = even, 1.0 = flawless)")
    print("  survive   = fraction of your starting ships still alive at end")
    print("  ttfk      = mean turn of first kill (in turn units)")
    print()

def classify_turn(ts, human_side,
                  snap_dur=20.0, snap_gap=2.0,
                  deliberate_dur=45.0, deliberate_gap=10.0,
                  deliberate_ratio=2.5):
    dur = ts.get("wallclock_duration_s") or 0.0
    hc = (ts.get("human_commits") or {}).get(human_side) or {}
    n_commits = hc.get("count", 0)
    n_inputs  = ts.get("input_count", 0)

    # Filler: engine-created turn, no duration, no activity at all.
    if dur < 0.5 and n_commits == 0 and n_inputs == 0:
        return "filler"
    # Real turn where player did nothing.
    if n_commits == 0:
        return "no_action"

    mean_g = hc.get("mean_gap_s") or 0.0
    max_g  = hc.get("max_gap_s")  or 0.0
    ratio  = (max_g / mean_g) if mean_g > 0 else 0.0

    if dur > deliberate_dur or max_g > deliberate_gap or ratio > deliberate_ratio:
        return "deliberate"
    if dur < snap_dur and max_g < snap_gap:
        return "snap"
    return "considered"

def _filter_filler_turns(ts_list, human_side):
    out = []
    for ts in ts_list:
        dur = ts.get("wallclock_duration_s") or 0.0
        hc = (ts.get("human_commits") or {}).get(human_side) or {}
        if dur < 0.5 and hc.get("count", 0) == 0 and ts.get("input_count", 0) == 0:
            continue
        out.append(ts)
    return out

# ---------------------------------------------------------------------------
# Views
# ---------------------------------------------------------------------------

def view_overview(battles, records):
    print("=" * 70)
    print("OVERVIEW")
    print("=" * 70)

    total = len(battles)
    llm_battles = len(records)
    draws = sum(1 for r in records if r["is_draw"])
    decisive = llm_battles - draws

    maps = sorted({b.get("summary", {}).get("map") for b in battles
                   if b.get("summary", {}).get("map")})
    models = sorted({r["model"] for r in records if r["model"]})
    prompts = sorted({r["prompt"] for r in records if r["prompt"]})
    shapes = sorted({b.get("summary", {}).get("scenario_shape") for b in battles
                     if b.get("summary", {}).get("scenario_shape")})

    print(f"  Total battles:        {total}")
    print(f"  Battles with LLM:     {llm_battles}")
    print(f"  Decisive (LLM):       {decisive}")
    print(f"  Draws (LLM):          {draws}")
    print(f"  Maps:                 {', '.join(maps) or '—'}")
    print(f"  Scenario shapes:      {', '.join(shapes) or '—'}")
    print(f"  Models:               {', '.join(models) or '—'}")
    print(f"  Prompts:              {', '.join(prompts) or '—'}")
    variant_pairs = {}
    for r in records:
        short = r.get("opponent_variant")
        if not short:
            continue
        long = r.get("opponent_variant_name") or ""
        # Prefer a non-empty long name if we've seen one
        if short not in variant_pairs or (not variant_pairs[short] and long):
            variant_pairs[short] = long

    if variant_pairs:
        pretty = ", ".join(
            f"{s}({l})" if l else s
            for s, l in sorted(variant_pairs.items())
        )
        print(f"  AU variants:          {pretty}")
    print()

def view_hard_turns(records, turn_filter=None, ignore_turns=None,
                    max_gap_ratio=2.5, min_duration_s=45):
    """
    Print ASCII snapshots of the board *before* each flagged turn.

    A turn is flagged if:
      - max_gap / mean_gap > max_gap_ratio   (a deliberate pause mid-turn), or
      - wallclock duration > min_duration_s  (a long turn overall).

    The snapshot is the last SCNR that fired before the turn's TURN event —
    i.e. the board state the player was looking at when the turn began.
    """
    ignore_turns = ignore_turns or set()
    print("=" * 110)
    print("HARD-TURN SETUPS (ASCII board, state at start of the turn)")
    print("=" * 110)
    print(f"  Flag: max_gap / mean_gap > {max_gap_ratio}   OR   duration > {min_duration_s}s")
    if turn_filter is not None:
        print(f"  Showing only turn {turn_filter}")
    if ignore_turns:
        print(f"  Ignoring turns: {sorted(ignore_turns)}")
    print()

    battles_scanned = 0
    total_flagged = 0

    for r in records:
        ts_list = r.get("turn_summaries") or []
        setup_by_turn = r.get("turn_setup_snapshots") or {}
        if not ts_list:
            continue
        human = r.get("human_side") or "?"
        battles_scanned += 1

        flagged = []
        for ts in ts_list:
            turn_n = ts.get("turn")
            if turn_n in ignore_turns:
                continue
            if turn_filter is not None and turn_n != turn_filter:
                continue
            hc = (ts.get("human_commits") or {}).get(human)
            if not hc:
                continue
            mean_g = hc.get("mean_gap_s") or 0.0
            max_g  = hc.get("max_gap_s")  or 0.0
            dur    = ts.get("wallclock_duration_s") or 0.0
            if dur == 0.0:          # end-of-game filler turn
                continue
            ratio = (max_g / mean_g) if mean_g > 0 else 0.0
            if ratio > max_gap_ratio or dur > min_duration_s:
                flagged.append({
                    "turn":           turn_n,
                    "duration_s":     dur,
                    "mean_gap_s":     mean_g,
                    "max_gap_s":      max_g,
                    "max_gap_ratio":  ratio,
                    "inputs":         ts.get("input_count", 0),
                    "commits":        hc["count"],
                })

        if not flagged:
            continue
        total_flagged += len(flagged)

        header = (
            f"[{r.get('player_id') or '?'} | {r.get('session_tag') or '?'} | "
            f"{r.get('map') or '?'} | {human}]  "
            f"opp={r.get('opponent_variant') or r.get('opponent_model') or '?'}"
        )
        print(header)
        print("-" * len(header))

        for f in flagged:
            setup = setup_by_turn.get(f["turn"])
            print(
                f"  Turn {f['turn']}  "
                f"dur={f['duration_s']:.1f}s  "
                f"max/mean={f['max_gap_ratio']:.1f}x  "
                f"inputs={f['inputs']}  commits={f['commits']}"
            )
            if not setup:
                print("    (no SCNR snapshot available)")
                print()
                continue
            for row in setup:
                print(f"    {row}")
            print()

        print()

    print(f"Scanned {battles_scanned} battles, flagged {total_flagged} turns.")
    print()


def view_hard_turns_quiet(records, ignore_turns=None, **_):
    """List flagged turns without printing boards (fast scan)."""
    print("=" * 110)
    print("HARD TURNS — SUMMARY ONLY")
    print("=" * 110)
    if ignore_turns:
        print(f"  Ignoring turns: {sorted(ignore_turns)}")
    for r in records:
        ts_list = r.get("turn_summaries") or []
        if not ts_list:
            continue
        human = r.get("human_side") or "?"
        flagged = []
        for ts in ts_list:
            turn_n = ts.get("turn")
            if turn_n in ignore_turns:
                continue
            hc = (ts.get("human_commits") or {}).get(human)
            if not hc:
                continue
            mean_g = hc.get("mean_gap_s") or 0.0
            max_g  = hc.get("max_gap_s")  or 0.0
            dur    = ts.get("wallclock_duration_s") or 0.0
            if dur == 0.0:
                continue
            ratio = (max_g / mean_g) if mean_g > 0 else 0.0
            if ratio > 2.5 or dur > 45:
                flagged.append((ts["turn"], dur, ratio))
        if not flagged:
            continue
        turns_str = ", ".join(
            f"t{t}({d:.0f}s,{r:.1f}x)" for t, d, r in flagged
        )
        print(f"  [{r.get('map') or '?'} | {human} | "
              f"opp={r.get('opponent_variant') or r.get('opponent_model') or '?'}]  "
              f"{turns_str}")
    print()

def view_human_turns(records,
                     snap_dur=20.0, snap_gap=2.0,
                     deliberate_dur=45.0, deliberate_gap=10.0,
                     deliberate_ratio=2.5):
    print("=" * 130)
    print("HUMAN TURN-LEVEL METRICS (per player / session)")
    print("=" * 130)

    # ---- Legend (dynamically reflects the thresholds in use) ----
    print()
    print("Turn classification:")
    print(f"  filler      duration == 0s (engine end-of-game turn; no human actions)")
    print(f"  no_action   duration > 0s but no human commits")
    print(f"  snap        duration < {snap_dur:.0f}s  AND  max commit gap < {snap_gap:.0f}s")
    print(f"              -> fast/obvious state, low deliberation")
    print(f"  considered  neither snap nor deliberate (normal deliberation)")
    print(f"  deliberate  duration > {deliberate_dur:.0f}s  OR  "
          f"max gap > {deliberate_gap:.0f}s  OR  max/mean gap ratio > {deliberate_ratio:.1f}x")
    print(f"              -> likely a hard decision moment")
    print("  delib%      = deliberate / (snap + considered + deliberate + no_action),")
    print("                i.e. fraction of *active* turns that were hard")
    print()

    # ---- Aggregate ----
    buckets = defaultdict(lambda: {
        "turns": 0,
        "snap": 0, "considered": 0, "deliberate": 0,
        "no_action": 0, "filler": 0,
        "commits": 0,
        "durations": [],     # excludes filler (0s)
        "inputs": [],        # excludes filler
        "commit_gaps": [],   # from per-turn human_commits.mean_gap_s
    })

    for r in records:
        pid  = r.get("player_id") or "(unknown)"
        sess = r.get("session_tag") or "—"
        human = r.get("human_side") or "?"
        b = buckets[(pid, sess)]

        for ts in (r.get("turn_summaries") or []):
            label = classify_turn(
                ts, human,
                snap_dur=snap_dur, snap_gap=snap_gap,
                deliberate_dur=deliberate_dur,
                deliberate_gap=deliberate_gap,
                deliberate_ratio=deliberate_ratio,
            )
            b["turns"] += 1
            b[label] += 1

            dur = ts.get("wallclock_duration_s")
            if dur is not None and dur > 0:
                b["durations"].append(dur)

            fac_inputs = (ts.get("input_by_faction") or {}).get(human, 0)
            b["inputs"].append(fac_inputs)

            hc = (ts.get("human_commits") or {}).get(human) or {}
            if hc:
                b["commits"] += hc.get("count", 0)
                if hc.get("mean_gap_s"):
                    b["commit_gaps"].append(hc["mean_gap_s"])

    # ---- Build rows ----
    rows = []
    totals = {"turns": 0, "snap": 0, "considered": 0, "deliberate": 0,
              "no_action": 0, "filler": 0, "commits": 0}
    grand_durations, grand_inputs, grand_gaps = [], [], []

    for (pid, sess), b in sorted(buckets.items()):
        active = b["snap"] + b["considered"] + b["deliberate"] + b["no_action"]
        delib_pct = (100 * b["deliberate"] / active) if active else 0.0

        rows.append([
            pid,
            sess,
            b["turns"],
            b["snap"],
            b["considered"],
            b["deliberate"],
            b["no_action"],
            b["filler"],
            f"{delib_pct:.0f}%",
            b["commits"],
            f"{statistics.fmean(b['durations']):.1f}"  if b["durations"]  else "—",
            f"{statistics.fmean(b['inputs']):.1f}"     if b["inputs"]     else "—",
            f"{statistics.fmean(b['commit_gaps']):.2f}" if b["commit_gaps"] else "—",
        ])

        for k in totals:
            totals[k] += b[k]
        grand_durations.extend(b["durations"])
        grand_inputs.extend(b["inputs"])
        grand_gaps.extend(b["commit_gaps"])

    fmt_table(
        ["player_id", "session", "turns",
         "snap", "cons", "delib", "no_act", "filler", "delib%",
         "commits", "mean dur (s)", "mean in/turn", "mean gap (s)"],
        rows,
        aligns=["<", "<", ">", ">", ">", ">", ">", ">", ">",
                ">", ">", ">", ">"],
    )

    # ---- Footer: overall totals across all players ----
    active = totals["snap"] + totals["considered"] + totals["deliberate"] + totals["no_action"]
    delib_pct = (100 * totals["deliberate"] / active) if active else 0.0
    print()
    print(
        f"  TOTAL  turns={totals['turns']}  "
        f"snap={totals['snap']}  cons={totals['considered']}  "
        f"delib={totals['deliberate']}  no_act={totals['no_action']}  "
        f"filler={totals['filler']}  delib%={delib_pct:.0f}%  "
        f"commits={totals['commits']}"
    )
    if grand_durations:
        print(f"  Overall mean turn = {statistics.fmean(grand_durations):.1f}s")
    print()


def view_human_per_battle(records):
    """One row per human battle, showing the per-turn breakdown."""
    print("=" * 110)
    print("PER-TURN BREAKDOWN (one line per turn, human-side only)")
    print("=" * 110)

    for r in records:
        ts_list = r.get("turn_summaries") or []
        if not ts_list:
            continue
        human = r.get("human_side") or "?"
        print(f"\n[{r.get('player_id') or '?'} | {r.get('session_tag') or '?'} | "
              f"{r.get('map') or '?'} | {human}]  "
              f"opp={r.get('opponent_variant') or r.get('opponent_model') or '?'}")
        rows = []
        for ts in ts_list:
            hc = (ts.get("human_commits") or {}).get(human, {})
            rows.append([
                ts["turn"],
                ts.get("input_count", 0),
                f"{ts['wallclock_duration_s']:.1f}"
                    if ts.get("wallclock_duration_s") is not None else "—",
                hc.get("count", 0),
                f"{hc['mean_gap_s']:.2f}" if hc else "—",
                f"{hc['max_gap_s']:.2f}"  if hc else "—",
                ts.get("first_input_battle_delta") if ts.get("first_input_battle_delta") is not None else "—",
            ])
        fmt_table(
            ["turn", "inputs", "duration (s)", "commits", "mean gap (s)", "max gap (s)", "1st input Δbt"],
            rows,
            aligns=[">", ">", ">", ">", ">", ">", ">"],
        )
    print()

def view_humans(records):
    print("=" * 90)
    print("HUMAN RESULTS BY PLAYER × SESSION")
    print("=" * 90)
    buckets = defaultdict(lambda: {"w": 0, "l": 0, "d": 0})
    for r in records:
        pid  = r.get("player_id") or "(unknown)"
        sess = r.get("session_tag") or "—"
        key = (pid, sess)
        if r["is_draw"]:
            buckets[key]["d"] += 1
        elif r["won"]:
            buckets[key]["w"] += 1
        else:
            buckets[key]["l"] += 1

    rows = []
    for (pid, sess), b in buckets.items():
        total = b["w"] + b["l"] + b["d"]
        rows.append([pid, sess, b["w"], b["l"], b["d"], total,
                     pct(b["w"], total), f"[{fmt_ci(b['w'], total)}]"])
    rows.sort(key=lambda r: (-r[2], r[0], r[1]))
    fmt_table(
        ["player_id", "session", "W", "L", "D", "Total", "Win%", "95% CI"],
        rows, aligns=["<", "<", ">", ">", ">", ">", ">", "<"],
    )
    print()


def view_human_vs_au(records):
    """Human win% by opponent type — the thesis baseline table."""
    print("=" * 92)
    print("HUMAN WIN% BY OPPONENT TYPE (AU variant / LLM model / scenario)")
    print("=" * 92)
    buckets = defaultdict(lambda: {"w": 0, "l": 0, "d": 0})
    for r in records:
        if r.get("level_type") == "tutorial":
            continue  # handle via 'levels' view instead
        key = (
            r["opponent_role"] or "?",
            r.get("opponent_variant") or "—",
            r.get("opponent_model") or "—",
        )
        if r["is_draw"]:
            buckets[key]["d"] += 1
        elif r["won"]:
            buckets[key]["w"] += 1
        else:
            buckets[key]["l"] += 1

    rows = []
    for (role, variant, model), b in buckets.items():
        total = b["w"] + b["l"] + b["d"]
        rows.append([role, variant, model,
                     b["w"], b["l"], b["d"], total,
                     pct(b["w"], total), f"[{fmt_ci(b['w'], total)}]"])
    rows.sort(key=lambda r: (r[0], r[1], r[2]))
    fmt_table(
        ["opponent", "variant", "model", "W", "L", "D", "Total", "Win%", "95% CI"],
        rows, aligns=["<", "<", "<", ">", ">", ">", ">", ">", "<"],
    )
    print()


def view_levels(records):
    """Per-level completion + duration (tutorials + matches pooled)."""
    print("=" * 96)
    print("PER-LEVEL COMPLETION & DURATION")
    print("=" * 96)
    buckets = defaultdict(lambda: {
        "n": 0, "wins": 0, "draws": 0, "times": [], "turns": [],
    })
    for r in records:
        lvl = r["map"] or "?"
        b = buckets[lvl]
        b["n"] += 1
        if r["is_draw"]:
            b["draws"] += 1
        elif r["won"]:
            b["wins"] += 1
        if r.get("real_duration_s") is not None:
            b["times"].append(r["real_duration_s"])
        if r.get("final_turn") is not None:
            b["turns"].append(r["final_turn"])

    rows = []
    for lvl, b in buckets.items():
        mean_s = statistics.fmean(b["times"]) if b["times"] else None
        mean_t = statistics.fmean(b["turns"]) if b["turns"] else None
        rows.append([
            lvl, b["n"], b["wins"],
            b["n"] - b["wins"] - b["draws"], b["draws"],
            pct(b["wins"], b["n"]),
            f"{mean_s:.1f}" if mean_s is not None else "—",
            f"{mean_t:.1f}" if mean_t is not None else "—",
        ])
    rows.sort(key=lambda r: r[0])
    fmt_table(
        ["level", "n", "W", "L", "D", "Win%", "mean s", "mean turn"],
        rows, aligns=["<", ">", ">", ">", ">", ">", ">", ">"],
    )
    print()

def view_wins_by_model(records):
    print("=" * 70)
    print("WINS BY MODEL (LLM side only)")
    print("=" * 70)

    buckets = defaultdict(lambda: {"w": 0, "l": 0, "d": 0})
    for r in records:
        m = r["model"] or "(unknown)"
        if r["is_draw"]:
            buckets[m]["d"] += 1
        elif r["won"]:
            buckets[m]["w"] += 1
        else:
            buckets[m]["l"] += 1

    rows = []
    for m, b in buckets.items():
        total = b["w"] + b["l"] + b["d"]
        rows.append([
            m,
            b["w"],
            b["l"],
            b["d"],
            total,
            pct(b["w"], total),
            f"[{fmt_ci(b['w'], total)}]",
        ])

    rows.sort(key=lambda r: (-r[1], r[0]))
    fmt_table(
        ["model", "W", "L", "D", "Total", "Win%", "95% CI"],
        rows,
        aligns=["<", ">", ">", ">", ">", ">", "<"],
    )
    print()


def view_wins_by_model_and_opponent(records):
    print("=" * 70)
    print("WINS BY MODEL × OPPONENT")
    print("=" * 70)

    buckets = defaultdict(lambda: {"w": 0, "l": 0, "d": 0})
    for r in records:
        key = (r["model"] or "(unknown)", r["opponent_role"] or "?")
        if r["is_draw"]:
            buckets[key]["d"] += 1
        elif r["won"]:
            buckets[key]["w"] += 1
        else:
            buckets[key]["l"] += 1

    rows = []
    for (m, opp), b in buckets.items():
        total = b["w"] + b["l"] + b["d"]
        rows.append([
            m,
            opp,
            b["w"],
            b["l"],
            b["d"],
            total,
            pct(b["w"], total),
            f"[{fmt_ci(b['w'], total)}]",
        ])

    rows.sort(key=lambda r: (r[0], r[1]))
    fmt_table(
        ["model", "opponent", "W", "L", "D", "Total", "Win%", "95% CI"],
        rows,
        aligns=["<", "<", ">", ">", ">", ">", ">", "<"],
    )
    print()

def view_variants(records):
    print("=" * 88)
    print("WINS BY AU VARIANT × LLM MODEL")
    print("=" * 88)

    # Lookup: short -> long (from the first record that has both)
    long_by_short = {}
    for r in records:
        sh = r.get("opponent_variant")
        if sh and sh not in long_by_short:
            long_by_short[sh] = r.get("opponent_variant_name") or ""

    buckets = defaultdict(lambda: {"w": 0, "l": 0, "d": 0})
    for r in records:
        variant = r.get("opponent_variant") or "(none)"
        model = r["model"] or "?"
        key = (variant, model)
        if r["is_draw"]:
            buckets[key]["d"] += 1
        elif r["won"]:
            buckets[key]["w"] += 1
        else:
            buckets[key]["l"] += 1

    rows = []
    for (v, m), b in buckets.items():
        total = b["w"] + b["l"] + b["d"]
        rows.append([
            v,
            long_by_short.get(v, ""),
            m,
            b["w"],
            b["l"],
            b["d"],
            total,
            pct(b["w"], total),
            f"[{fmt_ci(b['w'], total)}]",
        ])

    rows.sort(key=lambda x: (x[0], x[2]))
    fmt_table(
        ["AU", "name", "model", "W", "L", "D", "Total", "Win%", "95% CI"],
        rows,
        aligns=["<", "<", "<", ">", ">", ">", ">", ">", "<"],
    )
    print()

def view_side_results(records):
    print("=" * 92)
    print("WIN/LOSS BY SIDE (AU VARIANT × LLM MODEL)")
    print("=" * 92)

    # buckets[(variant, model)] = {"Red": {"w":..,"l":..,"d":..},
    #                              "Green": {"w":..,"l":..,"d":..}}
    buckets = defaultdict(lambda: {
        "Red": {"w": 0, "l": 0, "d": 0},
        "Green": {"w": 0, "l": 0, "d": 0},
    })

    for r in records:
        variant = r.get("opponent_variant") or "(none)"
        model = r["model"] or "?"
        side = r.get("llm_side")
        if side not in ("Red", "Green"):
            continue

        b = buckets[(variant, model)][side]
        if r["is_draw"]:
            b["d"] += 1
        elif r["won"]:
            b["w"] += 1
        else:
            b["l"] += 1

    rows = []
    for (v, m), sides in sorted(buckets.items()):
        red = sides["Red"]
        green = sides["Green"]

        red_total = red["w"] + red["l"] + red["d"]
        green_total = green["w"] + green["l"] + green["d"]

        red_pct = pct(red["w"], red_total)
        green_pct = pct(green["w"], green_total)

        rows.append([
            v, m,
            red["w"], red["l"], red["d"], red_total, red_pct,
            green["w"], green["l"], green["d"], green_total, green_pct,
        ])

    fmt_table(
        ["AU", "model",
         "R-W", "R-L", "R-D", "R-n", "R%",
         "G-W", "G-L", "G-D", "G-n", "G%"],
        rows,
        aligns=["<", "<",
                ">", ">", ">", ">", ">",
                ">", ">", ">", ">", ">"],
    )
    print()
    print("R-* = LLM plays Red, AU plays Green.")
    print("G-* = LLM plays Green, AU plays Red.")
    print("A large R% vs G% gap in the same row suggests a side effect.")
    print()

def view_response_times(records):
    print("=" * 100)
    print("RESPONSE TIMES BY MODEL (ms) — with P95 and trimmed stats")
    print("=" * 100)

    pooled = defaultdict(list)
    for r in records:
        model = r["model"] or "(unknown)"
        pooled[model].extend(r.get("response_times", []))

    rows = []
    for model, values in sorted(pooled.items()):
        st = compute_timing_stats(values)
        rows.append([
            model,
            st["count"],
            f"{st['min']:.0f}",
            f"{st['mean']:.0f}",
            f"{st['p95']:.0f}",
            f"{st['max']:.0f}",
            f"{st['trimmed_mean']:.0f}",
            f"{st['trimmed_count']}",
            f"{st['median']:.0f}",
            f"{st['stdev']:.0f}",
            f"{st['middle_90_mean']:.0f}",
        ])

    fmt_table(
        ["model", "n", "min", "mean", "p95", "max", "trim mean", "trim n", "median", "stdev", "mid 90 m"],
        rows,
        aligns=["<", ">", ">", ">", ">", ">", ">", ">", ">", ">", ">"],
    )
    print()
    print("p95        = 95th percentile of all calls for that model (ms).")
    print("trim mean  = mean of the bottom 95% (outliers above p95 excluded).")
    print("trim n     = number of calls used for the trimmed stats.")
    print("median     = 50th percentile (P50); robust to outliers.")
    print("stdev      = population standard deviation of all calls; "
          "inflated by load-time spikes.")
    print("mid 90 m   = mean of the middle 90% of calls "
          "(between p05 and p95); removes both tails.")
    print()

def view_raw(records):
    print("=" * 110)
    print("RAW BATTLE RECORDS (LLM side)")
    print("=" * 110)

    rows = []
    for r in records:
        result = "D" if r["is_draw"] else ("W" if r["won"] else "L")

        src = r["source"] or ""
        if len(src) > 42:
            src = src[:39] + "..."

        rows.append([
            src,
            r["model"] or "?",
            r.get("opponent_variant") or "—",
            (r["map"] or "?")[:22],
            result,
            r["win_reason"] or "—",
            r["final_turn"] if r["final_turn"] is not None else "—",
            r.get("wave_attacks", 0),
            r.get("invalid_total", 0),
            f"{r.get('damage_dealt', 0):.0f}",
            f"{r.get('damage_taken', 0):.0f}",
            f"{r['response_mean']:.0f}" if r.get("response_mean") else "—",
        ])

    # Sort by map, then model, then source
    rows.sort(key=lambda x: (x[3], x[1], x[0]))

    fmt_table(
        ["source", "model", "AU-var", "map", "R", "reason",
         "turns", "wave", "inval", "dmgD", "dmgT", "resp"],
        rows,
        aligns=["<", "<", "<", "<", "<", "<", ">", ">", ">", ">", ">", ">"],
    )
    print()

def view_deliberation_vs_outcome(records, **kw):
    print("=" * 130)
    print("DELIBERATION vs OUTCOME  (per player)")
    print("=" * 130)
    print("  Read across a row: does a player who deliberates more actually win more?")
    print("  combat = mean damage dealt / total enemy HP  (1.0 = fully wiped out enemy)")
    print("  dmgEff = mean damage dealt / (damage dealt + damage taken)  (0.5 = even trade)")
    print()

    buckets = defaultdict(lambda: {
        "cohort": None,
        "battles": 0, "wins": 0, "losses": 0, "draws": 0,
        "variants": defaultdict(int),
        "turns": 0, "dur_sum": 0.0, "commits": 0,
        "snap": 0, "considered": 0, "deliberate": 0,
        "no_action": 0, "filler": 0,
        "combat": [], "dmg_eff": [], "kill_ratio": [],
    })

    for r in records:
        player = r.get("session_tag") or r.get("player_id") or "?"
        b = buckets[player]
        b["cohort"] = r.get("player_id") or "?"
        b["battles"] += 1
        if r["is_draw"]: b["draws"] += 1
        elif r["won"]:   b["wins"]  += 1
        else:            b["losses"] += 1

        b["variants"][r.get("opponent_variant") or "?"] += 1

        if r.get("combat_score") is not None:
            b["combat"].append(r["combat_score"])
        if r.get("damage_efficiency") is not None:
            b["dmg_eff"].append(r["damage_efficiency"])
        if r.get("kill_ratio") is not None:
            b["kill_ratio"].append(r["kill_ratio"])

        for ts in (r.get("turn_summaries") or []):
            label = classify_turn(
                ts, r["human_side"],
                snap_dur=kw.get("snap_dur", 20.0),
                snap_gap=kw.get("snap_gap", 2.0),
                deliberate_dur=kw.get("deliberate_dur", 45.0),
                deliberate_gap=kw.get("deliberate_gap", 10.0),
                deliberate_ratio=kw.get("deliberate_ratio", 2.5),
            )
            b[label] += 1
            b["turns"] += 1
            dur = ts.get("wallclock_duration_s")
            if dur: b["dur_sum"] += dur
            hc = (ts.get("human_commits") or {}).get(r["human_side"]) or {}
            b["commits"] += hc.get("count", 0)

    rows = []
    for player, b in sorted(buckets.items()):
        opp_str = ",".join(f"{v}:{n}" for v, n in sorted(b["variants"].items()))
        active = (b["snap"] + b["considered"] + b["deliberate"] + b["no_action"])
        delib_pct = (100 * b["deliberate"] / active) if active else 0
        mean_dur = (b["dur_sum"] / b["turns"]) if b["turns"] else 0
        win_pct  = (100 * b["wins"] / b["battles"]) if b["battles"] else 0

        rows.append([
            player,
            b["cohort"],
            opp_str,
            f"{b['wins']}/{b['battles']} ({win_pct:.0f}%)",
            b["turns"],
            f"{mean_dur:.1f}",
            f"{delib_pct:.0f}%",
            b["commits"],
            f"{statistics.fmean(b['combat']):.2f}"   if b["combat"]    else "—",
            f"{statistics.fmean(b['dmg_eff']):.2f}"  if b["dmg_eff"]   else "—",
            f"{statistics.fmean(b['kill_ratio']):.2f}" if b["kill_ratio"] else "—",
        ])

    rows.sort(key=lambda r: -float(r[3].split("(")[-1].rstrip("%)") or 0))

    # ---- Pearson correlations across sessions ----
    metrics = []
    for player, b in buckets.items():
        active = (b["snap"] + b["considered"]
                  + b["deliberate"] + b["no_action"])
        delib_pct = (100 * b["deliberate"] / active) if active else 0.0
        win_pct   = (100 * b["wins"] / b["battles"]) if b["battles"] else 0.0
        metrics.append({
            "delib_pct":  delib_pct,
            "win_pct":    win_pct,
            "combat":     statistics.fmean(b["combat"])     if b["combat"]     else None,
            "dmg_eff":    statistics.fmean(b["dmg_eff"])    if b["dmg_eff"]    else None,
            "kill_ratio": statistics.fmean(b["kill_ratio"]) if b["kill_ratio"] else None,
        })

    def _corr(key):
        pairs = [(m["delib_pct"], m[key]) for m in metrics
                 if m[key] is not None]
        if len(pairs) < 3:
            return None
        xs = [p[0] for p in pairs]
        ys = [p[1] for p in pairs]
        return pearson_r(xs, ys), len(pairs)
    fmt_table(
        ["player", "cohort", "opponents", "W/total",
         "turns", "mean dur", "delib%", "commits",
         "combat", "dmgEff", "killRt"],
        rows,
        aligns=["<", "<", "<", "<", ">", ">", ">", ">", ">", ">", ">"],
    )
    print()

    print("Pearson r  (deliberation% vs outcome, across sessions):")
    print("  |r| > 0.70 → strong;  > 0.50 → moderate;  < 0.30 → no clear relationship")
    for key, label in [
        ("win_pct", "win%"),
        ("combat", "combat"),
        ("dmg_eff", "dmgEff"),
        ("kill_ratio", "killRt"),
    ]:
        result = _corr(key)
        if not result:
            print(f"  delib% vs {label:<8}  —  (too few sessions)")
            continue
        r, n = result
        strength = "strong" if abs(r) > 0.7 else ("moderate" if abs(r) > 0.5 else "weak/none")
        sign = "+" if r >= 0 else "−"
        print(f"  delib% vs {label:<8}  r = {sign}{abs(r):.3f}  (n={n}, {strength})")
    print()

def view_battle_lengths(records):
    print("=" * 70)
    print("BATTLE LENGTHS BY MODEL")
    print("=" * 70)

    buckets = defaultdict(list)
    for r in records:
        if r["final_turn"] is not None:
            buckets[r["model"] or "(unknown)"].append(r)

    rows = []
    for model, rs in buckets.items():
        turns = [r["final_turn"] for r in rs]
        ticks = [r["battle_duration"] for r in rs if r["battle_duration"]]
        rows.append([
            model,
            len(rs),
            f"{sum(turns)/len(turns):.1f}",
            min(turns),
            max(turns),
            f"{sum(ticks)/len(ticks):.0f}" if ticks else "—",
        ])

    rows.sort(key=lambda r: r[0])
    fmt_table(
        ["model", "n", "mean turns", "min", "max", "mean ticks"],
        rows,
        aligns=["<", ">", ">", ">", ">", ">"],
    )
    print()


def view_matchups(records):
    print("=" * 70)
    print("MATCHUP TYPES")
    print("=" * 70)

    buckets = defaultdict(lambda: {"total": 0, "llm_wins": 0, "draws": 0})
    for r in records:
        key = r["matchup_type"] or "?"
        buckets[key]["total"] += 1
        if r["is_draw"]:
            buckets[key]["draws"] += 1
        elif r["won"]:
            buckets[key]["llm_wins"] += 1

    rows = []
    for k, b in buckets.items():
        rows.append([
            k,
            b["total"],
            b["llm_wins"],
            b["total"] - b["llm_wins"] - b["draws"],
            b["draws"],
            pct(b["llm_wins"], b["total"]),
        ])

    rows.sort(key=lambda r: r[0])
    fmt_table(
        ["matchup", "Total", "LLM W", "LLM L", "Draws", "LLM Win%"],
        rows,
        aligns=["<", ">", ">", ">", ">", ">"],
    )
    print()

def view_level_opponent_matrix(records, **kw):
    print("=" * 110)
    print("LEVEL × OPPONENT MATRIX")
    print("=" * 110)

    # Collect
    buckets = defaultdict(lambda: {
        "w": 0, "l": 0, "d": 0, "combat": [], "dmg_eff": [],
    })
    for r in records:
        level = r.get("map") or "?"
        variant = r.get("opponent_variant") or "?"
        b = buckets[(level, variant)]
        if r["is_draw"]:      b["d"] += 1
        elif r["won"]:        b["w"] += 1
        else:                 b["l"] += 1
        if r.get("combat_score") is not None:
            b["combat"].append(r["combat_score"])
        if r.get("damage_efficiency") is not None:
            b["dmg_eff"].append(r["damage_efficiency"])

    levels   = sorted({k[0] for k in buckets})
    variants = sorted({k[1] for k in buckets})

    # --- W/L matrix ---
    print("\nWin rate by (level × opponent):")
    headers = ["level"] + [f"{v} (n)" for v in variants]
    rows = []
    for lvl in levels:
        row = [lvl]
        for v in variants:
            b = buckets.get((lvl, v))
            if not b:
                row.append("—")
            else:
                n = b["w"] + b["l"] + b["d"]
                p = (100 * b["w"] / n) if n else 0
                row.append(f"{b['w']}/{n} ({p:.0f}%)")
        rows.append(row)
    fmt_table(headers, rows, aligns=["<"] + [">"] * len(variants))
    print()

    # --- Combat score matrix ---
    print("Mean combat score (damage dealt / enemy HP; 1.0 = wiped enemy):")
    rows = []
    for lvl in levels:
        row = [lvl]
        for v in variants:
            b = buckets.get((lvl, v))
            if not b or not b["combat"]:
                row.append("—")
            else:
                row.append(f"{statistics.fmean(b['combat']):.2f}")
        rows.append(row)
    fmt_table(headers, rows, aligns=["<"] + [">"] * len(variants))
    print()

    # --- Damage efficiency matrix ---
    print("Mean damage efficiency (0.5 = even trade; 1.0 = flawless):")
    rows = []
    for lvl in levels:
        row = [lvl]
        for v in variants:
            b = buckets.get((lvl, v))
            if not b or not b["dmg_eff"]:
                row.append("—")
            else:
                row.append(f"{statistics.fmean(b['dmg_eff']):.2f}")
        rows.append(row)
    fmt_table(headers, rows, aligns=["<"] + [">"] * len(variants))
    print()

def view_quality(records, group_by="variant"):
    """Aggregate quality metrics per variant / cohort / model."""
    print("=" * 110)
    print(f"QUALITY METRICS BY {group_by.upper()}  (win/loss-independent skill)")
    print("=" * 110)
    _print_quality_legend()

    def key_fn(r):
        if group_by == "variant":
            return r.get("opponent_variant") or "?"
        if group_by == "cohort":
            return r.get("player_id") or "?"
        if group_by == "model":
            return r.get("model") or "?"
        return "?"

    buckets = _aggregate_quality(records, key_fn)
    rows = [
        _quality_row([key], _summarize_quality_bucket(b))
        for key, b in sorted(buckets.items())
    ]
    fmt_table(
        [group_by, "n", "W%", "combat", "dmgEff", "killRt", "survive", "ttfk"],
        rows,
        aligns=["<", ">", ">", ">", ">", ">", ">", ">"],
    )
    print()

def view_quality_llm_by_model(records, **kw):
    """LLM quality per (variant, model) — which model does best against which AI."""
    print("=" * 120)
    print("LLM QUALITY BY (VARIANT × MODEL)")
    print("=" * 120)
    _print_quality_legend()

    def key_fn(r):
        return (r.get("opponent_variant") or "?",
                r.get("model") or "?")

    buckets = _aggregate_quality(records, key_fn)
    rows = []
    for (variant, model), b in sorted(buckets.items()):
        s = _summarize_quality_bucket(b)
        rows.append(_quality_row([variant, model], s))

    fmt_table(
        ["variant", "model", "n", "W%", "combat", "dmgEff",
         "killRt", "survive", "ttfk"],
        rows,
        aligns=["<", "<", ">", ">", ">", ">", ">", ">", ">"],
    )
    print()

    # Per-model aggregates (collapse variant) for a quick summary
    print("Per-model totals (all variants collapsed):")
    model_buckets = _aggregate_quality(
        records, lambda r: r.get("model") or "?"
    )
    rows = [
        _quality_row([m], _summarize_quality_bucket(b))
        for m, b in sorted(model_buckets.items())
    ]
    fmt_table(
        ["model", "n", "W%", "combat", "dmgEff", "killRt", "survive", "ttfk"],
        rows,
        aligns=["<", ">", ">", ">", ">", ">", ">", ">"],
    )
    print()

def view_quality_comparison(human_recs, llm_recs, **kw):
    """Side-by-side human vs LLM quality, per variant."""
    print("=" * 120)
    print("QUALITY COMPARISON:  HUMANS vs LLMs  (per opponent variant)")
    print("=" * 120)
    _print_quality_legend()
    print("  Δ = LLM − Human. Negative Δ means the LLM performed worse.")
    print()

    h_buckets = _aggregate_quality(
        human_recs, lambda r: r.get("opponent_variant") or "?"
    )
    l_buckets = _aggregate_quality(
        llm_recs, lambda r: r.get("opponent_variant") or "?"
    )

    variants = sorted(set(h_buckets) | set(l_buckets))

    def _fmt_delta(a, b, decimals, pct=False):
        if a is None or b is None:
            return "—"
        d = b - a
        if pct:
            sign = "+" if d >= 0 else "−"
            return f"{sign}{abs(d):.{decimals}f}pp"
        sign = "+" if d >= 0 else "−"
        return f"{sign}{abs(d):.{decimals}f}"

    rows = []
    for variant in variants:
        h = _summarize_quality_bucket(h_buckets.get(variant, _empty_quality_bucket()))
        l = _summarize_quality_bucket(l_buckets.get(variant, _empty_quality_bucket()))

        rows.append([
            variant, "n",
            str(h["n"]) if h["n"] else "—",
            str(l["n"]) if l["n"] else "—",
            f"+{l['n'] - h['n']}" if h["n"] and l["n"] else "—",
        ])
        rows.append([
            variant, "W%",
            f"{h['win_pct']:.0f}%" if h["n"] else "—",
            f"{l['win_pct']:.0f}%" if l["n"] else "—",
            _fmt_delta(h["win_pct"] if h["n"] else None,
                       l["win_pct"] if l["n"] else None, 1, pct=True),
        ])
        for metric in ["combat", "dmg_eff", "kill_ratio", "survival", "ttfk"]:
            rows.append([
                variant, metric,
                f"{h[metric]:.2f}" if h[metric] is not None else "—",
                f"{l[metric]:.2f}" if l[metric] is not None else "—",
                _fmt_delta(h[metric], l[metric], 2),
            ])
        rows.append(["", "", "", "", ""])  # spacer

    fmt_table(
        ["variant", "metric", "human", "LLM", "Δ"],
        rows,
        aligns=["<", "<", ">", ">", ">"],
    )
    print()

def view_sides(records):
    print("=" * 78)
    print("SIDE SPLIT BY (AU VARIANT × LLM MODEL)")
    print("=" * 78)

    buckets = defaultdict(lambda: {"Red": 0, "Green": 0})
    for r in records:
        variant = r.get("opponent_variant") or "(none)"
        model = r["model"] or "?"
        side = r.get("llm_side")
        key = (variant, model)
        if side in ("Red", "Green"):
            buckets[key][side] += 1

    rows = []
    for (v, m), b in sorted(buckets.items()):
        total = b["Red"] + b["Green"]
        rows.append([v, m, b["Red"], b["Green"], total])

    fmt_table(
        ["AU", "model", "Red-LLM", "Green-LLM", "Total"],
        rows,
        aligns=["<", "<", ">", ">", ">"],
    )
    print()
    print("Red-LLM   = LLM plays Red, AU plays Green.")
    print("Green-LLM = LLM plays Green, AU plays Red.")
    print()

def view_decisions(records):
    """How the LLM did per decision type — placeholder for future use."""
    print("=" * 70)
    print("DECISION REASON BREAKDOWN")
    print("=" * 70)

    buckets = defaultdict(lambda: {"total": 0, "draws": 0, "timeout": 0,
                                    "survivors": 0, "elim": 0})
    for r in records:
        reason = r["win_reason"] or "(none)"
        b = buckets[reason]
        b["total"] += 1
        if reason == "max_turns_no_elimination":
            b["timeout"] += 1
        elif reason == "timeout_survivors":
            b["survivors"] += 1
        elif reason == "timeout_tie":
            b["draws"] += 1
        elif reason == "elimination":
            b["elim"] += 1

    rows = []
    for reason, b in buckets.items():
        rows.append([reason, b["total"]])
    rows.sort(key=lambda r: -r[1])

    fmt_table(["win_reason", "count"], rows, aligns=["<", ">"])
    print()


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

VIEWS = {
    "overview": view_overview,
    "wins": view_wins_by_model,
    "wins-vs": view_wins_by_model_and_opponent,
    "times": view_response_times,
    "lengths": view_battle_lengths,
    "matchups": view_matchups,
    "decisions": view_decisions,
    "variants": view_variants,
    "sides": view_sides,
    "side-results": view_side_results,
    "raw": view_raw,
    "humans": view_humans,
    "human-vs-au": view_human_vs_au,
    "levels": view_levels,
    "human-turns": view_human_turns,
    "human-battles": view_human_per_battle,
    "hard-turns": view_hard_turns,
    "hard-turns-summary": view_hard_turns_quiet,
}
VIEWS.update({
    "deliberation-vs-outcome": view_deliberation_vs_outcome,
    "level-opponent-matrix":   view_level_opponent_matrix,
    "quality":                 view_quality,          # by variant
    "quality-by-cohort":       lambda r, **kw: view_quality(r, group_by="cohort"),
})
VIEWS.update({
    "quality-llm": view_quality,   # not in human_views → dispatches llm_records
})
VIEWS.update({
    "quality-llm-by-model": view_quality_llm_by_model,
})
VIEWS["quality-comparison"] = lambda *a, **kw: None  # dispatched specially in main()

def _parse_turn_list(value):
    return [int(v.strip()) for v in value.split(",") if v.strip()]

def main():
    ap = argparse.ArgumentParser(description="Summarise a compiled battles JSONL.")
    ap.add_argument("jsonl", help="Path to compiled battles JSONL.")
    ap.add_argument("--view", choices=list(VIEWS.keys()),
                    help="Print a single view. Default: all.")
    ap.add_argument("--map", help="Filter to a single map.")
    ap.add_argument("--model", help="Filter to a single LLM model.")
    ap.add_argument("--prompt", help="Filter to a single prompt.")
    ap.add_argument("--exclude-draws", action="store_true",
                    help="Drop draw records before aggregating.")
    ap.add_argument("--source", help="Filter by source filename (substring).")
    ap.add_argument("--gene",
                    help="Filter by AU variant short name(s), comma-separated "
                         "for multiple, e.g. --gene CA or --gene CA,MD.")
    ap.add_argument("--player", help="Filter by player_id (extraText).")
    ap.add_argument("--matchup", help="Filter by matchup_type.")
    ap.add_argument("--session", help="Filter by session_tag (from extraText).")
    ap.add_argument("--turn", type=int,
                    help="For --view hard-turns: show only this turn.")
    ap.add_argument("--ignore-turn", type=_parse_turn_list, action="append",
                    default=[], metavar="N[,N...]",
                    help="Skip these turn numbers (repeatable; commas allowed). "
                         "Applies to --view hard-turns and --view hard-turns-summary.")
    ap.add_argument("--snap-dur", type=float, default=20.0,
                    help="Snap turn: max duration in seconds (default 20).")
    ap.add_argument("--snap-gap", type=float, default=2.0,
                    help="Snap turn: max commit gap in seconds (default 2).")
    ap.add_argument("--delib-dur", type=float, default=45.0,
                    help="Deliberate turn: min duration in seconds (default 45).")
    ap.add_argument("--delib-gap", type=float, default=10.0,
                    help="Deliberate turn: min max-gap in seconds (default 10).")
    ap.add_argument("--delib-ratio", type=float, default=2.5,
                    help="Deliberate turn: min max/mean gap ratio (default 2.5).")

    args = ap.parse_args()

    # Flatten [ [1,2], [3] ] -> {1, 2, 3}
    ignore_turns = {n for group in args.ignore_turn for n in group}

    battles = load_battles(args.jsonl)
    if not battles:
        print("No battles loaded.")
        return

    # Filter battles first, then extract records from the survivors.
    if args.map:
        battles = [b for b in battles
                   if b.get("summary", {}).get("map") == args.map]

    records = llm_records(battles)

    human_recs = human_records(battles)
    if args.player:
        human_recs = [r for r in human_recs if r.get("player_id") == args.player]
    if args.matchup:
        human_recs = [r for r in human_recs if r.get("matchup_type") == args.matchup]

    if args.session:
        human_recs = [r for r in human_recs
                      if r.get("session_tag") == args.session]

    if args.model:
        records = [r for r in records if r["model"] == args.model]
    if args.prompt:
        records = [r for r in records if r["prompt"] == args.prompt]
    if args.exclude_draws:
        records = [r for r in records if not r["is_draw"]]
    if args.source:
        records = [r for r in records if args.source in (r["source"] or "")]
    if args.gene:
        wanted = {g.strip() for g in args.gene.split(",") if g.strip()}
        records = [
            r for r in records
            if r.get("opponent_variant")
            and wanted & set(r["opponent_variant"].split(";"))
        ]

    human_views = {
        "humans", "human-vs-au", "levels",
        "human-turns", "human-battles", "hard-turns-summary",
        "deliberation-vs-outcome",
        "level-opponent-matrix",
        "quality",
        "quality-by-cohort",
    }

    if args.view:
        if args.view == "quality-comparison":
            view_quality_comparison(human_recs, records)
        elif args.view == "hard-turns":
            view_hard_turns(
                human_recs,
                turn_filter=args.turn,
                ignore_turns=ignore_turns,
                max_gap_ratio=args.delib_ratio,
                min_duration_s=args.delib_dur,
            )
        elif args.view == "hard-turns-summary":
            view_hard_turns_quiet(human_recs, ignore_turns=ignore_turns)
        elif args.view == "human-turns":
            view_human_turns(
                human_recs,
                snap_dur=args.snap_dur, snap_gap=args.snap_gap,
                deliberate_dur=args.delib_dur,
                deliberate_gap=args.delib_gap,
                deliberate_ratio=args.delib_ratio,
            )
        elif args.view in human_views:
            VIEWS[args.view](human_recs)
        elif args.view == "overview":
            view_overview(battles, records)
        else:
            VIEWS[args.view](records)
    else:
        # ---- default bundle ----
        view_overview(battles, records)
        view_wins_by_model(records)
        view_wins_by_model_and_opponent(records)
        view_response_times(records)
        view_battle_lengths(records)
        view_matchups(records)
        view_decisions(records)
        if human_recs:
            view_humans(human_recs)
            view_human_vs_au(human_recs)
            view_levels(human_recs)
            view_human_turns(human_recs)
            view_quality_comparison(human_recs, records)
            view_quality_llm_by_model(records)

if __name__ == "__main__":
    main()