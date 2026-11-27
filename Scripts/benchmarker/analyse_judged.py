#!/usr/bin/env python3
"""
analyse_judged.py — aggregate judged JSONL records into a model x prompt
report: rubric means per dimension, validity rate, token/timing cost, and
optional pairwise wins.

Usage:
    python analyse_judged.py <judged.jsonl> [judged_pairwise.jsonl ...]
"""

import json
import os
import sys

DIMENSIONS = ["rule_adherence", "target_quality", "board_awareness",
              "movement_economy", "collaboration"]


def mean(values):
    return sum(values) / len(values) if values else None


def fmt(v, nd=2):
    return f"{v:.{nd}f}" if v is not None else "—"


def load(path):
    rows = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                rows.append(json.loads(line))
    return rows


def main():
    if len(sys.argv) < 2:
        print("usage: analyse_judged.py <judged.jsonl> [judged_pairwise.jsonl ...]")
        return 1

    records = load(sys.argv[1])
    pairwise_files = sys.argv[2:]
    pairwise = []
    for path in pairwise_files:
        pairwise.extend(load(path))

    # ---- group ALL records (judged or not) per model x prompt
    cells = {}
    for record in records:
        key = (record.get("model", "?"), record.get("prompt_file", "?"))
        cell = cells.setdefault(key, {"total": 0, "ok": 0, "judged": 0,
                                       "overalls": [], "dims": {d: [] for d in DIMENSIONS},
                                       "out_tokens": [], "gen_times": []})
        cell["total"] += 1
        if record.get("verdict") == "OK":
            cell["ok"] += 1
            j = record.get("judgment")
            if isinstance(j, dict) and j.get("verdict") == "OK":
                cell["judged"] += 1
                try:
                    cell["overalls"].append(int(j["overall"]))
                    for d in DIMENSIONS:
                        cell["dims"][d].append(int(j["scores"][d]))
                except (KeyError, TypeError, ValueError):
                    pass
        # efficiency columns come from all records that produced an answer
        if record.get("output_tokens"):
            cell["out_tokens"].append(record["output_tokens"])
        if record.get("gen_time_s") is not None:
            cell["gen_times"].append(record["gen_time_s"])

    models  = sorted({m for m, _ in cells})
    prompts = sorted({p for _, p in cells})
    print()
    print("=" * 100)
    print(f"JUDGED RESULTS — {len(models)} models x {len(prompts)} prompts "
          f"({len(records)} runs, {sum(c['judged'] for c in cells.values())} judged)")
    print("=" * 100)
    hdr = (f"  {'model':<26} {'prompt':<22} {'n':>3} {'ok%':>5} {'jud':>4} "
           f"{'overall':>7} " + " ".join(f"{d[:5]:>6}" for d in DIMENSIONS)
           + f" {'out_tok':>8} {'gen_s':>6}")
    print(hdr)
    print("  " + "-" * (len(hdr) - 4))
    for (model, prompt), c in sorted(cells.items()):
        ok_rate = 100 * c["ok"] / c["total"] if c["total"] else 0
        print(f"  {model:<26} {os.path.basename(prompt):<22} {c['total']:>3} "
              f"{ok_rate:>4.0f}% {c['judged']:>4} "
              f"{fmt(mean(c['overalls']), 1):>7} "
              + " ".join(fmt(mean(c['dims'][d]), 1) for d in DIMENSIONS)
              + f" {fmt(mean(c['out_tokens']), 0):>8} {fmt(mean(c['gen_times']), 2):>6}")

    if pairwise:
        wins, games = {}, {}
        for r in pairwise:
            for side, key in (("A", "model_a"), ("B", "model_b")):
                model = r.get(key)
                if model:
                    games[model] = games.get(model, 0) + 1
                    if r.get("winner") == side:
                        wins[model] = wins.get(model, 0) + 1
        print()
        print("=" * 100)
        print(f"PAIRWISE (judge preference between close answers, {len(pairwise)} comparisons)")
        print("=" * 100)
        print(f"  {'model':<28} {'wins':>5} {'played':>7} {'win%':>6}")
        print("  " + "-" * 50)
        for model in sorted(games):
            w = wins.get(model, 0)
            print(f"  {model:<28} {w:>5} {games[model]:>7} "
                  f"{(100 * w / games[model]):>5.0f}%")
        print("=" * 100)
    return 0


if __name__ == "__main__":
    sys.exit(main())