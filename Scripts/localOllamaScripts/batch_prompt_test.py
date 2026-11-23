#!/usr/bin/env python3
"""
batch_prompt_test.py — run a matrix of prompts against a list of Ollama
models, validate every answer, and report OK / INVALID / ERROR / TIMEOUT
per combination.

Models argument is a comma-separated list or a file with one model per
line. Prompts are one or more files or folders (folders: all .txt files).

Usage:
    python batch_prompt_test.py <models> <prompt> [prompt ...]     (host defaults to 192.168.2.62:11434)
    python batch_prompt_test.py <ip> <port> <models> <prompt> [prompt ...]
    python batch_prompt_test.py 192.168.1.50 11434 llama3.1:8b,phi4,gemma4:e4b prompt.txt
    python batch_prompt_test.py 10.0.0.2 11434 models.txt prompts_folder --save-jsonl runs.jsonl

Model names must match `ollama list` exactly, including the tag (e.g. gemma4:e4b, llama3.1:8b).
"""

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.request

from prompt_validator import validate_answer


def parse_args():
    ap = argparse.ArgumentParser(
        description="Batch-test prompts against several Ollama models with validation."
    )
    ap.add_argument("ip", nargs="?", default="192.168.2.62",
                    help="Ollama server IP address or hostname (default: 192.168.2.62).")
    ap.add_argument("port", type=int, nargs="?", default=11434,
                    help="Ollama server port (default: 11434).")
    ap.add_argument("models",
                    help="Comma-separated model names, or a file with one model per line.")
    ap.add_argument("prompts", nargs="+",
                    help="Prompt files and/or folders (folders: all .txt files).")
    ap.add_argument("--system", metavar="FILE", help="File with a system prompt.")
    ap.add_argument("--temperature", type=float, help="Sampling temperature.")
    ap.add_argument("--timeout", type=float, default=300.0,
                    help="Hard timeout per run in seconds (default: 300).")
    ap.add_argument("--warmup", action="store_true",
                    help="Send a tiny request to each model first so the timed runs "
                         "exclude model load time.")
    ap.add_argument("--save-jsonl", metavar="FILE",
                    help="Append one JSON record per run to this file.")
    ap.add_argument("--no-validate", action="store_true",
                    help="Skip answer validation (only check JSON + timing).")
    ap.add_argument("--quiet", action="store_true",
                    help="Do not print each model answer, only the results table.")
    return ap.parse_args()


def load_models(spec):
    if os.path.isfile(spec):
        with open(spec, encoding="utf-8") as f:
            return [line.strip() for line in f if line.strip()]
    return [m.strip() for m in spec.split(",") if m.strip()]


def load_prompts(paths):
    files = []
    for path in paths:
        if os.path.isdir(path):
            for name in sorted(os.listdir(path)):
                if name.startswith(".") or not name.endswith(".txt"):
                    continue
                files.append(os.path.join(path, name))
        else:
            files.append(path)
    out = []
    for f in files:
        with open(f, encoding="utf-8") as fh:
            out.append((f, fh.read()))
    return out


def post_generate(ip, port, payload, timeout):
    url = f"http://{ip}:{port}/api/generate"
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data,
                                 headers={"Content-Type": "application/json"},
                                 method="POST")
    start = time.perf_counter()
    resp = urllib.request.urlopen(req, timeout=timeout)
    body = json.loads(resp.read().decode("utf-8"))
    return body, time.perf_counter() - start


