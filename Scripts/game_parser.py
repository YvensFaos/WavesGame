#!/usr/bin/env python3
import json
import argparse
from collections import defaultdict, Counter


def load_events(path):
    """Load JSONL events from a file."""
    events = []
    with open(path, "r", encoding="utf-8-sig") as f:
        for line_no, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                events.append(json.loads(line))
            except json.JSONDecodeError as exc:
                print(f"[warn] skipping line {line_no}: {exc}")
    return events

def classify_actor(actor_info):
    prefab = actor_info.get("shipPrefabType") or ""
    name = actor_info.get("name") or ""

    if name.startswith("Target"):
        kind = "Target"
    elif "Llm" in prefab or name.startswith("LLM|"):
        kind = "LLM"
    elif "AI" in prefab or name.startswith("AU|"):
        kind = "AI Unit"
    else:
        kind = "Unknown"

    return {
        "kind": kind,
        "ship_prefab": prefab,
        "llm_type": actor_info.get("llmType"),
        "llm_model": actor_info.get("llmModel"),
        "llm_prompt": actor_info.get("basePrompt"),
        "llm_info": actor_info.get("llmInfo"),
        "machine_brain": actor_info.get("machineBrain"),
    }

def new_actor(name, faction=None, health=None, pos=None, meta=None):
    a = {
        "name": name,
        "faction": faction,
        "initial_health": health,
        "final_health": health,
        "position": pos,
        "moves": 0,
        "attacks": 0,
        "damage_dealt": 0.0,
        "damage_taken": 0.0,
        "kills": 0,
        "deaths": 0,
        "commands": 0,
        "response_times": [],
    }
    if meta:
        a.update(meta)
    return a

def get_actor(summary, actor_id, faction=None):
    if actor_id not in summary["actors"]:
        summary["actors"][actor_id] = new_actor(actor_id, faction)
    return summary["actors"][actor_id]

def summarise(events):
    summary = {
        "map": None,
        "randomSeed": None,
        "maxTurns": None,
        "winner": None,
        "winner_message": None,
        "final_turn": 0,
        "final_timestamp": 0,

        "event_counts": Counter(),
        "event_counts_by_faction": defaultdict(Counter),

        "moves_by_faction": Counter(),
        "attacks_by_faction": Counter(),
        "damage_dealt_by_faction": Counter(),
        "damage_taken_by_faction": Counter(),
        "kills_by_faction": Counter(),
        "deaths_by_faction": Counter(),
        "commands_by_faction": Counter(),
        "invalid_by_faction": defaultdict(Counter),

        "response_times_by_faction": defaultdict(list),
        "avg_response_time_by_faction": {},

        "actors": {},
    }

    # Used to infer kills: targetId -> (attackerId, attackerFaction)
    last_attacker_by_target = {}

    for ev in events:
        et = ev.get("eventType")
        faction = ev.get("faction")
        actor_id = ev.get("actorId")

        summary["event_counts"][et] += 1
        if faction:
            summary["event_counts_by_faction"][faction][et] += 1

        if "turn" in ev:
            summary["final_turn"] = max(summary["final_turn"], ev["turn"])
        if "timeStamp" in ev:
            summary["final_timestamp"] = max(summary["final_timestamp"], ev["timeStamp"])

        if et == "INFO":
            summary["map"] = ev.get("map")
            summary["randomSeed"] = ev.get("randomSeed")
            summary["maxTurns"] = ev.get("maxTurns")

            for actor in ev.get("navalActorEntryJsons", []):
                name = actor.get("name")
                if not name:
                    continue
                summary["actors"][name] = new_actor(
                    name,
                    faction=actor.get("faction"),
                    health=actor.get("currentHealth"),
                    pos=actor.get("position"),
                    meta=classify_actor(actor),
                )

        elif et == "OVER":
            summary["winner"] = ev.get("winningFaction")
            summary["winner_message"] = (ev.get("goalMessage") or "").strip()

        elif et == "GSTT":
            for actor in ev.get("navalActorEntryJsons", []):
                name = actor.get("name")
                if not name:
                    continue
                a = get_actor(summary, name, actor.get("faction"))
                a["faction"] = actor.get("faction", a["faction"])
                a["final_health"] = actor.get("currentHealth", a["final_health"])
                a["position"] = actor.get("position", a["position"])

        elif et == "MOVE":
            if actor_id:
                a = get_actor(summary, actor_id, faction)
                a["moves"] += 1
            if faction:
                summary["moves_by_faction"][faction] += 1

        elif et == "ATTK":
            damage = ev.get("damage", 0) or 0
            if actor_id:
                a = get_actor(summary, actor_id, faction)
                a["attacks"] += 1
                a["damage_dealt"] += damage
            if faction:
                summary["attacks_by_faction"][faction] += 1
                summary["damage_dealt_by_faction"][faction] += damage

            target_id = ev.get("targetId")
            if target_id:
                last_attacker_by_target[target_id] = (actor_id, faction)

        elif et == "DAMG":
            damage = ev.get("damage", 0) or 0
            if actor_id:
                a = get_actor(summary, actor_id, faction)
                a["damage_taken"] += damage
            if faction:
                summary["damage_taken_by_faction"][faction] += damage

        elif et == "DEAD":
            if actor_id:
                a = get_actor(summary, actor_id, faction)
                a["deaths"] += 1
            if faction:
                summary["deaths_by_faction"][faction] += 1

            # Optional kill inference: last attacker before death gets credit.
            killer = last_attacker_by_target.get(actor_id)
            if killer:
                killer_id, killer_faction = killer
                if killer_id:
                    ka = get_actor(summary, killer_id, killer_faction)
                    ka["kills"] += 1
                if killer_faction:
                    summary["kills_by_faction"][killer_faction] += 1

        elif et == "CMMD":
            if actor_id:
                a = get_actor(summary, actor_id, faction)
                a["commands"] += 1
            if faction:
                summary["commands_by_faction"][faction] += 1

        elif et == "RESN":
            rt = ev.get("responseTime")
            if rt is not None:
                if actor_id:
                    a = get_actor(summary, actor_id, faction)
                    a["response_times"].append(rt)
                if faction:
                    summary["response_times_by_faction"][faction].append(rt)

        elif et == "NVLD":
            if faction:
                summary["invalid_by_faction"][faction][ev.get("type", "unknown")] += 1

    # Average response time per faction
    for fac, times in summary["response_times_by_faction"].items():
        summary["avg_response_time_by_faction"][fac] = (
            sum(times) / len(times) if times else 0.0
        )

    return summary

