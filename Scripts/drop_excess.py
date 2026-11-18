#!/usr/bin/env python3
"""
drop_excess.py — stratified subsampling of LLM battle logs to a per-model target.

Produces two lists of source filenames:
  <keep>  — every source_file that should remain in the working folder
  <drop>  — every source_file that should be moved to backup

Stratification ensures the sample is balanced on the axes that matter:
  level,variant,faction  (8 cells) → equal Base/Small, HA/MD, Red/Green
  level,variant          (4 cells) → equal Base/Small and HA/MD
  level                  (2 cells) → equal Base/Small
  variant                (2 cells) → equal HA/MD

Only LLM records are downsampled. Human records are untouched (never in
the drop list). Cells that contain fewer battles than their share are
kept entirely; models that end below target are flagged in the summary.

Usage:
  python3 drop_excess.py alllogs.jsonl --keep keep_80.txt --drop drop_excess.txt
  python3 drop_excess.py alllogs.jsonl --keep keep.txt --drop drop.txt --dry-run
  python3 drop_excess.py alllogs.jsonl --keep keep.txt --drop drop.txt \\
      --target-per-model 80 --cell-stratification level,variant,faction \\
      --seed 20261118
  python3 drop_excess.py alllogs.jsonl --keep keep.txt --drop drop.txt \\
      --only-models ministral-3:8b,gpt-oss:20b
  python3 drop_excess.py alllogs.jsonl --keep keep.txt --drop drop.txt \\
      --exclude-models mistral-small3.2:24b
"""

import argparse
import json
import random
import sys
from collections import defaultdict


# ---------------------------------------------------------------------------
# Loading + record helpers
# ---------------------------------------------------------------------------

def load_records(path):
    out = []
    with open(path, 'r', encoding='utf-8-sig') as f:
        for i, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                out.append(json.loads(line))
            except json.JSONDecodeError as exc:
                print(f'[warn] skipping line {i}: {exc}', file=sys.stderr)
    return out


def is_llm(summary):
    return bool(summary.get('llm_side'))


def model_of(summary):
    llm = summary.get('llm_side')
    if not llm:
        return None
    return (summary.get('faction_models') or {}).get(llm) or '(unknown)'


def cell_key(summary, strategy):
    level = summary.get('map') or '?'
    llm = summary.get('llm_side')
    if not llm:
        return None

    roles = summary.get('faction_roles') or {}
    opponent = next((f for f in roles if f != llm), None)
    variant = (summary.get('faction_variants') or {}).get(opponent) or '?'
    faction = llm

    if strategy == 'level,variant,faction':
        return (level, variant, faction)
    if strategy == 'level,variant':
        return (level, variant)
    if strategy == 'level':
        return (level,)
    if strategy == 'variant':
        return (variant,)
    raise ValueError(f'unknown stratification: {strategy}')


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

NUM_CELLS = {
    'level,variant,faction': 8,
    'level,variant': 4,
    'level': 2,
    'variant': 2,
}


