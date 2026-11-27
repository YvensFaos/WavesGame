#!/usr/bin/env python3
"""Validity and quality charts for benchmarker results: a 2x2 grid.

Reads a benchmarker results JSONL and writes, next to the input file:

  validity_2x2.png    - 2x2 grid, per model x prompt:
                          (1) strict validity rate (any OK)
                          (2) active/passive/invalid split
                          (3) error taxonomy per model (all prompts pooled)
                          (4) mean output tokens
  validity_stats.csv  - one row per model x prompt with all numbers

Rules:
  - SERVER_DOWN records are excluded from every chart and from the
    denominator of every rate (infrastructure failure, not reasoning).
  - TIMEOUT counts as an invalid action (and as its own taxonomy class).
  - "Any OK" = OK-active + OK-passive (strict validity).

Usage:
    python3 validity_charts.py <results.jsonl>
"""
import json
import sys
from collections import defaultdict
from pathlib import Path

import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

if len(sys.argv) < 2:
    print("usage: python3 validity_charts.py <results.jsonl>")
    sys.exit(1)
RESULTS = sys.argv[1]
OUT_DIR = Path(RESULTS).resolve().parent  # write outputs next to the input

EXCLUDED = {"SERVER_DOWN"}  # not counted anywhere


def classify(record):
    """Return (verdict_class, error_class) for one record."""
    v = record.get("verdict", "?")
    if v in ("OK",):
        kind = record.get("ok_kind") or "passive"
        return ("OK", "ok-active" if kind == "active" else "ok-passive")
    if v == "TIMEOUT":
        return ("INVALID", "timeout")
    if v == "INVALID_JSON":
        return ("INVALID", "invalid-json")
    issues = " ".join(record.get("issues") or [])
    if v == "INVALID_ATTACK":
        if "DEAD ZONE" in issues:
            return ("INVALID", "attack: dead zone")
        if "own tile" in issues:
            return ("INVALID", "attack: own tile")
        return ("INVALID", "attack: out of range")
    if v == "INVALID_MOVE":
        if "not in the PTNF movement list" in issues:
            return ("INVALID", "move: not in list")
        if "steps but only" in issues:
            return ("INVALID", "move: steps exceeded")
        return ("INVALID", "move: other")
    return ("INVALID", "other")


# ---- load and group
cells = defaultdict(list)   # (model, prompt) -> list of (verdict_class, err, out_tokens)
with open(RESULTS, encoding="utf-8") as f:
    for line in f:
        line = line.strip()
        if not line:
            continue
        r = json.loads(line)
        if r.get("verdict") in EXCLUDED:
            continue
        prompt = r.get("prompt_file") or r.get("run") or "?"
        verdict_class, err_class = classify(r)
        cells[(r.get("model", "?"), prompt)].append(
            (verdict_class, err_class, r.get("output_tokens") or 0))

models = sorted({m for m, _ in cells})
prompts = sorted({p for _, p in cells})

# ---- per model x prompt stats
stats = {}
for (m, p), runs in cells.items():
    n = len(runs)
    ok = [r for r in runs if r[0] == "OK"]
    active = [r for r in ok if r[1] == "ok-active"]
    passive = [r for r in ok if r[1] == "ok-passive"]
    stats[(m, p)] = {
        "n": n,
        "ok_rate": 100 * len(ok) / n,
        "active_rate": 100 * len(active) / n,
        "passive_rate": 100 * len(passive) / n,
        "invalid_rate": 100 * (n - len(ok)) / n,
        "out_tokens_mean": np.mean([r[2] for r in runs]),
    }

# ---- error taxonomy per model (all prompts pooled)
ERROR_ORDER = ["attack: out of range", "attack: dead zone", "attack: own tile",
               "move: not in list", "move: steps exceeded", "move: other",
               "invalid-json", "timeout", "other"]
errors_by_model = {m: defaultdict(int) for m in models}
for (m, p), runs in cells.items():
    for verdict_class, err_class, _ in runs:
        if verdict_class == "INVALID":
            errors_by_model[m][err_class] += 1

# ---- CSV export
csv_path = OUT_DIR / "validity_stats.csv"
with open(csv_path, "w", encoding="utf-8") as f:
    f.write("model,prompt,n,ok_rate,active_rate,passive_rate,invalid_rate,"
            "out_tokens_mean\n")
    for (m, p) in sorted(stats):
        s = stats[(m, p)]
        f.write(f"{m},{p},{s['n']},{s['ok_rate']:.1f},{s['active_rate']:.1f},"
                f"{s['passive_rate']:.1f},{s['invalid_rate']:.1f},"
                f"{s['out_tokens_mean']:.1f}\n")
print(f"wrote {csv_path}")