def describe_faction_composition(summary, faction):
    actors = [a for a in summary["actors"].values()
              if a.get("faction") == faction]
    if not actors:
        return f"  (no actor metadata found for faction '{faction}')"

    kinds = Counter(a.get("kind", "Unknown") for a in actors)
    lines = [f"  Actors: {len(actors)} -> {dict(kinds)}"]

    llms = [a for a in actors if a.get("kind") == "LLM"]
    if llms:
        providers = sorted({a.get("llm_type") for a in llms if a.get("llm_type")})
        models = sorted({a.get("llm_model") for a in llms if a.get("llm_model")})
        prompts = sorted({a.get("llm_prompt") for a in llms if a.get("llm_prompt")})
        lines.append(f"    LLM providers: {providers}")
        lines.append(f"    LLM models:    {models}")
        lines.append(f"    LLM prompts:   {prompts}")

    aus = [a for a in actors if a.get("kind") == "AI Unit"]
    if aus:
        brains = sorted({a.get("machine_brain") for a in aus if a.get("machine_brain")})
        lines.append(f"    AI brains:     {brains}")

    targets = [a for a in actors if a.get("kind") == "Target"]
    if targets:
        lines.append(f"    Targets:       {len(targets)}")

    return "\n".join(lines)

def print_summary(s):
    print("=== Battle Summary ===")
    print(f"Map: {s['map']}")
    print(f"Seed: {s['randomSeed']}")
    print(f"MaxTurns: {s['maxTurns']}")
    print(f"Winner: {s['winner']}")
    print(f"Goal: {s['winner_message']}")

    if s["winner"]:
        winner_actors = [a for a in s["actors"].values() if a.get("deaths", 0) == 0]
        winning_factions = Counter(
            a.get("faction") for a in winner_actors if a.get("faction")
        )
        if winning_factions:
            winning_faction = winning_factions.most_common(1)[0][0]
            print(f"Winning faction composition ({winning_faction}):")
            print(describe_faction_composition(s, winning_faction))

    print(f"Final turn: {s['final_turn']}")
    print(f"Final timestamp: {s['final_timestamp']}")
    print()

    print("Event counts overall:")
    for et, cnt in s["event_counts"].most_common():
        print(f"  {et}: {cnt}")
    print()

    factions = set()
    for key in [
        "moves_by_faction",
        "attacks_by_faction",
        "damage_dealt_by_faction",
        "damage_taken_by_faction",
        "kills_by_faction",
        "deaths_by_faction",
        "commands_by_faction",
        "event_counts_by_faction",
    ]:
        factions.update(s[key].keys())
    factions.discard(None)

    print("Per faction:")
    for fac in sorted(factions, key=str):
        print(f"  {fac}:")
        print(f"    moves: {s['moves_by_faction'].get(fac, 0)}")
        print(f"    attacks: {s['attacks_by_faction'].get(fac, 0)}")
        print(f"    damage dealt: {s['damage_dealt_by_faction'].get(fac, 0)}")
        print(f"    damage taken: {s['damage_taken_by_faction'].get(fac, 0)}")
        print(f"    kills (inferred): {s['kills_by_faction'].get(fac, 0)}")
        print(f"    deaths: {s['deaths_by_faction'].get(fac, 0)}")
        print(f"    commands: {s['commands_by_faction'].get(fac, 0)}")
        print(
            f"    avg response time: "
            f"{s['avg_response_time_by_faction'].get(fac, 0.0):.1f}"
        )
        inv = s["invalid_by_faction"].get(fac, {})
        if inv:
            print(f"    invalid: {dict(inv)}")
        print(f"    event counts: {dict(s['event_counts_by_faction'].get(fac, {}))}")
    print()

    print("Actors:")
    for name, a in sorted(s["actors"].items()):
        print(
            f"  {name} [{a.get('faction')}]: "
            f"health {a.get('initial_health')} -> {a.get('final_health')}, "
            f"pos {a.get('position')}, "
            f"moves {a['moves']}, attacks {a['attacks']}, "
            f"dmg dealt {a['damage_dealt']}, dmg taken {a['damage_taken']}, "
            f"kills {a['kills']}, deaths {a['deaths']}, commands {a['commands']}"
        )


def main():
    parser = argparse.ArgumentParser(description="Summarise a Waves JSONL log.")
    parser.add_argument("logfile", help="Path to the Waves JSONL log file.")
    args = parser.parse_args()

    events = load_events(args.logfile)
    summary = summarise(events)
    print_summary(summary)


if __name__ == "__main__":
    main()