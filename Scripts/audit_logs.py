#!/usr/bin/env python3
"""
audit_logs.py — report, filter, and optionally list-for-deletion stale battleground logs.

Usage:
  # Report only — shows prompt distribution, turn-count issues, per-model counts,
  # and duplicate source_file entries (the usual cause of non-×16 counts).
  python3 audit_logs.py alllogs.jsonl

  # Produce a filtered JSONL (drop old prompts + zero-turn records)
  python3 audit_logs.py alllogs.jsonl --keep-prompt Prompt_4a --write-clean clean.jsonl

  # Also produce a list of raw log filenames that were excluded
  python3 audit_logs.py alllogs.jsonl --keep-prompt Prompt_4a --to-delete delete_list.txt
"""

import argparse
import json
import sys
from collections import Counter, defaultdict


# ---------- helpers ----------

def _is_human_battle(s):
    roles = s.get("faction_roles") or {}
    return any(r == "Human" for r in roles.values())


def _get_prompt(s):
    llm = s.get("llm_side")
    if not llm:
        return None
    return (s.get("faction_prompts") or {}).get(llm)


def _get_model(s):
    llm = s.get("llm_side")
    if not llm:
        return None
    return (s.get("faction_models") or {}).get(llm)


def load_records(path):
    out = []
    with open(path, "r", encoding="utf-8-sig") as f:
        for i, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                out.append(json.loads(line))
            except json.JSONDecodeError as exc:
                print(f"[warn] skipping line {i}: {exc}", file=sys.stderr)
    return out


# ---------- report ----------

def report(records):
    total = len(records)
    human_battles = sum(1 for r in records if _is_human_battle(r["summary"]))
    llm_battles = total - human_battles

    print("=" * 100)
    print("AUDIT REPORT")
    print("=" * 100)
    print(f"  Total records:      {total}")
    print(f"  Human battles:      {human_battles}")
    print(f"  LLM battles:        {llm_battles}")
    print()

    # ---- Prompt distribution ----
    prompt_counts = Counter()
    for r in records:
        s = r["summary"]
        if _is_human_battle(s):
            prompt_counts["(human, no LLM prompt)"] += 1
        else:
            prompt_counts[_get_prompt(s) or "(no prompt field)"] += 1
    print("  Prompt distribution:")
    for p, n in prompt_counts.most_common():
        print(f"    {p:<32}  {n:>4}")
    print()

    # ---- Turn counts ----
    zero_turn = [r for r in records if (r["summary"].get("final_turn") or 0) < 1]
    print(f"  Records with final_turn < 1 (corrupt/truncated):  {len(zero_turn)}")
    if zero_turn:
        by_src = Counter(r.get("source_file") or "?" for r in zero_turn)
        for src, n in by_src.most_common(10):
            print(f"    {src}  ×{n}")
        if len(by_src) > 10:
            print(f"    ... and {len(by_src) - 10} more")
    print()

    # ---- Duplicates ----
    src_counter = Counter(r.get("source_file") or "?" for r in records)
    dupes = {k: v for k, v in src_counter.items() if v > 1}
    if dupes:
        print(f"  DUPLICATE source_file entries  ({len(dupes)} files):")
        for src, n in sorted(dupes.items())[:15]:
            print(f"    {src}  appears {n}×")
        if len(dupes) > 15:
            print(f"    ... and {len(dupes) - 15} more")
    else:
        print("  No duplicate source_file entries.")
    print()

    # ---- Per-model × per-prompt counts ----
    print("  Per-model LLM battle counts (× = prompt breakdown):")
    model_total = Counter()
    model_prompts = defaultdict(Counter)
    for r in records:
        s = r["summary"]
        if _is_human_battle(s):
            continue
        m = _get_model(s) or "?"
        p = _get_prompt(s) or "(no prompt)"
        model_total[m] += 1
        model_prompts[m][p] += 1

    for model, n in model_total.most_common():
        batches = n // 16
        rem = n % 16
        suffix = f"  (+{rem} extra)" if rem else "  ✓ multiple of 16"
        print(f"    {model:<32}  {n:>4}  = {batches}×16{suffix}")
        if len(model_prompts[model]) > 1:
            for p, pn in model_prompts[model].most_common():
                marker = "  ✓" if pn % 16 == 0 else ""
                print(f"        └─ {p:<24}  {pn:>4}{marker}")
    print()


# ---------- filter ----------

def filter_records(records, keep_prompts, exclude_models=None, drop_zero_turn=True):
    exclude_set = set(exclude_models or [])
    kept, dropped = [], []
    for r in records:
        s = r["summary"]
        if drop_zero_turn and (s.get("final_turn") or 0) < 1:
            dropped.append(r); continue
        if _is_human_battle(s):
            kept.append(r); continue
        if keep_prompts and _get_prompt(s) not in keep_prompts:
            dropped.append(r); continue
        if _get_model(s) in exclude_set:
            dropped.append(r); continue
        kept.append(r)
    return kept, dropped


def write_jsonl(records, path):
    with open(path, "w", encoding="utf-8") as f:
        for r in records:
            f.write(json.dumps(r, ensure_ascii=False) + "\n")
    print(f"  Wrote {len(records)} records to {path}")


def write_delete_list(dropped, path):
    seen = sorted({r.get("source_file") for r in dropped if r.get("source_file")})
    with open(path, "w", encoding="utf-8") as f:
        for src in seen:
            f.write(src + "\n")
    print(f"  Wrote {len(seen)} source filenames to {path}")


# ---------- main ----------

def main():
    ap = argparse.ArgumentParser(description="Audit and clean up battleground logs.")
    ap.add_argument("jsonl", help="Path to compiled battles JSONL")
    ap.add_argument("--keep-prompt", nargs="*", default=[],
                    help="Keep only LLM records using these prompts. "
                         "Human records are always kept.")
    ap.add_argument("--keep-zero-turn", action="store_true",
                    help="Do not drop zero-turn records (for debugging)")
    ap.add_argument("--write-clean", metavar="PATH",
                    help="Write filtered records to this JSONL file")
    ap.add_argument("--to-delete", metavar="PATH",
                    help="Write source_file names of dropped records to this file")
    ap.add_argument("--exclude-model", nargs="*", default=[],
                    help="Drop records from these models entirely.")
    args = ap.parse_args()

    records = load_records(args.jsonl)
    if not records:
        print("No records loaded.")
        return

    report(records)

    if args.write_clean or args.to_delete:
        keep_set = set(args.keep_prompt) if args.keep_prompt else set()
        kept, dropped = filter_records(
            records, keep_set,
            exclude_models=args.exclude_model,
            drop_zero_turn=not args.keep_zero_turn,
        )
        print(f"  Filter summary: kept {len(kept)}, dropped {len(dropped)}")
        if args.write_clean:
            write_jsonl(kept, args.write_clean)
        if args.to_delete:
            write_delete_list(dropped, args.to_delete)


if __name__ == "__main__":
    main()