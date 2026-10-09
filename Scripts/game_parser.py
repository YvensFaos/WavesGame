#!/usr/bin/env python3
"""
Summarise a battle JSONL log.
Usage:
    python game_parser.py path/to/log.txt
"""

SCHEMA_VERSION = "1.0.0"

import json
import argparse
import statistics
from collections import defaultdict, Counter

# ---------------------------------------------------------------------------
# Loading
# ---------------------------------------------------------------------------

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


# ---------------------------------------------------------------------------
# Classification
# ---------------------------------------------------------------------------

def kind_is_combatant(kind):
    """Anything that isn't scenery (Target/Wave) is a combatant."""
    return kind not in ("Target", "Wave", "Unknown", None)


def kind_from_name(name):
    if not name:
        return "Unknown"
    if name.startswith("Target"):
        return "Target"
    if name.startswith("Wave"):
        return "Wave"
    if name.startswith("LLM|"):
        return "LLM"
    if name.startswith("AU|"):
        return "AU"
    if "|" in name:
        return "Human"
    return "Unknown"


def classify_actor(actor_info):
    """Extract kind + subtype metadata from an INFO navalActorEntryJsons entry."""
    prefab = actor_info.get("shipPrefabType") or ""
    name = actor_info.get("name") or ""

    kind = None
    if name.startswith("Target"):
        kind = "Target"
    elif "Llm" in prefab or name.startswith("LLM|"):
        kind = "LLM"
    elif "AI" in prefab or name.startswith("AU|"):
        kind = "AU"
    elif prefab == "NavalShip" or ("|" in name and not name.startswith(("Target", "Wave"))):
        kind = "Human"
    if kind is None:
        kind = kind_from_name(name)

    return {
        "kind": kind,
        "ship_prefab": prefab,
        "ship_data": actor_info.get("shipData"),
        "llm_type": actor_info.get("llmType"),
        "llm_model": actor_info.get("llmModel"),
        "llm_prompt": actor_info.get("basePrompt"),
        "llm_info": actor_info.get("llmInfo"),
        "machine_brain": actor_info.get("machineBrain"),
    }


def resolve_role(actors):
    """Return a single role label for a set of actors on the same side."""
    kinds = {a.get("kind") for a in actors if kind_is_combatant(a.get("kind"))}
    if not kinds:
        return "Unknown"
    if len(kinds) == 1:
        return kinds.pop()
    return "Mixed"


# ---------------------------------------------------------------------------
# Stats + actor helpers
# ---------------------------------------------------------------------------

def compute_stats(values):
    if not values:
        return {"count": 0, "sum": 0.0, "mean": 0.0, "min": 0.0, "max": 0.0, "stdev": 0.0}
    return {
        "count": len(values),
        "sum": sum(values),
        "mean": statistics.fmean(values),
        "min": min(values),
        "max": max(values),
        "stdev": statistics.pstdev(values) if len(values) > 1 else 0.0,
    }


def new_actor(name, faction=None, health=None, pos=None, meta=None):
    a = {
        "name": name,
        "faction": faction,
        "initial_health": health,
        "final_health": health,
        "position": pos,

        # Existing counters
        "moves": 0,               # MOVE events (tile steps actually taken)
        "tiles_walked": 0,        # NEW: sum of Manhattan distances across MOVE events
        "attacks": 0,             # ATTK events (successful attacks)
        "damage_dealt": 0.0,      # ATTK damage (potential)
        "damage_dealt_actual": 0.0,  # NEW: DAMG damage attributed to attacker
        "damage_taken": 0.0,
        "kills": 0,
        "deaths": 0,
        "commands": 0,
        "response_times": [],

        # NEW: attempted vs failed
        "moves_attempted": 0,
        "moves_failed": 0,
        "attacks_attempted": 0,
        "attacks_failed": 0,

        # NEW: friendly fire / wave metrics
        "friendly_fire_attacks": 0,
        "self_damage_attacks": 0,
        "wave_attacks": 0,

        # NEW: kill links
        "killed_by": [],
        "killed": [],
    }
    if meta:
        a.update(meta)
    return a