def main():
    ap = argparse.ArgumentParser(
        description='Stratified subsample of LLM battle logs to a per-model target.'
    )
    ap.add_argument('jsonl', help='Input JSONL file')
    ap.add_argument('--keep', required=True,
                    help='Output: source_file names to keep')
    ap.add_argument('--drop', required=True,
                    help='Output: source_file names to move to backup')
    ap.add_argument('--target-per-model', type=int, default=80,
                    help='Target battles per model (default 80)')
    ap.add_argument('--cell-stratification',
                    choices=list(NUM_CELLS.keys()),
                    default='level,variant,faction',
                    help='What counts as a cell. Default level,variant,faction '
                         'so that Base/Small, HA/MD and Red/Green are all balanced.')
    ap.add_argument('--seed', type=int, default=20261118,
                    help='Random seed (default 20261118)')
    ap.add_argument('--only-models', default=None,
                    help='Comma-separated models to downsample; others untouched')
    ap.add_argument('--exclude-models', default=None,
                    help='Comma-separated models to leave untouched')
    ap.add_argument('--dry-run', action='store_true',
                    help='Print the plan but do not write files')
    args = ap.parse_args()

    records = load_records(args.jsonl)
    if not records:
        print('No records loaded.', file=sys.stderr)
        return 1

    llm = [r for r in records if 'summary' in r and is_llm(r['summary'])]
    if not llm:
        print('No LLM records found.', file=sys.stderr)
        return 1

    if args.only_models:
        wanted = {m.strip() for m in args.only_models.split(',') if m.strip()}
        llm = [r for r in llm if model_of(r['summary']) in wanted]
        print(f'Restricted to models: {sorted(wanted)}')

    if args.exclude_models:
        excluded = {m.strip() for m in args.exclude_models.split(',') if m.strip()}
        llm = [r for r in llm if model_of(r['summary']) not in excluded]
        print(f'Excluded models: {sorted(excluded)}')

    # Group by (model, cell)
    groups = defaultdict(list)
    for r in llm:
        s = r['summary']
        groups[(model_of(s), cell_key(s, args.cell_stratification))].append(r)

    # Compute per-cell target
    num_cells = NUM_CELLS[args.cell_stratification]
    target_cell = max(1, args.target_per_model // num_cells)

    # Which models are over target?
    model_totals = defaultdict(int)
    for (m, _), g in groups.items():
        model_totals[m] += len(g)
    over  = {m for m, n in model_totals.items() if n > args.target_per_model}
    under = set(model_totals) - over

    print()
    print(f'Target per model: {args.target_per_model}')
    print(f'Stratification:   {args.cell_stratification} ({num_cells} cells)')
    print(f'Target per cell:  {target_cell}')
    print(f'Models over:      {sorted(over) or "—"}')
    print(f'Models under:     {sorted(under) or "—"}')

    rng = random.Random(args.seed)
    keep, drop = [], []
    plan = defaultdict(lambda: defaultdict(dict))

    for (model, cell), group in groups.items():
        before = len(group)
        shuffled = list(group)
        rng.shuffle(shuffled)

        if model in under:
            kept_here = shuffled
        else:
            kept_here = shuffled[:target_cell]
        dropped_here = shuffled[len(kept_here):]

        keep.extend(r['source_file'] for r in kept_here)
        drop.extend(r['source_file'] for r in dropped_here)
        plan[model][cell] = {'before': before,
                             'keep':   len(kept_here),
                             'drop':   len(dropped_here)}

    # ---- Summary ----
    print()
    print('=' * 110)
    print('DOWNSAMPLE PLAN')
    print('=' * 110)
    print(f'  seed: {args.seed}   target/model: {args.target_per_model}   '
          f'target/cell: {target_cell}')
    print()

    all_models = sorted(plan.keys(),
                        key=lambda m: -sum(v['before'] for v in plan[m].values()))
    grand_before = grand_keep = grand_drop = 0
    short_models = []

    for model in all_models:
        cells   = plan[model]
        before  = sum(v['before'] for v in cells.values())
        kept    = sum(v['keep']   for v in cells.values())
        dropped = sum(v['drop']   for v in cells.values())
        grand_before += before
        grand_keep   += kept
        grand_drop   += dropped

        delta = kept - args.target_per_model
        if delta < 0:
            note = f'  ⚠ under by {-delta}'
            short_models.append((model, kept, -delta))
        elif delta == 0:
            note = '  ✓'
        else:
            note = ''

        print(f'  {model:<28}  {before:>4} → {kept:>4}  (drop {dropped}){note}')
        for cell, v in sorted(cells.items()):
            if v['drop'] > 0:
                cstr = ','.join(str(x) for x in cell) if isinstance(cell, tuple) else str(cell)
                print(f'      {cstr:<45}  {v["before"]:>3} → {v["keep"]:>3}  '
                      f'(drop {v["drop"]})')

    print()
    print(f'  TOTAL  keep={grand_keep}  drop={grand_drop}  of {grand_before}')

    if short_models:
        print()
        print('  ⚠ Models ending below target (some cells lack battles):')
        for m, k, d in short_models:
            print(f'      {m:<28}  {k} (short by {d})')

    if args.dry_run:
        print()
        print('Dry run — no files written.')
        return 0

    with open(args.keep, 'w', encoding='utf-8') as f:
        f.write('\n'.join(sorted(keep)) + '\n')
    with open(args.drop, 'w', encoding='utf-8') as f:
        f.write('\n'.join(sorted(drop)) + '\n')

    print()
    print(f'Wrote {len(keep)} entries to {args.keep}')
    print(f'Wrote {len(drop)} entries to {args.drop}')
    return 0


if __name__ == '__main__':
    sys.exit(main())