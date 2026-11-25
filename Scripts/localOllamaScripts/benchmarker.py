#!/usr/bin/env python3
"""
benchmarker.py — settings-driven benchmark for WavesST LLM prompts.

Runs a matrix of (model x run) where each run pairs a prompt file with an
answer key (the ground truth produced by battle_replay.py:
    python3 battle_replay.py <log> --state TURN ACTOR --json > key.json
), sends the prompt to Ollama, and validates the reply against the key.

Everything can come from a settings JSON, from the command line, or a mix
(command-line flags override settings).

Settings schema:
{
  "host": "192.168.2.62", "port": 11434,
  "models": ["llama3.1:8b", "phi4"],
  "prompt_pairs": [
    {"prompt": "promptA.txt", "check": "promptA_check.txt"},
    {"prompt": "promptB.txt", "check": "promptB_check.txt"},
    {"prompt": "promptB-no-list.txt", "check": "promptB_check.txt"}
  ],
  "repeat": 5,
  "output": "results.jsonl",
  "timeout": 300,
  "temperature": 0.0
}

"prompt" and "check" are required per pair. Optional per pair: "name"
(defaults to the prompt filename), "axes" (free-form labels carried into
every record), "repeat" and "temperature" overrides. Several prompts may
share one check file (same decision state, different prompt variants).
"output" is the jsonl to append records to (same as --save-jsonl).

Validation is against the ENGINE answer key, never against the prompt's own
lists. Extra metrics: option conformance (did the model pick from the
prompt's listed pairs / safe tiles, parsed from the prompt text) and a
wave-tile warning when the chosen destination sits on a wave.

Usage:
    python benchmarker.py --settings config.json
    python benchmarker.py --settings config.json --model llama3.1:8b --repeat 3
    python benchmarker.py --settings config.json --run "C-nolist-turn1-Luana"
    python benchmarker.py --models llama3.1:8b,phi4 \\
        --prompt promptA.txt --key key.json --axes pairs:true strategy:false
    python benchmarker.py --settings config.json --validate-only \\
        --answer '{"movement":[8,8],"attack":[8,10],"move_after_attack":[-1,-1]}'
"""

import argparse
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request


# ---------------------------------------------------------------- settings


DEFAULT_HOST = "192.168.2.62"
DEFAULT_PORT = 11434
BUDGET = 3           # orthogonal steps shared by movement + moveAfterAttack
ATTACK_DISTANCE = 2  # exact Manhattan distance; 1 is a dead zone


def parse_axes(spec):
    """'pairs:true strategy:false' -> {'pairs': True, 'strategy': False}"""
    if not spec:
        return {}
    axes = {}
    for part in spec.split():
        key, _, value = part.partition(":")
        axes[key.strip()] = value.strip().lower() in ("1", "true", "yes", "y")
    return axes


def load_settings(path):
    try:
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, json.JSONDecodeError) as exc:
        print(f"ERROR: cannot read settings '{path}': {exc}", file=sys.stderr)
        return None


def build_runs(args, settings):
    """Merge settings prompt_pairs with CLI overrides into run dicts."""
    models = args.models or (settings or {}).get("models")
    if isinstance(models, str):
        models = [m.strip() for m in models.split(",") if m.strip()]
    models = models or []

    runs = []
    if settings:
        for r in settings.get("prompt_pairs", []):
            if args.run and r.get("name") != args.run \
                    and os.path.basename(r.get("prompt", "")) != args.run:
                continue
            if args.prompt or args.key:
                continue  # CLI run replaces the whole matrix
            prompt, check = r.get("prompt"), r.get("check")
            if not prompt or not check:
                print(f"ERROR: prompt pair {r} needs 'prompt' and 'check'.",
                      file=sys.stderr)
                return None, None
            runs.append({
                "name": r.get("name")
                        or os.path.basename(prompt).rsplit(".", 1)[0],
                "prompt": prompt,
                "key": check,
                "axes": r.get("axes") or {},
                "repeat": r.get("repeat") or 0,
                "temperature": r.get("temperature", settings.get("temperature")),
            })
    if args.prompt or args.key:  # CLI-defined run
        if not (args.prompt and args.key):
            print("ERROR: --prompt and --key must be given together.",
                  file=sys.stderr)
            return None, None
        runs.append({
            "name": args.run or os.path.basename(args.prompt).rsplit(".", 1)[0],
            "prompt": args.prompt,
            "key": args.key,
            "axes": args.axes or {},
            "repeat": args.repeat or 0,
            "temperature": args.temperature,
        })
    return models, runs