def get_actor(summary, actor_id, faction=None):
    if actor_id not in summary["actors"]:
        summary["actors"][actor_id] = new_actor(
            actor_id, faction, meta={"kind": kind_from_name(actor_id)}
        )
    return summary["actors"][actor_id]


# ---------------------------------------------------------------------------
# Main summariser
# ---------------------------------------------------------------------------

def summarise(events):
    summary = {
        # Battle metadata
        "map": None,
        "randomSeed": None,
        "maxTurns": None,
        "winner": None,
        "winner_message": None,
        "first_timestamp": None,
        "final_timestamp": 0,
        "final_turn": 0,
        "battle_duration": 0,

        # Scenario
        "fleet_sizes": {},
        "scenario_shape": None,
        "waves": [],
        "wave_count": 0,
        "neutral_count": 0,
        "neutral_destroyed": 0,
        "neutral_alive": 0,
        "wave_actor_count": 0,
        "wave_actor_damage_taken": 0.0,

        # Matchup / winner
        "faction_roles": {},
        "matchup_type": None,
        "matchup_label": None,
        "is_draw": False,
        "win_reason": None,
        "winner_side": None,
        "winner_role": None,
        "winner_model": None,
        "loser_side": None,
        "loser_role": None,
        "llm_side": None,
        "llm_won": None,
        "num_llm_actors": 0,
        "num_nonllm_actors": 0,

        # Event counters
        "event_counts": Counter(),
        "event_counts_by_faction": defaultdict(Counter),

        # Faction counters
        "moves_by_faction": Counter(),
        "tiles_walked_by_faction": Counter(),
        "attacks_by_faction": Counter(),
        "damage_dealt_by_faction": Counter(),           # legacy (ATTK)
        "damage_dealt_actual_by_faction": Counter(),    # NEW (DAMG)
        "damage_taken_by_faction": Counter(),
        "kills_by_faction": Counter(),
        "deaths_by_faction": Counter(),
        "commands_by_faction": Counter(),
        "invalid_by_faction": defaultdict(Counter),

        # NEW: friendly-fire / wave-by-faction
        "friendly_fire_by_faction": Counter(),
        "self_damage_by_faction": Counter(),
        "wave_attacks_by_faction": Counter(),
        "wave_friendly_fire_by_faction": Counter(),

        # Response times
        "response_times_by_faction": defaultdict(list),
        "response_stats_by_faction": {},
        "response_stats_overall": {},

        # Actors + kill edges
        "actors": {},
        "kills": [],
    }

    last_attacker_by_target = {}   # target_id -> (attacker_id, faction, turn)
    wave_kill_victims = set()      # victims whose death came via PPGT

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
            ts = ev["timeStamp"]
            if summary["first_timestamp"] is None:
                summary["first_timestamp"] = ts
            if ts > summary["final_timestamp"]:
                summary["final_timestamp"] = ts

        # ------------------------------------------------------------------
        if et == "INFO":
            summary["map"] = ev.get("map")
            summary["randomSeed"] = ev.get("randomSeed")
            summary["maxTurns"] = ev.get("maxTurns")

            summary["waves"] = [
                {
                    "name": w.get("name"),
                    "direction": w.get("direction"),
                    "damage": w.get("damage"),
                    "aoe": w.get("areaOfEffect"),
                }
                for w in ev.get("waveActorEntryJsons", [])
            ]
            summary["wave_count"] = len(summary["waves"])

            # Pre-register waves as actors so they show up even if untouched.
            for w in ev.get("waveActorEntryJsons", []):
                wname = w.get("name")
                if not wname:
                    continue
                summary["actors"][wname] = new_actor(
                    wname,
                    faction="Neutral",
                    health=None,
                    pos=None,
                    meta={
                        "kind": "Wave",
                        "wave_direction": w.get("direction"),
                        "wave_damage": w.get("damage"),
                        "wave_aoe": w.get("areaOfEffect"),
                    },
                )

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

        # ------------------------------------------------------------------
        elif et == "OVER":
            summary["winner"] = (ev.get("winningFaction") or "").strip()
            summary["winner_message"] = (ev.get("goalMessage") or "").strip()

        # ------------------------------------------------------------------
        elif et == "GSTT":
            for actor in ev.get("navalActorEntryJsons", []):
                name = actor.get("name")
                if not name:
                    continue
                a = get_actor(summary, name, actor.get("faction"))
                a["faction"] = actor.get("faction", a["faction"])
                a["final_health"] = actor.get("currentHealth", a["final_health"])
                a["position"] = actor.get("position", a["position"])

        # ------------------------------------------------------------------
        elif et == "CMMD":
            if actor_id:
                a = get_actor(summary, actor_id, faction)
                a["commands"] += 1
                move = ev.get("movement") or {}
                atk = ev.get("attack") or {}
                if move.get("x", -1) != -1 or move.get("y", -1) != -1:
                    a["moves_attempted"] += 1
                if atk.get("x", -1) != -1 or atk.get("y", -1) != -1:
                    a["attacks_attempted"] += 1
            if faction:
                summary["commands_by_faction"][faction] += 1

        # ------------------------------------------------------------------
        elif et == "MOVE":
            if actor_id:
                a = get_actor(summary, actor_id, faction)
                a["moves"] += 1

                mf = ev.get("moveFrom") or {}
                mt = ev.get("moveTo") or {}
                if "x" in mf and "x" in mt:
                    dist = abs(mt["x"] - mf["x"]) + abs(mt["y"] - mf["y"])
                else:
                    dist = 1  # fallback: treat each MOVE event as one tile
                a["tiles_walked"] += dist

            if faction:
                summary["moves_by_faction"][faction] += 1
                summary["tiles_walked_by_faction"][faction] += dist if actor_id else 0

        # ------------------------------------------------------------------
        elif et == "ATTK":
            damage = ev.get("damage", 0) or 0
            target_id = ev.get("targetId")

            if actor_id:
                a = get_actor(summary, actor_id, faction)
                a["attacks"] += 1
                a["damage_dealt"] += damage

                if target_id:
                    # Friendly-fire check
                    if target_id == actor_id:
                        a["self_damage_attacks"] += 1
                        if faction:
                            summary["self_damage_by_faction"][faction] += 1
                    else:
                        target = summary["actors"].get(target_id)
                        if target and faction and target.get("faction") == faction:
                            a["friendly_fire_attacks"] += 1
                            if faction:
                                summary["friendly_fire_by_faction"][faction] += 1

                    # Wave attack check
                    if target_id.startswith("Wave"):
                        a["wave_attacks"] += 1
                        if faction:
                            summary["wave_attacks_by_faction"][faction] += 1

            if faction:
                summary["attacks_by_faction"][faction] += 1
                summary["damage_dealt_by_faction"][faction] += damage

            if target_id:
                last_attacker_by_target[target_id] = (
                    actor_id, faction, ev.get("turn")
                )

        # ------------------------------------------------------------------
        elif et == "PPGT":
            # Wave propagation: transfer attribution from the wave to whoever
            # attacked the wave, so wave-kills aren't miscredited.
            origin = ev.get("originId")
            target = ev.get("targetId")
            if origin and target:
                origin_attacker = last_attacker_by_target.get(origin)
                if origin_attacker:
                    last_attacker_by_target[target] = origin_attacker
                    wave_kill_victims.add(target)

                    # Wave-friendly-fire detection
                    att_id, att_fac, _ = origin_attacker
                    victim = summary["actors"].get(target)
                    if (victim and att_fac
                            and victim.get("faction") == att_fac):
                        summary["wave_friendly_fire_by_faction"][att_fac] += 1

        # ------------------------------------------------------------------
        elif et == "DAMG":
            damage = ev.get("damage", 0) or 0
            if actor_id:
                a = get_actor(summary, actor_id, faction)
                a["damage_taken"] += damage

                # Attribute the damage to the last attacker of this actor.
                attacker = last_attacker_by_target.get(actor_id)
                if attacker:
                    att_id, att_fac, _ = attacker
                    if att_id:
                        aa = get_actor(summary, att_id, att_fac)
                        aa["damage_dealt_actual"] += damage
                    if att_fac:
                        summary["damage_dealt_actual_by_faction"][att_fac] += damage

            if faction:
                summary["damage_taken_by_faction"][faction] += damage

        # ------------------------------------------------------------------
        elif et == "DEAD":
            if actor_id:
                a = get_actor(summary, actor_id, faction)
                a["deaths"] += 1
                a["final_health"] = 0
                a["position"] = {"x": -1, "y": -1}
            if faction:
                summary["deaths_by_faction"][faction] += 1

            killer = last_attacker_by_target.get(actor_id)
            if killer:
                killer_id, killer_faction, killer_turn = killer
                method = ("wave_propagation"
                          if actor_id in wave_kill_victims
                          else "direct_attack")

                if killer_id:
                    ka = get_actor(summary, killer_id, killer_faction)
                    ka["kills"] += 1
                    ka["killed"].append({
                        "victim_id": actor_id,
                        "victim_faction": faction,
                        "turn": killer_turn,
                        "method": method,
                    })
                if killer_faction:
                    summary["kills_by_faction"][killer_faction] += 1

                victim = summary["actors"].get(actor_id)
                if victim:
                    victim["killed_by"].append({
                        "killer_id": killer_id,
                        "killer_faction": killer_faction,
                        "turn": killer_turn,
                        "method": method,
                    })

                summary["kills"].append({
                    "turn": killer_turn,
                    "killer_id": killer_id,
                    "killer_faction": killer_faction,
                    "victim_id": actor_id,
                    "victim_faction": faction,
                    "method": method,
                })

        # ------------------------------------------------------------------
        elif et == "RESN":
            rt = ev.get("responseTime")
            if rt is not None:
                if actor_id:
                    a = get_actor(summary, actor_id, faction)
                    a["response_times"].append(rt)
                if faction:
                    summary["response_times_by_faction"][faction].append(rt)

        # ------------------------------------------------------------------
        elif et == "NVLD":
            if actor_id:
                a = get_actor(summary, actor_id, faction)
                vtype = ev.get("type", "unknown")
                if vtype == "FailedToMove":
                    a["moves_failed"] += 1
                elif vtype == "OutOfReach":
                    a["attacks_failed"] += 1
            if faction:
                summary["invalid_by_faction"][faction][
                    ev.get("type", "unknown")
                ] += 1

    # ------------------------------------------------------------------
    # Post-processing
    # ------------------------------------------------------------------

    # Response time stats
    for fac, times in summary["response_times_by_faction"].items():
        summary["response_stats_by_faction"][fac] = compute_stats(times)
    all_times = [t for times in summary["response_times_by_faction"].values()
                 for t in times]
    summary["response_stats_overall"] = compute_stats(all_times)

    # Battle duration
    if summary["first_timestamp"] is not None:
        summary["battle_duration"] = (
            summary["final_timestamp"] - summary["first_timestamp"]
        )

    # Fleet sizes + scenario shape
    sizes = Counter()
    for a in summary["actors"].values():
        if a.get("faction") and kind_is_combatant(a.get("kind")):
            sizes[a["faction"]] += 1
    summary["fleet_sizes"] = dict(sizes)
    if sizes:
        counts = sorted(sizes.values(), reverse=True)
        summary["scenario_shape"] = "v".join(str(v) for v in counts)

    # Neutral / wave counts
    neutral_names = [n for n in summary["actors"] if n.startswith("Target")]
    wave_names = [n for n in summary["actors"] if n.startswith("Wave")]

    summary["neutral_count"] = len(neutral_names)
    summary["neutral_destroyed"] = sum(
        1 for n in neutral_names if summary["actors"][n].get("deaths", 0) > 0
    )
    summary["neutral_alive"] = (
        summary["neutral_count"] - summary["neutral_destroyed"]
    )
    summary["wave_actor_count"] = len(wave_names)
    summary["wave_actor_damage_taken"] = sum(
        summary["actors"][n].get("damage_taken", 0) for n in wave_names
    )

    # LLM / non-LLM actor counts
    summary["num_llm_actors"] = sum(
        1 for a in summary["actors"].values() if a.get("kind") == "LLM"
    )
    summary["num_nonllm_actors"] = sum(
        1 for a in summary["actors"].values()
        if kind_is_combatant(a.get("kind")) and a.get("kind") != "LLM"
    )

    # Faction roles + matchup
    faction_roles = {}
    for fac in sizes:
        fac_actors = [a for a in summary["actors"].values()
                      if a.get("faction") == fac]
        faction_roles[fac] = resolve_role(fac_actors)
    summary["faction_roles"] = faction_roles

    roles_sorted = sorted(set(faction_roles.values()))
    if len(roles_sorted) >= 2:
        summary["matchup_type"] = "_vs_".join(roles_sorted)
    elif len(roles_sorted) == 1:
        summary["matchup_type"] = roles_sorted[0] + "_mirror"
    else:
        summary["matchup_type"] = "Unknown"

    labels = []
    for fac in sorted(faction_roles.keys()):
        role = faction_roles[fac]
        if role == "LLM":
            models = sorted({
                f"{a.get('llm_type')}/{a.get('llm_model')}"
                for a in summary["actors"].values()
                if a.get("faction") == fac and a.get("kind") == "LLM"
            })
            labels.append(f"LLM({','.join(models)})")
        elif role == "AU":
            brains = sorted({
                a.get("machine_brain")
                for a in summary["actors"].values()
                if a.get("faction") == fac and a.get("kind") == "AU"
            })
            labels.append(f"AI({','.join(b for b in brains if b)})")
        elif role == "Human":
            ships = sorted({
                a.get("ship_data")
                for a in summary["actors"].values()
                if a.get("faction") == fac and a.get("kind") == "Human"
            })
            labels.append(f"Human({','.join(s for s in ships if s)})")
        else:
            labels.append(role)
    summary["matchup_label"] = " vs ".join(labels)

    # Winner / loser resolution
    if summary["winner"] and " is " in summary["winner"]:
        winner_faction = summary["winner"].rsplit(" is ", 1)[1].rstrip("!").strip()
        summary["winner_side"] = winner_faction
        summary["winner_role"] = faction_roles.get(winner_faction)

        models = sorted({
            a.get("llm_model") or a.get("machine_brain") or a.get("ship_data")
            for a in summary["actors"].values()
            if a.get("faction") == winner_faction
            and kind_is_combatant(a.get("kind"))
        })
        summary["winner_model"] = ", ".join(m for m in models if m) or winner_faction

        for fac in faction_roles:
            if fac != winner_faction:
                summary["loser_side"] = fac
                summary["loser_role"] = faction_roles[fac]
                break

        summary["win_reason"] = "elimination"

    if (summary["winner"] is None
            and summary["maxTurns"]
            and summary["final_turn"] >= summary["maxTurns"]):
        summary["is_draw"] = True
        summary["win_reason"] = "draw_max_turns"

    # LLM side / won
    for fac, role in faction_roles.items():
        if role == "LLM":
            summary["llm_side"] = fac
            break
    if summary["llm_side"] and summary["winner_side"]:
        summary["llm_won"] = (summary["llm_side"] == summary["winner_side"])

    return summary


