import sys
from collections import defaultdict
import file_utils
import json

factions_data = defaultdict(lambda:
                          {"llm_type": 0,
                           "count": 0,
                           "llm_model": 0,
                           "base_prompt": 0,
                           "genes_data": 0,
                           "actors": []
                           })

actors_data = defaultdict(lambda: {
    "faction": "",
    "death": 0,
    "attacks": 0
})

def process_info(json_data):
    level_map = json_data["map"]
    seed = json_data["randomSeed"]
    max_turns = json_data["maxTurns"]
    print(f"{level_map},{seed},{max_turns}")
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

def process_reason(json_data):


def process_event_type(json_data):
    event_type = json_data["eventType"]
    match event_type:
        case "INFO":
            process_info(json_data)
        case "RESN":
            process_reason(json_data)
        case "WARNING":
            return "WARNING"
    return None

#{"eventType":"RESN","reasoning":"Move to (10,6) to get enemy at (10,13) within attack range (Manhattan distance 2) and attack it, then stay in place.","actorId":"LLMAgent|DeepSeek|deepseek-chat|Green|1","turn":0,"timeStamp":2}


def main():
    folder = sys.argv[1]
    files = file_utils.get_valid_files_from_folder(folder, 'utf-8-sig')

    for file in files:
        actors_data.clear()
        for line in file:
            json_data = json.loads(line)
            process_event_type(json_data)


if __name__ == "__main__":
    main()