# ------------------------------------------------------- answer-key loading


def load_key(path):
    """Answer key written by battle_replay.py --state T A --json:
    {"position":[x,y], "budget":3, "attack_range":2,
     "moves":[{"position":[x,y], "steps":n, "wave_tile":bool,
               "attacks":[{"kind":"enemy|ally|target|obstacle",
                           "position":[x,y], "name":...}]}]}"""
    try:
        with open(path, encoding="utf-8") as f:
            key = json.load(f)
    except (OSError, json.JSONDecodeError) as exc:
        print(f"ERROR: cannot read key '{path}': {exc}", file=sys.stderr)
        return None
    if "moves" not in key or "position" not in key:
        print(f"ERROR: '{path}' does not look like a battle_replay answer "
              f"key (need 'position' and 'moves').", file=sys.stderr)
        return None
    return key


# ------------------------------------------------------------ prompt parsing


PAIR_RE = re.compile(r"move\s*\((\d+)\s*,\s*(\d+)\)")
TILE_RE = re.compile(r"^\((\d+)\s*,\s*(\d+)\)\s*$", re.MULTILINE)


def parse_prompt_options(text):
    """(pairs, safe_tiles) listed inside the prompt itself, for conformance."""
    pairs = {tuple(int(g) for g in m) for m in PAIR_RE.findall(text)}
    safe = {tuple(int(g) for g in m) for m in TILE_RE.findall(text)}
    return pairs, safe


# -------------------------------------------------------------- validation


def manhattan(a, b):
    return abs(a[0] - b[0]) + abs(a[1] - b[1])


def as_pos(value):
    """[-1,-1] or None means 'no move'/'no attack'. Returns tuple or None."""
    if value in (None, [-1, -1], ("-1", "-1"), []):
        return None
    if isinstance(value, (list, tuple)) and len(value) == 2:
        try:
            return (int(value[0]), int(value[1]))
        except (TypeError, ValueError):
            return "BAD"
    return "BAD"


def validate_answer(key, answer):
    """Engine-truth validation. Returns (verdict, issues list).
    Verdicts: OK, INVALID_JSON, INVALID_MOVE, INVALID_ATTACK."""
    issues = []
    try:
        parsed = json.loads(answer)
    except (TypeError, json.JSONDecodeError) as exc:
        return "INVALID_JSON", [f"unparseable JSON: {exc}"]
    if not isinstance(parsed, dict):
        return "INVALID_JSON", ["answer is not a JSON object"]

    origin = tuple(key["position"])
    stay = origin
    moves = {tuple(m["position"]): m for m in key["moves"]}

    movement = as_pos(parsed.get("movement"))
    attack = as_pos(parsed.get("attack"))
    maa = as_pos(parsed.get("move_after_attack"))

    if movement == "BAD":
        return "INVALID_JSON", ["movement is not [x, y]"]
    if attack == "BAD":
        return "INVALID_JSON", ["attack is not [x, y]"]
    if movement is None:
        movement = stay  # declared no-move means stay

    if movement not in moves:
        return "INVALID_MOVE", [
            f"movement {movement} not reachable within {BUDGET} steps "
            f"from {stay} (engine answer key)"]

    entry = moves[movement]
    if entry.get("wave_tile"):
        issues.append(f"destination {movement} is a WAVE tile "
                      f"(wave damage / probabilistic push)")

    if attack is None:
        issues.append("no attack declared")
        if entry["attacks"]:
            issues.append(f"NOTE: {len(entry['attacks'])} valid attack(s) "
                          f"existed from {movement}")
    else:
        valid_attacks = {tuple(a["position"]): a for a in entry["attacks"]}
        if attack in valid_attacks:
            hit = valid_attacks[attack]
            if hit.get("kind") == "ally":
                issues.append(f"ALLY FIRE: attack on {hit.get('name')} at "
                              f"{attack} is legal but hurts your faction")
            elif hit.get("kind") == "obstacle":
                issues.append("attack wasted on an obstacle (no effect)")
            elif hit.get("kind") == "wave":
                issues.append("attack on a wave shot (engine-legal; "
                              "propagates one tile behind the wave)")
            elif hit.get("kind") == "target":
                issues.append("attack on a neutral target (does not help win)")
            else:
                issues.append(f"valid attack on {hit.get('name')} "
                              f"hp {hit.get('hp')}")
        else:
            dist = manhattan(movement, attack)
            reason = (f"DEAD ZONE: {attack} is at distance {dist} "
                      f"(adjacent tiles cannot be attacked)"
                      if dist == 1 else
                      f"OUT OF RANGE: {attack} is at Manhattan distance {dist} "
                      f"(only exactly {ATTACK_DISTANCE} is legal)"
                      if dist > ATTACK_DISTANCE else
                      f"illegal target at {attack} "
                      f"(no enemy/ally/target/obstacle there)")
            return "INVALID_ATTACK", [
                f"attack {attack} from {movement}: {reason}"]

    # move_after_attack: soft check against the remaining budget
    if maa not in (None, movement):
        remaining = BUDGET - entry["steps"]
        dist = manhattan(movement, maa)
        if dist > remaining:
            issues.append(f"NOTE: move_after_attack {maa} needs {dist} steps "
                          f"but only {remaining} remain (engine may refuse)")
    return "OK", issues