# ---------------------------------------------------------------------------
# Rendering
# ---------------------------------------------------------------------------

def describe_combatants(summary):
    factions = sorted({
        a.get("faction")
        for a in summary["actors"].values()
        if kind_is_combatant(a.get("kind")) and a.get("faction")
    })
    if not factions:
        return "  (no combatants found)"

    lines = []
    for fac in factions:
        role = summary["faction_roles"].get(fac, "?")
        actors = [a for a in summary["actors"].values() if a.get("faction") == fac]
        parts = []

        llms = [a for a in actors if a.get("kind") == "LLM"]
        if llms:
            models = sorted({
                f"{a.get('llm_type')}/{a.get('llm_model')}"
                for a in llms if a.get("llm_type") or a.get("llm_model")
            })
            prompts = sorted({a.get("llm_prompt") for a in llms if a.get("llm_prompt")})
            parts.append(f"{len(llms)} LLM ({', '.join(models)}; prompts: {', '.join(prompts)})")

        aus = [a for a in actors if a.get("kind") == "AU"]
        if aus:
            brains = sorted({a.get("machine_brain") for a in aus if a.get("machine_brain")})
            parts.append(f"{len(aus)} AU ({', '.join(brains)})")

        humans = [a for a in actors if a.get("kind") == "Human"]
        if humans:
            ships = sorted({a.get("ship_data") or a.get("ship_prefab") or "?"
                            for a in humans})
            parts.append(f"{len(humans)} Human ({', '.join(ships)})")

        lines.append(f"  {fac} [{role}]: {' + '.join(parts) if parts else '?'}")
    return "\n".join(lines)


