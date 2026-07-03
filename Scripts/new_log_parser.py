import sys
from collections import defaultdict
import file_utils
import json

actors_data = defaultdict(lambda:
                          {"llm_type": 0,
                           "count": 0,
                           "llm_type": 0,
                           "llm_model": 0,
                           "base_prompt": 0,
                           "genes_data": 0,
                           "actors": []
                           })

# def get_faction_from_name(name):
#     for faction in actors_data:
#         if faction["actors"]

def process_info(json_data):
    level_map = json_data["map"]
    seed = json_data["randomSeed"]
    max_turns = json_data["maxTurns"]
    print(f"{level_map},{seed},{max_turns}")
    actors_info = json_data["navalActorEntryJsons"]

    for actor in actors_info:
        faction = actor["faction"]
        actors_data[faction]["count"] += 1
        actors_data[faction]["llm_type"] = actor["llmType"]
        actors_data[faction]["llm_model"] = actor["llmModel"]
        actors_data[faction]["base_prompt"] = actor["basePrompt"]
        actors_data[faction]["genes_data"] = actor["genesData"]
        actors_data[faction]["actors"].append(actor["name"])

    for faction in actors_data:
        print(f"{faction},{actors_data[faction]}")

def process_event_type(json_data):
    event_type = json_data["eventType"]
    match event_type:
        case "INFO":
            process_info(json_data)
        case "WARNING":
            return "WARNING"
    return None


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
