import sys
from collections import defaultdict
import file_utils
import json

destroyed_data = defaultdict(lambda: {
    "turn": 0,
})

factions_data = defaultdict(lambda:
                            {"llm_type": 0,
                             "count": 0,
                             "llm_model": 0,
                             "base_prompt": 0,
                             "genes_data": 0,
                             "deaths": 0,
                             "actors": [],
                             "movements": [],
                             "cmmd": {
                                 "movements": [],
                                 "attacks": [],
                                 "postMovements": []
                             },
                             "invalid": {
                                 "out_of_reach": 0
                             }
                             })

actors_data = defaultdict(lambda: {
    "faction": "",
    "death": 0,
    "attacks": 0,
    "steps": 0,
    "dead": False,
    "reasoning": [],
    "movements": [],
    "cmmd": {
        "movements": [],
        "attacks": [],
        "postMovements": []
    },
    "invalid": {
        "out_of_reach": 0
    }
})

file_info = defaultdict(lambda: {
    "level_map": "",
    "seed": 0,
    "max_turns": 0,
})


def process_info(file_name, json_data):
    level_map = json_data["map"]
    seed = json_data["randomSeed"]
    max_turns = json_data["maxTurns"]

    file_info[file_name]["level_map"] = level_map
    file_info[file_name]["seed"] = seed
    file_info[file_name]["max_turns"] = max_turns

    actors_info = json_data["navalActorEntryJsons"]

    for actor in actors_info:
        faction = actor["faction"]
        factions_data[faction]["count"] += 1
        factions_data[faction]["llm_type"] = actor["llmType"]
        factions_data[faction]["llm_model"] = actor["llmModel"]
        factions_data[faction]["base_prompt"] = actor["basePrompt"]
        factions_data[faction]["genes_data"] = actor["genesData"]
        actor_name = actor["name"]
        factions_data[faction]["actors"].append(actor_name)
        actors_data[actor_name]["faction"] = faction

    for faction in factions_data:
        print(f"{faction},{factions_data[faction]}")
    for actor in actors_data:
        print(f"{actor},{actors_data[actor]}")


def process_reason(file_name, json_data):
    actor = json_data["actorId"]
    actors_data[actor]["reasoning"].append(json_data["reasoning"])


def process_cmmd(file_name, json_data):
    actor_id = json_data["actorId"]
    actor = actors_data[actor_id]
    actor_faction = actor["faction"]
    faction = factions_data[actor_faction]
    movement = json_data["movement"]
    if movement["x"] != -1 and movement["y"] != -1:
        actor["cmmd"]["movements"].append(movement)
        faction["cmmd"]["movements"].append(movement)
    attack = json_data["attack"]
    if attack["x"] != -1 and attack["y"] != -1:
        actor["cmmd"]["attacks"].append(attack)
        faction["cmmd"]["attacks"].append(attack)
    move_after_attack = json_data["moveAfterAttack"]
    if move_after_attack["x"] != -1 and move_after_attack["y"] != -1:
        actor["cmmd"]["attacks"].append(move_after_attack)
        faction["cmmd"]["attacks"].append(move_after_attack)


def process_move(file_name, json_data):
    actor_id = json_data["actorId"]
    actor = actors_data[actor_id]
    actor_faction = actor["faction"]
    faction = factions_data[actor_faction]
    movement = json_data["moveTo"]
    if movement["x"] != -1 and movement["y"] != -1:
        actor["movements"].append(movement)
        faction["movements"].append(movement)
        move_from = json_data["moveFrom"]
        distance = abs(move_from["x"] - movement["x"]) + abs(move_from["y"] - movement["y"])
        actor["steps"] += distance


def process_invalid(file_name, json_data):
    actor_id = json_data["actorId"]
    actor = actors_data[actor_id]
    actor_faction = actor["faction"]
    faction = factions_data[actor_faction]
    match json_data["type"]:
        case "OutOfReach":
            actor["invalid"]["out_of_reach"] += 1
            faction["invalid"]["out_of_reach"] += 1


def process_dead(file_name, json_data):
    actor_id = json_data["actorId"]
    actor = actors_data.get(actor_id)
    destroyed_data[actor_id]["turn"] = json_data["turn"]

    if actor is not None:
        actor["dead"] = True
        actor_faction = actor["faction"]
        factions_data[actor_faction]["deaths"] += 1


def process_event_type(file_name, json_data):
    event_type = json_data["eventType"]
    match event_type:
        case "INFO":
            process_info(file_name, json_data)
        case "RESN":
            process_reason(file_name, json_data)
        case "CMMD":
            process_cmmd(file_name, json_data)
        case "MOVE":
            process_move(file_name, json_data)
        case "NVLD":
            process_invalid(file_name, json_data)
        case "DEAD":
            process_dead(file_name, json_data)
        case "WARNING":
            return "WARNING"

    return None


def main():
    folder = sys.argv[1]
    files, file_names = file_utils.get_valid_files_from_folder(folder, 'utf-8-sig')

    for index, file in enumerate(files):
        actors_data.clear()
        for line in file:
            json_data = json.loads(line)
            process_event_type(file_names[index], json_data)


if __name__ == "__main__":
    main()
