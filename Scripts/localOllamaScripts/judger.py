#!/usr/bin/env python3
"""
judger.py — LLM-judge pass over batch_prompt_test.py runs.

Reads the JSONL produced by batch_prompt_test.py, and for every answer
sends the game state + the chosen action (blind: no model name, no prompt
label, no timing) to a judge model with a fixed rubric. Optionally also
runs pairwise comparisons between close-scoring answers to the same
prompt state.

The judge NEVER grades what the deterministic validator already grades:
invalid actions/times are counted, not judged.

Usage:
    python judger.py <judge_model> <runs.jsonl> [options]
    python judger.py gpt-oss:20b runs.jsonl --save-jsonl judged.jsonl
    python judger.py gpt-oss:20b runs.jsonl --pairwise --save-jsonl judged.jsonl

Server defaults to 192.168.2.62:11434 (override with --ip and --port).
"""

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.request

from prompt_validator import extract_json

RUBRIC_VERSION = "rubric_v1"

RUBRIC_TEMPLATE = """You are an expert evaluator of decisions in WavesST, a turn-based naval strategy game.
Below is (1) the exact instructions and game state an AI player received this turn, and (2) the action it chose. Score the chosen action on five dimensions, each an integer 1-5 (1 = very poor, 5 = excellent). Judge ONLY from the information present in the state:

1. rule_adherence: was the correct rule in the stated priority order followed, not merely a legal action?
2. target_quality: quality of the attack target (enemy ship best; neutral acceptable; pointless wave/obstacle worst; [-1,-1] is fine when nothing useful is reachable).
3. board_awareness: does the action reflect understanding of the board (enemy positions and health, waves, next-turn setup)?
4. movement_economy: purposeful movement, correct use of [-1,-1] and retreat, no wandering.
5. collaboration: does the action consider wave-propagation risk to allies and avoid sabotaging allied positioning, given the information the state actually provides?

Return ONLY a valid JSON object, no other text:
{{"scores": {{"rule_adherence": n, "target_quality": n, "board_awareness": n, "movement_economy": n, "collaboration": n}}, "overall": n, "rationale": "one or two sentences"}}

=== AI PLAYER'S INSTRUCTIONS AND GAME STATE ===
{game_prompt}

=== CHOSEN ACTION ===
{answer}"""

PAIRWISE_TEMPLATE = """You are an expert evaluator of decisions in WavesST, a turn-based naval strategy game.
Two AI players received the exact same instructions and game state below, and chose actions A and B. Decide which action is the better move overall (tactics, target choice, movement purpose). Do not reward verbosity or reasoning style; judge the action itself.

Return ONLY a valid JSON object, no other text:
{{"winner": "A" or "B", "rationale": "one or two sentences"}}

=== SHARED INSTRUCTIONS AND GAME STATE ===
{game_prompt}

=== ACTION A ===
{answer_a}

=== ACTION B ===
{answer_b}"""


def parse_args():
    ap = argparse.ArgumentParser(
        description="LLM-judge pass over batch_prompt_test.py runs (blind)."
    )
    ap.add_argument("judge_model", help="Model name as in `ollama list` (e.g. gpt-oss:20b).")
    ap.add_argument("runs_jsonl", help="JSONL file produced by batch_prompt_test.py --save-jsonl.")
    ap.add_argument("--ip", default="192.168.2.62", help="Ollama host (default: 192.168.2.62).")
    ap.add_argument("--port", type=int, default=11434, help="Ollama port (default: 11434).")
    ap.add_argument("--prompts-dir", metavar="DIR",
                    help="Folder to resolve prompt files from if the recorded paths are missing.")
    ap.add_argument("--save-jsonl", metavar="FILE", required=True,
                    help="Output JSONL: input records with judgment attached.")
    ap.add_argument("--include-invalid", action="store_true",
                    help="Also judge INVALID_ACTION answers (default: OK only).")
    ap.add_argument("--pairwise", action="store_true",
                    help="Additionally compare answers from different models "
                         "to the same prompt state; adds 'pairwise' wins to the output.")
    ap.add_argument("--close-threshold", type=int, default=1,
                    help="Pairwise gap: compare pairs whose rubric overall differs by <= this (default: 1).")
    ap.add_argument("--temperature", type=float, default=0.0,
                    help="Judge sampling temperature (default: 0).")
    ap.add_argument("--timeout", type=float, default=300.0,
                    help="Per-request timeout in seconds (default: 300).")
    ap.add_argument("--quiet", action="store_true", help="Do not print per-answer progress lines.")
    return ap.parse_args()


def read_runs(path):
    records = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                records.append(json.loads(line))
    return records


def resolve_prompt(record, prompts_dir):
    path = record.get("prompt_file", "")
    candidates = [path]
    if prompts_dir and not os.path.isfile(path):
        candidates.append(os.path.join(prompts_dir, os.path.basename(path)))
    for c in candidates:
        if os.path.isfile(c):
            with open(c, encoding="utf-8") as f:
                return f.read()
    return None


def ask_judge(ip, port, model, prompt, temperature, timeout):
    url = f"http://{ip}:{port}/api/generate"
    payload = {"model": model, "prompt": prompt, "stream": False,
               "options": {"temperature": temperature}}
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data,
                                 headers={"Content-Type": "application/json"},
                                 method="POST")
    start = time.perf_counter()
    try:
        resp = urllib.request.urlopen(req, timeout=timeout)
        body = json.loads(resp.read().decode("utf-8"))
    except (urllib.error.HTTPError, urllib.error.URLError,
            TimeoutError, ConnectionError) as exc:
        return {"verdict": "JUDGE_ERROR", "error": str(exc)}, time.perf_counter() - start
    wall = time.perf_counter() - start
    if body.get("error"):
        return {"verdict": "JUDGE_ERROR", "error": body["error"]}, wall
    answer = body.get("response", "")
    parsed = extract_json(answer)
    if parsed is None:
        return {"verdict": "JUDGE_ERROR",
                "error": "judge did not return valid JSON",
                "raw": answer[:200]}, wall
    out = {"verdict": "OK"}
    out.update(parsed)
    out["judge_tokens"] = body.get("eval_count")
    out["judge_time_s"] = round(wall, 2)
    return out, wall


