#!/usr/bin/env python3
"""Grouped bar charts of LLM generation latency per model x prompt.

Reads a benchmarker results JSONL, groups latency (gen_time_s) per
(model, prompt), and draws two figures: one panel per prompt, and one
grid with models on the x-axis. Bar = mean, whiskers = central 95%
(2.5th-97.5th percentile), white dash = median. Writes the figures and
a latency_stats.csv next to the results file.

Usage:
    python3 latency_charts.py <results.jsonl>
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
    print("usage: python3 latency_charts.py <results.jsonl>")
    sys.exit(1)
RESULTS = sys.argv[1]
OUT_DIR = Path(RESULTS).resolve().parent  # write outputs next to the input

# ---- load and group
cells = defaultdict(list)
with open(RESULTS, encoding="utf-8") as f:
    for line in f:
        line = line.strip()
        if not line:
            continue
        r = json.loads(line)
        if r.get("gen_time_s") is not None:
            prompt = r.get("prompt_file") or r.get("run") or "?"
            cells[(r.get("model", "?"), prompt)].append(float(r["gen_time_s"]))

models = sorted({m for m, _ in cells})
prompts = sorted({p for _, p in cells})

stats = {}
for (m, p), vals in cells.items():
    a = np.asarray(vals)
    stats[(m, p)] = {
        "n": len(a),
        "mean": a.mean(), "std": a.std(ddof=1) if len(a) > 1 else 0.0,
        "median": float(np.median(a)), "min": a.min(), "max": a.max(),
        # central 95% range: 2.5th to 97.5th percentile
        "lo95": float(np.percentile(a, 2.5)),
        "hi95": float(np.percentile(a, 97.5)),
    }

# CSV export for reuse (R or other charts)
csv_path = OUT_DIR / "latency_stats.csv"
with open(csv_path, "w", encoding="utf-8") as f:
    f.write("model,prompt,n,mean_s,std_s,median_s,lo95_s,hi95_s,min_s,max_s\n")
    for (m, p) in sorted(stats):
        s = stats[(m, p)]
        f.write(f"{m},{p},{s['n']},{s['mean']:.3f},{s['std']:.3f},"
                f"{s['median']:.3f},{s['lo95']:.3f},{s['hi95']:.3f},"
                f"{s['min']:.3f},{s['max']:.3f}\n")
print(f"wrote {csv_path}")

# ---- plot: one subplot per prompt, models on the x-axis
fig, axes = plt.subplots(1, len(prompts), figsize=(4.2 * len(prompts), 5.5),
                         sharey=True, squeeze=False)
colors = plt.cm.tab10(np.linspace(0, 1, max(len(models), 2)))

for ax, p in zip(axes[0], prompts):
    xs = np.arange(len(models))
    means = [stats.get((m, p), {}).get("mean", np.nan) for m in models]
    los = [stats.get((m, p), {}).get("lo95", np.nan) for m in models]
    his = [stats.get((m, p), {}).get("hi95", np.nan) for m in models]
    meds = [stats.get((m, p), {}).get("median", np.nan) for m in models]

    bars = ax.bar(xs, means, width=0.62, color=colors,
                  edgecolor="black", linewidth=0.6)
    # central 95% whiskers (2.5th-97.5th percentile)
    ax.vlines(xs, los, his, color="black", linewidth=1.4, zorder=5)
    for ys in (los, his):
        ax.hlines(ys, xs - 0.18, xs + 0.18, color="black",
                  linewidth=1.4, zorder=5)
    # median marker
    ax.scatter(xs, meds, marker="_", s=260, color="white", linewidth=2.0,
               zorder=6, label="median")

    ax.set_xticks(xs)
    ax.set_xticklabels(models, rotation=45, ha="right", fontsize=9)
    ax.set_title(f"{p}", fontsize=12)
    ax.grid(axis="y", alpha=0.3)
    ax.set_axisbelow(True)

axes[0][0].set_ylabel("generation time (s)", fontsize=11)
legend_labels = models + ["95% range", "median"]
legend_handles = [plt.Rectangle((0, 0), 1, 1, edgecolor="black",
                                facecolor=plt.cm.tab10(i / max(len(models) - 1, 1)))
                  for i in range(len(models))]
legend_handles += [plt.Line2D([0], [0], color="black", lw=1.4),
                   plt.Line2D([0], [0], marker="_", color="black", lw=2.0,
                              markersize=12)]
fig.legend(legend_handles, legend_labels, loc="upper center",
           ncol=min(len(legend_labels), 9), frameon=False, fontsize=9,
           bbox_to_anchor=(0.5, 1.0))
fig.suptitle("LLM generation latency per model and prompt "
             "(bar = mean, whiskers = central 95%, dash = median)",
             y=1.10, fontsize=13)
fig.tight_layout(rect=(0, 0, 1, 0.97))
fig_path = OUT_DIR / "latency_by_prompt.png"
fig.savefig(fig_path, dpi=200, bbox_inches="tight")
plt.close(fig)
print(f"wrote {fig_path}")

# ---- plot 2: models on the x-axis, one bar per prompt (ENIAC-style grid)
fig, ax = plt.subplots(figsize=(13, 6))
width = 0.16
xs = np.arange(len(models))
pcolors = plt.cm.viridis(np.linspace(0.1, 0.9, len(prompts)))
for i, p in enumerate(prompts):
    means = [stats.get((m, p), {}).get("mean", np.nan) for m in models]
    los = [stats.get((m, p), {}).get("lo95", np.nan) for m in models]
    his = [stats.get((m, p), {}).get("hi95", np.nan) for m in models]
    pos = xs + (i - (len(prompts) - 1) / 2) * width
    ax.bar(pos, means, width=width * 0.9, color=pcolors[i],
           edgecolor="black", linewidth=0.5, label=p)
    ax.vlines(pos, los, his, color="black", linewidth=1.0, zorder=5)
    for ys in (los, his):
        ax.hlines(ys, pos - 0.05, pos + 0.05, color="black",
                  linewidth=1.0, zorder=5)

ax.set_xticks(xs)
ax.set_xticklabels(models, rotation=30, ha="right", fontsize=10)
ax.set_ylabel("generation time (s)", fontsize=11)
ax.set_title("Latency per model per prompt (bar = mean, whiskers = central 95%)",
             fontsize=13)
ax.legend(title="prompt", fontsize=9, ncol=len(prompts))
ax.grid(axis="y", alpha=0.3)
ax.set_axisbelow(True)
fig.tight_layout()
fig2_path = OUT_DIR / "latency_by_model.png"
fig.savefig(fig2_path, dpi=200)
plt.close(fig)
print(f"wrote {fig2_path}")