# ---- 2x2 grid
fig, axes = plt.subplots(2, 2, figsize=(16, 11))
pcolors = plt.cm.viridis(np.linspace(0.1, 0.9, len(prompts)))


def grouped(ax, values, ylabel, title, ymax=None):
    width = min(0.16, 0.8 / max(len(prompts), 1))
    xs = np.arange(len(models))
    for i, p in enumerate(prompts):
        vals = [values.get((m, p), np.nan) for m in models]
        pos = xs + (i - (len(prompts) - 1) / 2) * width
        ax.bar(pos, vals, width=width * 0.9, color=pcolors[i],
               edgecolor="black", linewidth=0.5, label=p)
    ax.set_xticks(xs)
    ax.set_xticklabels(models, rotation=30, ha="right", fontsize=9)
    ax.set_ylabel(ylabel, fontsize=10)
    ax.set_title(title, fontsize=12)
    ax.grid(axis="y", alpha=0.3)
    ax.set_axisbelow(True)
    if ymax:
        ax.set_ylim(0, ymax)
    ax.legend(fontsize=8, ncol=min(len(prompts), 5))


# (1) strict validity: any OK
okvals = {k: v["ok_rate"] for k, v in stats.items()}
grouped(axes[0][0], okvals, "% of runs (any OK)",
        "Strict validity: any OK (active + passive), SERVER_DOWN excluded",
        ymax=105)

# (2) active / passive / invalid split (stacked shares per model x prompt)
ax = axes[0][1]
width = min(0.16, 0.8 / max(len(prompts), 1))
xs = np.arange(len(models))
share_colors = {"active": "#4caf50", "passive": "#bdbdbd", "invalid": "#e53935"}
for i, p in enumerate(prompts):
    act = [stats.get((m, p), {}).get("active_rate", 0) for m in models]
    pas = [stats.get((m, p), {}).get("passive_rate", 0) for m in models]
    inv = [stats.get((m, p), {}).get("invalid_rate", 0) for m in models]
    pos = xs + (i - (len(prompts) - 1) / 2) * width
    ax.bar(pos, act, width * 0.9, color=share_colors["active"],
           edgecolor="black", linewidth=0.5, label="prompt " + p)
    ax.bar(pos, pas, width * 0.9, bottom=act, color=share_colors["passive"],
           edgecolor="black", linewidth=0.5)
    ax.bar(pos, inv, width * 0.9, bottom=[a + b for a, b in zip(act, pas)],
           color=share_colors["invalid"], edgecolor="black", linewidth=0.5)
ax.set_xticks(xs)
ax.set_xticklabels(models, rotation=30, ha="right", fontsize=9)
ax.set_ylabel("% of runs", fontsize=10)
ax.set_ylim(0, 105)
ax.set_title("Action outcome split: active (green) / passive (grey) / "
             "invalid (red)", fontsize=12)
ax.legend(fontsize=8, ncol=min(len(prompts), 5))
ax.grid(axis="y", alpha=0.3)
ax.set_axisbelow(True)

# (3) error taxonomy per model, prompts pooled (stacked counts)
ax = axes[1][0]
xs = np.arange(len(models))
bottoms = np.zeros(len(models))
taxonomy_colors = plt.cm.tab20(np.linspace(0, 1, len(ERROR_ORDER)))
present = [e for e in ERROR_ORDER
           if any(errors_by_model[m][e] > 0 for m in models)]
for j, err in enumerate(present):
    vals = np.array([errors_by_model[m][err] for m in models], dtype=float)
    ax.bar(xs, vals, 0.6, bottom=bottoms, color=taxonomy_colors[j],
           edgecolor="black", linewidth=0.5, label=err)
    bottoms += vals
ax.set_xticks(xs)
ax.set_xticklabels(models, rotation=30, ha="right", fontsize=9)
ax.set_ylabel("invalid runs (count, all prompts pooled)", fontsize=10)
ax.set_title("Error taxonomy per model", fontsize=12)
ax.legend(fontsize=8)
ax.grid(axis="y", alpha=0.3)
ax.set_axisbelow(True)

# (4) mean output tokens per model x prompt
tokvals = {k: v["out_tokens_mean"] for k, v in stats.items()}
grouped(axes[1][1], tokvals, "mean output tokens",
        "Response size: mean output tokens", ymax=None)

fig.suptitle("Validity and quality per model x prompt "
             "(SERVER_DOWN excluded, TIMEOUT counts as invalid)",
             fontsize=14)
fig.tight_layout(rect=(0, 0, 1, 0.96))
fig_path = OUT_DIR / "validity_2x2.png"
fig.savefig(fig_path, dpi=200, bbox_inches="tight")
plt.close(fig)
print(f"wrote {fig_path}")