def valid_scores(judgment):
    if not isinstance(judgment, dict) or judgment.get("verdict") != "OK":
        return False
    scores = judgment.get("scores")
    if not isinstance(scores, dict) or "overall" not in judgment:
        return False
    try:
        all(1 <= int(v) <= 5 for v in scores.values())
        1 <= int(judgment["overall"]) <= 5
    except (TypeError, ValueError):
        return False
    return True


def main():
    args = parse_args()
    records = read_runs(args.runs_jsonl)
    if not records:
        print("ERROR: no records in", args.runs_jsonl, file=sys.stderr)
        return 1

    out_records = list(records)
    judged, skipped, errors, n = 0, 0, 0, 0
    for i, record in enumerate(records):
        verdict = record.get("verdict")
        if verdict not in ("OK",) and not (args.include_invalid and verdict == "INVALID_ACTION"):
            skipped += 1
            continue
        prompt_text = resolve_prompt(record, args.prompts_dir)
        if prompt_text is None:
            print(f"ERROR: prompt file not found for record: "
                  f"{record.get('prompt_file')}", file=sys.stderr)
            errors += 1
            continue
        n += 1
        if not args.quiet:
            print(f"[judge {n}] {os.path.basename(record.get('prompt_file', '?'))} ...", flush=True)
        meta = RUBRIC_TEMPLATE.format(game_prompt=prompt_text, answer=record.get("answer", ""))
        judgment, _ = ask_judge(args.ip, args.port, args.judge_model, meta,
                                args.temperature, args.timeout)
        judgment["judge_model"] = args.judge_model
        judgment["rubric"] = RUBRIC_VERSION
        out_records[i] = dict(record)
        out_records[i]["judgment"] = judgment
        if judgment.get("verdict") == "OK":
            judged += 1
        else:
            errors += 1
            if not args.quiet:
                print(f"  -> {judgment.get('verdict')}: {judgment.get('error', '')[:120]}")

    pairwise_results = []
    if args.pairwise:
        groups = {}
        for idx, record in enumerate(out_records):
            j = record.get("judgment")
            if valid_scores(j):
                groups.setdefault(record.get("prompt_file"), []).append((idx, record))
        pair_n = 0
        for path, items in groups.items():
            prompt_text = resolve_prompt(items[0][1], args.prompts_dir)
            if prompt_text is None:
                continue
            for a in range(len(items)):
                for b in range(a + 1, len(items)):
                    idx_a, rec_a = items[a]
                    idx_b, rec_b = items[b]
                    if rec_a.get("model") == rec_b.get("model"):
                        continue
                    ja, jb = rec_a["judgment"], rec_b["judgment"]
                    try:
                        gap = abs(int(ja["overall"]) - int(jb["overall"]))
                    except (TypeError, ValueError):
                        continue
                    if gap > args.close_threshold:
                        continue
                    pair_n += 1
                    if not args.quiet:
                        print(f"[pair {pair_n}] comparing two close answers ...", flush=True)
                    meta = PAIRWISE_TEMPLATE.format(
                        game_prompt=prompt_text,
                        answer_a=rec_a.get("answer", ""),
                        answer_b=rec_b.get("answer", ""))
                    result, _ = ask_judge(args.ip, args.port, args.judge_model,
                                          meta, args.temperature, args.timeout)
                    result["rubric"] = RUBRIC_VERSION
                    result["model_a"] = rec_a.get("model")
                    result["model_b"] = rec_b.get("model")
                    result["prompt_file"] = path
                    result["overall_a"] = ja.get("overall")
                    result["overall_b"] = jb.get("overall")
                    pairwise_results.append(result)

    print()
    print("=" * 70)
    print("JUDGE SUMMARY")
    print("=" * 70)
    print(f"  Records in input       :  {len(records)}")
    print(f"  Answer records judged  :  {judged}")
    print(f"  Skipped (not OK)       :  {skipped}")
    print(f"  Errors                 :  {errors}")
    if args.pairwise:
        wins = {}
        for r in pairwise_results:
            w = r.get("winner")
            other = {"A": "model_a", "B": "model_b"}.get(w)
            if other:
                wins[r[other]] = wins.get(r[other], 0) + 1
        print(f"  Pairwise comparisons   :  {len(pairwise_results)}")
        for model in sorted(wins):
            print(f"    {model:<28}:  {wins[model]} wins")
    print("=" * 70)

    with open(args.save_jsonl, "w", encoding="utf-8") as f:
        for record in out_records:
            f.write(json.dumps(record) + "\n")
    if pairwise_results:
        base, ext = os.path.splitext(args.save_jsonl)
        pair_path = base + "_pairwise" + ext
        with open(pair_path, "w", encoding="utf-8") as f:
            for r in pairwise_results:
                f.write(json.dumps(r) + "\n")
        print(f"\nSaved judged records to {args.save_jsonl}")
        print(f"Saved {len(pairwise_results)} pairwise results to {pair_path}")
    else:
        print(f"\nSaved judged records to {args.save_jsonl}")
    return 0 if errors == 0 else 1


if __name__ == "__main__":
    sys.exit(main())