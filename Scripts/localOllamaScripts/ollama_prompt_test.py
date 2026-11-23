#!/usr/bin/env python3
"""
ollama_prompt_test.py — send a prompt file to an Ollama model and print
the answer plus token/timing statistics. Works with any model served by
Ollama, via the common /api/generate REST endpoint.

Usage:
    python ollama_prompt_test.py <model> <prompt_file>            (host defaults to 192.168.2.62:11434)
    python ollama_prompt_test.py <ip> <port> <model> <prompt_file>
    python ollama_prompt_test.py 192.168.1.50 11434 llama3.1:8b prompt.txt
    python ollama_prompt_test.py localhost 11434 phi4 prompt.txt --temperature 0.7
    python ollama_prompt_test.py 10.0.0.2 11434 gemma4:e4b prompt.txt --stream
    python ollama_prompt_test.py localhost 11434 phi4 prompt.txt --validate

Model names must match `ollama list` exactly, including the tag (e.g. gemma4:e4b, llama3.1:8b).
"""

import argparse
import json
import sys
import time
import urllib.error
import urllib.request

from prompt_validator import validate_answer


def print_validation(prompt, answer_text):
    """Validation block in the same report style as the STATS block."""
    verdict, issues, _ = validate_answer(prompt, answer_text)
    print()
    print("=" * 70)
    print(f"VALIDATION: {verdict}")
    print("=" * 70)
    for issue in issues:
        print(f"  - {issue}")
    if not issues:
        print("  - action passes the prompt's SAFE / PAIRS rules")
    print("=" * 70)
    return verdict


def parse_args():
    ap = argparse.ArgumentParser(
        description="Send a prompt file to an Ollama model and print answer + stats."
    )
    ap.add_argument("ip", nargs="?", default="192.168.2.62",
                    help="Ollama server IP address or hostname (default: 192.168.2.62).")
    ap.add_argument("port", type=int, nargs="?", default=11434,
                    help="Ollama server port (default: 11434).")
    ap.add_argument("model",
                    help="Model name as known to Ollama (e.g. llama3.1:8b).")
    ap.add_argument("prompt_file",
                    help="Path to a text file whose contents are the prompt.")
    ap.add_argument("--system", metavar="FILE",
                    help="Optional file with a system prompt.")
    ap.add_argument("--temperature", type=float,
                    help="Sampling temperature (e.g. 0.7).")
    ap.add_argument("--stream", action="store_true",
                    help="Stream tokens live instead of waiting for the full answer.")
    ap.add_argument("--timeout", type=float, default=300.0,
                    help="Request timeout in seconds (default: 300).")
    ap.add_argument("--raw", action="store_true",
                    help="Print the raw JSON response instead of the formatted answer.")
    ap.add_argument("--validate", action="store_true",
                    help="Validate the answer against the prompt's SAFE/PAIRS rules.")
    ap.add_argument("--no-stats", action="store_true",
                    help="Do not print the statistics block.")
    return ap.parse_args()


def read_file(path, what):
    try:
        with open(path, "r", encoding="utf-8") as f:
            return f.read()
    except OSError as exc:
        print(f"ERROR: cannot read {what} file '{path}': {exc}", file=sys.stderr)
        sys.exit(1)


def build_payload(args, prompt, system_text):
    payload = {
        "model": args.model,
        "prompt": prompt,
        "stream": bool(args.stream),
    }
    if system_text:
        payload["system"] = system_text
    if args.temperature is not None:
        payload["options"] = {"temperature": args.temperature}
    return payload


def run_request(url, payload, timeout):
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=data,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    return urllib.request.urlopen(req, timeout=timeout)


def fmt_duration(ns):
    """Format Ollama nanosecond durations human-friendly."""
    s = ns / 1e9
    if s >= 60:
        return f"{int(s // 60)}m {s % 60:.2f}s"
    if s >= 1:
        return f"{s:.2f}s"
    return f"{s * 1000:.1f}ms"