def conformance(prompt_text, movement, attack):
    """Did the model select within the prompt's own lists? (optional metric)"""
    pairs, safe = parse_prompt_options(prompt_text)
    if not pairs and not safe:
        return "n/a (no lists in prompt)"
    if movement in (None, [-1, -1], ("-1", "-1"), []):
        return "stay"
    mv = tuple(movement)
    tag = []
    if pairs:
        tag.append("movement from listed pairs" if mv in pairs
                   else "movement off the listed pairs")
    if safe:
        tag.append("on SAFE list" if mv in safe
                   else "NOT on the SAFE list")
    if attack not in (None, [-1, -1], ("-1", "-1"), []):
        atk = tuple(attack)
        ok = any(
            re.search(rf"move\s*\(\s*{mv[0]}\s*,\s*{mv[1]}\s*\)[^\n]*"
                      rf"attacks\s*\(\s*{atk[0]}\s*,\s*{atk[1]}\s*\)",
                      prompt_text) for _ in [0])
        if not pairs:
            tag.append("no pairs list (algebra self-computed)")
        else:
            tag.append("attack listed for that move" if ok
                        else "attack NOT listed for that move")
    return ", ".join(tag)


# ------------------------------------------------------------------- ollama


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


def run_one(host, port, model, prompt_text, run, timeout):
    payload = {"model": model, "prompt": prompt_text, "stream": False,
                "format": "json"}
    if run.get("temperature") is not None:
        payload["options"] = {"temperature": run["temperature"]}
    try:
        body, wall_s = post_generate(host, port, payload, timeout)
    except TimeoutError:
        return {"verdict": "TIMEOUT", "issues": []}
    except urllib.error.URLError as exc:
        return {"verdict": "SERVER_DOWN", "issues": [str(exc)]}
    except urllib.error.HTTPError as exc:
        return {"verdict": "MODEL_ERROR",
                "issues": [f"HTTP {exc.code}: "
                           f"{exc.read().decode('utf-8', 'replace')[:200]}"]}

    record = {"verdict": "OK", "issues": [],
              "client_time_s": round(wall_s, 2),
              "prompt_tokens": body.get("prompt_eval_count"),
              "output_tokens": body.get("eval_count")}
    if body.get("eval_duration") and body.get("eval_count"):
        record["gen_time_s"] = round(body["eval_duration"] / 1e9, 3)
        record["gen_tok_s"] = round(
            body["eval_count"] / (body["eval_duration"] / 1e9), 1)
    record["answer"] = body.get("response", "")
    if body.get("error"):
        record["verdict"] = "MODEL_ERROR"
        record["issues"] = [str(body["error"])[:200]]
    return record


# -------------------------------------------------------------------- main


