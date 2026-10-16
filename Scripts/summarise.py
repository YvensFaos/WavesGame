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


def fmt_ci(wins, total):
    """Format a Wilson CI as 'lo-hi' in percentage points, e.g. '30.4-59.3'."""
    if not total:
        return "—"
    lo, hi = wilson_interval(total, wins)
    return f"{lo * 100:.1f}-{hi * 100:.1f}"

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
}

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
    args = ap.parse_args()

    battles = load_battles(args.jsonl)
    if not battles:
        print("No battles loaded.")
        return

    # Filter battles first, then extract records from the survivors.
    if args.map:
        battles = [b for b in battles
                   if b.get("summary", {}).get("map") == args.map]

    records = llm_records(battles)

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

    if args.view:
        if args.view == "overview":
            view_overview(battles, records)
        else:
            VIEWS[args.view](records)
    else:
        view_overview(battles, records)
        view_wins_by_model(records)
        view_wins_by_model_and_opponent(records)
        view_response_times(records)
        view_battle_lengths(records)
        view_matchups(records)
        view_decisions(records)


if __name__ == "__main__":
    main()