def run_one(args, model, prompt_name, prompt_text, warmup_done):
    record = {
        "model": model,
        "prompt_file": prompt_name,
        "verdict": "OK",
        "issues": [],
    }
    if args.warmup and not warmup_done:
        try:
            post_generate(args.ip, args.port,
                          {"model": model, "prompt": "Hi", "stream": False}, 60.0)
        except Exception:
            pass

    payload = {"model": model, "prompt": prompt_text, "stream": False}
    if args.system:
        with open(args.system, encoding="utf-8") as f:
            payload["system"] = f.read()
    if args.temperature is not None:
        payload["options"] = {"temperature": args.temperature}

    start = time.perf_counter()
    try:
        body, wall_s = post_generate(args.ip, args.port, payload, args.timeout)
    except TimeoutError:
        record["verdict"] = "TIMEOUT"
        record["client_time_s"] = round(time.perf_counter() - start, 2)
        return record
    except urllib.error.HTTPError as exc:
        record["verdict"] = "MODEL_ERROR"
        record["issues"] = [f"HTTP {exc.code}: {exc.read().decode('utf-8', 'replace')[:200]}"]
        return record
    except (urllib.error.URLError, ConnectionError) as exc:
        record["verdict"] = "SERVER_DOWN"
        record["issues"] = [str(exc)]
        return record

    record["client_time_s"] = round(wall_s, 2)
    record["prompt_tokens"] = body.get("prompt_eval_count")
    record["output_tokens"] = body.get("eval_count")
    if body.get("prompt_eval_duration"):
        record["prompt_time_s"] = round(body["prompt_eval_duration"] / 1e9, 3)
    if body.get("eval_duration"):
        record["gen_time_s"] = round(body["eval_duration"] / 1e9, 3)
        if body.get("eval_count"):
            record["gen_tok_s"] = round(body["eval_count"] / (body["eval_duration"] / 1e9), 1)
    if body.get("load_duration"):
        record["load_time_s"] = round(body["load_duration"] / 1e9, 2)

    answer = body.get("response", "")
    record["answer"] = answer

    if body.get("error"):
        record["verdict"] = "MODEL_ERROR"
        record["issues"] = [body["error"]]
    else:
        verdict, issues, _ = validate_answer(prompt_text, answer)
        record["verdict"] = verdict
        record["issues"] = issues
    return record


def main():
    args = parse_args()
    models = load_models(args.models)
    prompts = load_prompts(args.prompts)

    if not models:
        print("ERROR: no models given.", file=sys.stderr)
        return 1
    if not prompts:
        print("ERROR: no prompt files found.", file=sys.stderr)
        return 1

    print(f"Batch run: {len(models)} models x {len(prompts)} prompts "
          f"({len(models) * len(prompts)} runs) -> {args.ip}:{args.port}")

    records = []
    for model in models:
        warmup_done = False
        for name, text in prompts:
            print(f"[run] {model} <- {os.path.basename(name)} ...", flush=True)
            record = run_one(args, model, name, text, warmup_done)
            warmup_done = True
            records.append(record)
            if not args.quiet and record.get("answer"):
                print("  " + record["answer"].replace("\n", "\n  "))
            print(f"  -> {record['verdict']}"
                  + (f" ({'; '.join(record['issues'])})" if record.get("issues") else ""))

    print()
    print("=" * 96)
    print(f"RESULTS ({os.path.basename(args.models) if os.path.isfile(args.models) else args.models})")
    print("=" * 96)
    hdr = (f"  {'model':<28} {'prompt':<24} {'prompt_tok':>10} "
           f"{'out_tok':>8} {'gen_s':>7} {'wall_s':>7}  verdict")
    print(hdr)
    print("  " + "-" * (len(hdr) - 4))
    for r in records:
        print(f"  {r['model']:<28} {os.path.basename(r['prompt_file']):<24} "
              f"{r.get('prompt_tokens') or '—':>10} "
              f"{r.get('output_tokens') or '—':>8} "
              f"{r.get('gen_time_s') if r.get('gen_time_s') is not None else '—':>7} "
              f"{r.get('client_time_s') if r.get('client_time_s') is not None else '—':>7}  "
              f"{r['verdict']}")

    print()
    print("=" * 96)
    print("SUMMARY BY MODEL")
    print("=" * 96)
    print(f"  {'model':<28} {'OK':>4} {'INV_ACT':>8} {'INV_JSON':>9} "
          f"{'TIMEOUT':>8} {'ERROR':>6} {'avg gen_s':>10}")
    print("  " + "-" * 92)
    for model in models:
        rs = [r for r in records if r["model"] == model]
        def count(v):
            return sum(1 for r in rs if r["verdict"] == v)
        gens = [r["gen_time_s"] for r in rs if r.get("gen_time_s") is not None]
        avg = f"{sum(gens) / len(gens):.2f}" if gens else "—"
        print(f"  {model:<28} {count('OK'):>4} {count('INVALID_ACTION'):>8} "
              f"{count('INVALID_JSON'):>9} {count('TIMEOUT'):>8} "
              f"{count('MODEL_ERROR') + count('SERVER_DOWN') + count('PROMPT_UNPARSED'):>6} {avg:>10}")
    print("=" * 96)

    if args.save_jsonl:
        with open(args.save_jsonl, "a", encoding="utf-8") as f:
            for r in records:
                f.write(json.dumps(r) + "\n")
        print(f"\nSaved {len(records)} records to {args.save_jsonl}")

    ok = sum(1 for r in records if r["verdict"] == "OK")
    return 0 if ok == len(records) else 1


if __name__ == "__main__":
    sys.exit(main())