def describe_faction_composition(summary, faction):
    actors = [a for a in summary["actors"].values() if a.get("faction") == faction]
    if not actors:
        return f"  (no actor metadata found for faction '{faction}')"

    combatants = [a for a in actors if kind_is_combatant(a.get("kind"))]
    kinds = Counter(a.get("kind", "Unknown") for a in combatants)
    lines = [f"  Actors: {len(combatants)} -> {dict(kinds)}"]

    llms = [a for a in combatants if a.get("kind") == "LLM"]
    if llms:
        providers = sorted({a.get("llm_type") for a in llms if a.get("llm_type")})
        models = sorted({a.get("llm_model") for a in llms if a.get("llm_model")})
        prompts = sorted({a.get("llm_prompt") for a in llms if a.get("llm_prompt")})
        lines.append(f"    LLM providers: {providers}")
        lines.append(f"    LLM models:    {models}")
        lines.append(f"    LLM prompts:   {prompts}")

    aus = [a for a in combatants if a.get("kind") == "AU"]
    if aus:
        brains = sorted({a.get("machine_brain") for a in aus if a.get("machine_brain")})
        lines.append(f"    AI brains:     {brains}")

    humans = [a for a in combatants if a.get("kind") == "Human"]
    if humans:
        ships = sorted({a.get("ship_data") or "?" for a in humans})
        lines.append(f"    Human ships:   {ships}")

    return "\n".join(lines)