def print_stats(resp, wall_s):
    """Stats block in the same report style as the other Scripts tools
    (===== header, two-space-indented '  Key : value' lines, — for missing)."""
    def get(key):
        v = resp.get(key) if isinstance(resp, dict) else None
        return v if v is not None else 0

    p_tokens = get("prompt_eval_count")
    p_dur    = get("prompt_eval_duration")
    q_dur    = get("load_duration")
    e_tokens = get("eval_count")
    e_dur    = get("eval_duration")
    t_dur    = get("total_duration")

    print()
    print("=" * 70)
    print("STATS")
    print("=" * 70)
    if not p_tokens and not e_tokens:
        print("  (no token counts reported; model was loaded for this call,")
        print("   run the script again for token statistics)")
    if p_tokens:
        print(f"  Prompt tokens        :  {p_tokens}")
        if p_dur:
            print(f"  Prompt eval time     :  {fmt_duration(p_dur)}")
    if q_dur:
        print(f"  Model load time      :  {fmt_duration(q_dur)}")
    if e_tokens:
        print(f"  Output tokens        :  {e_tokens}")
        if e_dur:
            print(f"  Generation time      :  {fmt_duration(e_dur)}")
    if p_tokens and p_dur:
        print(f"  Prompt speed         :  {p_tokens / (p_dur / 1e9):.1f} tok/s")
    if e_tokens and e_dur:
        print(f"  Generation speed     :  {e_tokens / (e_dur / 1e9):.1f} tok/s")
    if t_dur:
        print(f"  Total (server side)  :  {fmt_duration(t_dur)}")
    print(f"  Total (client side)  :  {wall_s:.2f}s")
    if p_tokens or e_tokens:
        print(f"  Total tokens         :  {p_tokens + e_tokens}")
    print("=" * 70)


def main():
    args = parse_args()
    prompt = read_file(args.prompt_file, "prompt")
    system_text = read_file(args.system, "system") if args.system else None
    payload = build_payload(args, prompt, system_text)
    url = f"http://{args.ip}:{args.port}/api/generate"

    if args.stream:
        answer_parts = []
        resp_json = {}
        start = time.perf_counter()
        try:
            with run_request(url, payload, args.timeout) as resp:
                for line in resp:
                    if not line.strip():
                        continue
                    chunk = json.loads(line.decode("utf-8"))
                    if chunk.get("done"):
                        resp_json = chunk
                    else:
                        token = chunk.get("response", "")
                        answer_parts.append(token)
                        print(token, end="", flush=True)
        except urllib.error.HTTPError as exc:
            print(f"ERROR: Ollama returned HTTP {exc.code}: "
                  f"{exc.read().decode('utf-8', 'replace')[:500]}", file=sys.stderr)
            return 1
        except (urllib.error.URLError, TimeoutError, ConnectionError) as exc:
            print(f"ERROR: connection failed: {exc}\n"
                  f"Is Ollama running at {args.ip}:{args.port}?", file=sys.stderr)
            return 1
        wall_s = time.perf_counter() - start
        print("\n")
        if not args.no_stats:
            print_stats(resp_json, wall_s)
        if args.validate:
            print_validation(prompt, "".join(answer_parts))
        return 0

    start = time.perf_counter()
    try:
        with run_request(url, payload, args.timeout) as resp:
            body = resp.read().decode("utf-8")
    except urllib.error.HTTPError as exc:
        print(f"ERROR: Ollama returned HTTP {exc.code}: "
              f"{exc.read().decode('utf-8', 'replace')[:500]}", file=sys.stderr)
        return 1
    except (urllib.error.URLError, TimeoutError, ConnectionError) as exc:
        print(f"ERROR: connection failed: {exc}\n"
              f"Is Ollama running at {args.ip}:{args.port}?", file=sys.stderr)
        return 1

    wall_s = time.perf_counter() - start
    try:
        resp_json = json.loads(body)
    except json.JSONDecodeError as exc:
        print(f"ERROR: unexpected non-JSON response from server: {exc}", file=sys.stderr)
        return 1

    if args.raw:
        print(json.dumps(resp_json, indent=2))
        return 0

    if resp_json.get("error"):
        print(f"ERROR: Ollama error: {resp_json['error']}", file=sys.stderr)
        return 1

    print(resp_json.get("response", ""))
    verdict = None
    if not args.no_stats:
        print_stats(resp_json, wall_s)
    if args.validate:
        verdict = print_validation(prompt, resp_json.get("response", ""))
    return 0 if verdict in (None, "OK") else 1


if __name__ == "__main__":
    sys.exit(main())