def parse_args():
    ap = argparse.ArgumentParser(
        description="WavesST prompt benchmarker: settings JSON or CLI, "
                    "validated against battle_replay answer keys.")
    src = ap.add_mutually_exclusive_group()
    src.add_argument("--settings", metavar="FILE",
                     help="Settings JSON with models, runs, prompt+key pairs.")
    src.add_argument("--models", metavar="M1,M2,...",
                     help="Models for a CLI-only run.")
    ap.add_argument("--prompt", metavar="FILE", help="Prompt file (CLI run).")
    ap.add_argument("--key", metavar="FILE",
                    help="Answer key JSON (battle_replay --state ... --json).")
    ap.add_argument("--run", help="Run only this named run from the settings.")
    ap.add_argument("--repeat", type=int, default=0,
                    help="Repeat every run N times (overrides settings).")
    ap.add_argument("--axes", type=parse_axes, default={},
                    help="Axis labels, e.g. 'pairs:true strategy:false'.")
    ap.add_argument("--host", help=f"Ollama host (default {DEFAULT_HOST}).")
    ap.add_argument("--port", type=int, help=f"Ollama port (default {DEFAULT_PORT}).")
    ap.add_argument("--temperature", type=float,
                    help="Sampling temperature (overrides settings).")
    ap.add_argument("--timeout", type=float,
                    help="Hard timeout per run in seconds.")
    ap.add_argument("--warmup", action="store_true",
                    help="Send a tiny request per model first to exclude load time.")
    ap.add_argument("--save-jsonl", metavar="FILE",
                    help="Append one JSON record per run to this file.")
    ap.add_argument("--validate-only", action="store_true",
                    help="Do not call Ollama; validate --answer against --key.")
    ap.add_argument("--answer", metavar="JSON",
                    help="With --validate-only: the model answer JSON to check.")
    ap.add_argument("--quiet", action="store_true",
                    help="Print only the summary tables.")
    return ap.parse_args()