def print_summary(s):
    print("=== Battle Summary ===")
    print(f"Map: {s['map']}")
    print(f"Seed: {s['randomSeed']}  MaxTurns: {s['maxTurns']}")
    print(f"Winner: {s['winner']}")
    print(f"Goal: {s['winner_message']}")
    print(f"Matchup: {s['matchup_type']}  ->  {s['matchup_label']}")
    print(
        f"Winner side: {s['winner_side']} "
        f"({s['winner_role']}, model={s['winner_model']})  "
        f"Win reason: {s['win_reason']}  Draw: {s['is_draw']}"
    )
    if s["llm_side"]:
        print(
            f"LLM side: {s['llm_side']}  "
            f"LLM won: {s['llm_won']}  "
            f"(#LLM actors: {s['num_llm_actors']}, "
            f"#non-LLM actors: {s['num_nonllm_actors']})"
        )
    print()

    print("Combatants:")
    print(describe_combatants(s))
    print()

    overall = s["response_stats_overall"]
    if overall["count"]:
        print(
            f"Response times (overall, {overall['count']} responses, ms): "
            f"sum={overall['sum']:.1f}, mean={overall['mean']:.1f}, "
            f"min={overall['min']:.1f}, max={overall['max']:.1f}, "
            f"stdev={overall['stdev']:.1f}"
        )
        print()

    shape = s["scenario_shape"] or "?"
    wave_desc = ", ".join(
        f"{w['name']} ({w.get('direction', '?')}, dmg {w.get('damage', '?')})"
        for w in s["waves"]
    ) or "none"
    print(
        f"Scenario: {shape} "
        f"({', '.join(f'{f}: {n}' for f, n in s['fleet_sizes'].items())}), "
        f"{s['wave_count']} wave(s): [{wave_desc}], "
        f"{s['neutral_count']} neutral(s) "
        f"({s['neutral_destroyed']} destroyed, {s['neutral_alive']} alive)"
    )

    if s["winner_side"]:
        print(f"Winning faction composition ({s['winner_side']}):")
        print(describe_faction_composition(s, s["winner_side"]))

    print(f"Final turn: {s['final_turn']}")
    print(
        f"First ts: {s['first_timestamp']}  "
        f"Final ts: {s['final_timestamp']}  "
        f"Duration: {s['battle_duration']}"
    )
    print()

    print("Event counts overall:")
    for et, cnt in s["event_counts"].most_common():
        print(f"  {et}: {cnt}")
    print()

    # ----- Per-faction block -----
    factions = set()
    for key in [
        "moves_by_faction",
        "attacks_by_faction",
        "damage_dealt_by_faction",
        "damage_dealt_actual_by_faction",
        "damage_taken_by_faction",
        "kills_by_faction",
        "deaths_by_faction",
        "commands_by_faction",
        "event_counts_by_faction",
        "friendly_fire_by_faction",
        "self_damage_by_faction",
        "wave_attacks_by_faction",
        "wave_friendly_fire_by_faction",
    ]:
        factions.update(s[key].keys())
    factions.discard(None)

    print("Per faction:")
    for fac in sorted(factions, key=str):
        role = s["faction_roles"].get(fac, "?")
        print(f"  {fac} [{role}]:")
        print(f"    moves (events): {s['moves_by_faction'].get(fac, 0)}")
        print(f"    tiles walked:   {s['tiles_walked_by_faction'].get(fac, 0)}")
        print(f"    attacks (successful): {s['attacks_by_faction'].get(fac, 0)}")
        print(
            f"    damage dealt (potential, ATTK): "
            f"{s['damage_dealt_by_faction'].get(fac, 0)}"
        )
        print(
            f"    damage dealt (actual, DAMG):    "
            f"{s['damage_dealt_actual_by_faction'].get(fac, 0)}"
        )
        print(f"    damage taken: {s['damage_taken_by_faction'].get(fac, 0)}")
        print(f"    kills (inferred): {s['kills_by_faction'].get(fac, 0)}")
        print(f"    deaths: {s['deaths_by_faction'].get(fac, 0)}")
        print(f"    commands: {s['commands_by_faction'].get(fac, 0)}")
        print(
            f"    friendly fire attacks: "
            f"{s['friendly_fire_by_faction'].get(fac, 0)}"
        )
        print(
            f"    self-damage attacks:   "
            f"{s['self_damage_by_faction'].get(fac, 0)}"
        )
        print(
            f"    wave attacks:          "
            f"{s['wave_attacks_by_faction'].get(fac, 0)}"
        )
        print(
            f"    wave friendly fire:    "
            f"{s['wave_friendly_fire_by_faction'].get(fac, 0)}"
        )

        rs = s["response_stats_by_faction"].get(fac)
        if rs and rs["count"]:
            print(
                f"    response times (ms, {rs['count']}): "
                f"sum={rs['sum']:.1f}, mean={rs['mean']:.1f}, "
                f"min={rs['min']:.1f}, max={rs['max']:.1f}, "
                f"stdev={rs['stdev']:.1f}"
            )
        inv = s["invalid_by_faction"].get(fac, {})
        if inv:
            print(f"    invalid: {dict(inv)}")
        print(f"    event counts: {dict(s['event_counts_by_faction'].get(fac, {}))}")
    print()

    # ----- Kills -----
    if s["kills"]:
        print("Kill edges:")
        for k in s["kills"]:
            print(
                f"  turn {k['turn']}: "
                f"{k['killer_id']} [{k['killer_faction']}] -> "
                f"{k['victim_id']} [{k['victim_faction']}] "
                f"({k['method']})"
            )
        print()

    # ----- Actors -----
    print("Actors:")
    for name, a in sorted(s["actors"].items()):
        extra = ""
        if a.get("kind") == "Wave":
            extra = (f", wave dmg {a.get('wave_damage')} "
                     f"dir {a.get('wave_direction')}")

        kind = a.get("kind", "?")

        if kind == "LLM":
            moves_str = (
                f"moves {a['moves']}/{a.get('moves_attempted', 0)} "
                f"(fail {a.get('moves_failed', 0)}) "
                f"tiles {a.get('tiles_walked', 0)}"
            )
            attacks_str = (
                f"attacks {a['attacks']}/{a.get('attacks_attempted', 0)} "
                f"(fail {a.get('attacks_failed', 0)})"
            )
        else:
            moves_str = f"moves {a['moves']} tiles {a.get('tiles_walked', 0)}"
            attacks_str = f"attacks {a['attacks']}"

        print(
            f"  {name} [{a.get('faction')}] ({kind}): "
            f"hp {a.get('initial_health')} -> {a.get('final_health')}, "
            f"pos {a.get('position')}, "
            f"{moves_str}, "
            f"{attacks_str}, "
            f"dmg dealt {a['damage_dealt_actual']:.1f}/{a['damage_dealt']:.1f} "
            f"(actual/potential), "
            f"dmg taken {a['damage_taken']}, "
            f"kills {a['kills']}, deaths {a['deaths']}, "
            f"commands {a['commands']}, "
            f"FF {a.get('friendly_fire_attacks', 0)}, "
            f"self {a.get('self_damage_attacks', 0)}, "
            f"wave-atk {a.get('wave_attacks', 0)}"
            f"{extra}"
        )

        if a.get("killed_by"):
            for kb in a["killed_by"]:
                print(
                    f"      killed_by {kb['killer_id']} "
                    f"[{kb['killer_faction']}] "
                    f"turn {kb['turn']} ({kb['method']})"
                )
        if a.get("killed"):
            for kd in a["killed"]:
                print(
                    f"      killed {kd['victim_id']} "
                    f"[{kd['victim_faction']}] "
                    f"turn {kd['turn']} ({kd['method']})"
                )