def main():
    args = parse_args()

    # ---- offline validation mode -------------------------------------------
    if args.validate_only:
        if not (args.key and args.answer is not None):
            print("ERROR: --validate-only needs --key and --answer.",
                  file=sys.stderr)
            return 1
        key = load_key(args.key)
        if key is None:
            return 1
        verdict, issues = validate_answer(key, args.answer)
        print("=" * 70)
        print(f"VALIDATION (key actor at {tuple(key['position'])})")
        print("=" * 70)
        print(f"verdict: {verdict}")
        for issue in issues:
            print(f"  - {issue}")

    # ---- settings / CLI assembly -------------------------------------------
    settings = None
    if args.settings:
        settings = load_settings(args.settings)
        if settings is None:
            return 1
    host = args.host or (settings or {}).get("host", DEFAULT_HOST)
    port = args.port or (settings or {}).get("port", DEFAULT_PORT)
    timeout = args.timeout or (settings or {}).get("timeout", 300.0)

    if args.validate_only:
        return 0

    models, runs = build_runs(args, settings)
    if models is None:
        return 1
    if not models:
        print("ERROR: no models. Use settings 'models' or --models.",
              file=sys.stderr)
        return 1
    if not runs:
        print("ERROR: no runs. Use settings 'prompt_pairs' or --prompt/--key.",
              file=sys.stderr)
        return 1

    global_repeat = args.repeat or (settings or {}).get("repeat", 1)

    # ---- load prompts and keys ---------------------------------------------
    prepared = []
    for run in runs:
        try:
            with open(run["prompt"], encoding="utf-8") as f:
                prompt_text = f.read()
        except OSError as exc:
            print(f"ERROR: cannot read prompt '{run['prompt']}': {exc}",
                  file=sys.stderr)
            return 1
        key = load_key(run["key"])
        if key is None:
            return 1
        prepared.append((run, prompt_text, key))

    # per-run repeat: pair-level "repeat" wins, else global
    counts = [max(1, run.get("repeat") or global_repeat)
              for run, _, _ in prepared]
    total = len(models) * sum(counts)

    print("=" * 70)
    print(f"benchmark: {len(models)} models x {len(prepared)} runs "
          f"(x repeats = {total} requests) -> {host}:{port}")
    print("=" * 70)

    records = []
    warmup_done = set()
    for model in models:
        for run, prompt_text, key in prepared:
            run_repeat = max(1, run.get("repeat") or global_repeat)
            for attempt in range(run_repeat):
                attempt_id = f"{model} <- {run['name']} #{attempt + 1}"
                if not args.quiet:
                    print(f"[run] {attempt_id} ...", flush=True)

                if args.warmup and model not in warmup_done:
                    try:
                        post_generate(host, port,
                                      {"model": model, "prompt": "Hi",
                                       "stream": False}, 60.0)
                    except Exception:
                        pass
                    warmup_done.add(model)

                result = run_one(host, port, model, prompt_text, run, timeout)
                answer = result.pop("answer", None)
                record = {"model": model, "run": run["name"],
                          "axes": run.get("axes", {}), "attempt": attempt + 1}
                record.update(result)
                record["answer"] = answer

                if answer is not None and result["verdict"] in ("OK",):
                    verdict, issues = validate_answer(key, answer)
                    record["verdict"] = verdict
                    record["issues"] = issues
                    try:
                        parsed = json.loads(answer)
                        movement = parsed.get("movement")
                        attack = parsed.get("attack")
                        record["conformance"] = conformance(
                            prompt_text, movement, attack)
                    except (TypeError, json.JSONDecodeError):
                        record["conformance"] = "n/a"
                else:
                    record["issues"] = record.get("issues", [])
                records.append(record)

                if not args.quiet and answer:
                    print("  " + str(answer).replace("\n", "\n  "))
                print(f"  -> {record['verdict']}"
                      + (f" ({'; '.join(record['issues'])[:120]})"
                         if record.get("issues") else ""))

    # ---- report -------------------------------------------------------------
    print()
    print("=" * 70)
    print("RESULTS BY RUN")
    print("=" * 70)
    for run, _, _ in prepared:
        name = run["name"]
        print(f"\n  {name}   axes={json.dumps(run.get('axes', {}))}")
        print(f"  {'model':<28} {'verdict':<16} summary")
        print("  " + "-" * 66)
        for r in [r for r in records if r["run"] == name]:
            note = "; ".join(r.get("issues", []))[:60] or "clean"
            print(f"  {r['model']:<28} {r['verdict']:<16} {note}")

    print()
    print("=" * 70)
    print("SUMMARY BY MODEL x RUN")
    print("=" * 70)
    print(f"  {'model':<24} {'run':<26} "
          f"{'OK':>4} {'DEADZONE/RANGE':>14} {'MOVE':>5} {'JSON':>5} {'ERR':>4}")
    print("  " + "-" * 96)
    for model in models:
        for run, _, _ in prepared:
            rs = [r for r in records
                  if r["model"] == model and r["run"] == run["name"]]
            if not rs:
                continue
            ok = sum(1 for r in rs if r["verdict"] == "OK")
            bad_atk = sum(1 for r in rs if r["verdict"] == "INVALID_ATTACK")
            bad_mv = sum(1 for r in rs if r["verdict"] == "INVALID_MOVE")
            bad_js = sum(1 for r in rs if r["verdict"] == "INVALID_JSON")
            err = sum(1 for r in rs
                      if r["verdict"] in ("TIMEOUT", "MODEL_ERROR",
                                          "SERVER_DOWN"))
            print(f"  {model:<24} {run['name'][:25]:<26} "
                  f"{ok:>4} {bad_atk:>14} {bad_mv:>5} {bad_js:>5} {err:>4}")

    ally_hits = sum(1 for r in records
                    if any("ALLY FIRE" in i for i in r.get("issues", [])))
    wave_tiles = sum(1 for r in records
                     if any("WAVE tile" in i for i in r.get("issues", [])))
    print()
    print(f"  ally-fire answers: {ally_hits}   wave-tile destinations: {wave_tiles}")

    out_file = args.save_jsonl or (settings or {}).get("output")
    if out_file:
        with open(out_file, "a", encoding="utf-8") as f:
            for r in records:
                f.write(json.dumps(r) + "\n")
        print(f"\nSaved {len(records)} records to {out_file}")

    ok = sum(1 for r in records if r["verdict"] == "OK")
    return 0 if ok == len(records) else 1


if __name__ == "__main__":
    sys.exit(main())