def to_jsonable(obj):
    """Recursively convert Counter / defaultdict / set to JSON-friendly types."""
    if isinstance(obj, Counter):
        return dict(obj)
    if isinstance(obj, defaultdict):
        return {k: to_jsonable(v) for k, v in obj.items()}
    if isinstance(obj, dict):
        return {str(k): to_jsonable(v) for k, v in obj.items()}
    if isinstance(obj, (list, tuple)):
        return [to_jsonable(v) for v in obj]
    if isinstance(obj, set):
        return sorted(to_jsonable(v) for v in obj)
    return obj


def summary_to_jsonl(summary, source_file=None, lean=False):
    s = dict(summary)
    if lean:
        s.pop("actors", None)
        s.pop("kills", None)
    payload = {
        "schema_version": SCHEMA_VERSION,
        "source_file": source_file,
        "summary": to_jsonable(s),
    }
    return json.dumps(payload, ensure_ascii=False, separators=(",", ":"))

def main():
    parser = argparse.ArgumentParser(description="Summarise a Battleground JSONL log.")
    parser.add_argument("logfile", help="Path to the Battleground JSONL log.")
    parser.add_argument(
        "--format",
        choices=("text", "jsonl", "both"),
        default="text",
        help="Output format (default: text).",
    )
    parser.add_argument(
        "--lean",
        action="store_true",
        help="Drop per-actor and per-kill data from JSONL output.",
    )
    args = parser.parse_args()

    events = load_events(args.logfile)
    summary = summarise(events)

    if args.format in ("text", "both"):
        print_summary(summary)
    if args.format in ("jsonl", "both"):
        print(summary_to_jsonl(
            summary,
            source_file=args.logfile,
            lean=args.lean,
        ))

if __name__ == "__main__":
    main()