#!/usr/bin/env python3
"""
Parse a combat log file and output a CSV line with metrics for the Red (LLM) agent(s).
Usage: python parse_combat.py <logfile>
Example: python parse_combat.py AI_Wars_Green_vs_Red.log >> results.csv
"""

import sys
import re
from collections import defaultdict

def parse_log_file(filepath, delimiter=';'):
    # Data structures for Red agents (key = agent ID, e.g., "2", "1", "5")
    red_agents = defaultdict(lambda: {
        'movement_attempts': 0,
        'attack_attempts': 0,
        'successful_attacks': 0,
        'successful_attack_targets': [],
        'last_target_type': None,
        'kills': 0,
        'avg_request_time': None,
        'request_count': 0
    })

    winner = None
    model_name = None

    # Regex to extract Red agent ID from log line
    # The ID appears as |Red|<digits>]  (e.g., |Red|2])
    red_id_pattern = re.compile(r'\|Red\|(\d+)\]')

    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    # First pass: find model name from any Red agent line
    for line in lines:
        if 'LLMAgent' in line and '|Red|' in line:
            # Extract everything between 'LLMAgent|' and '|Red|'
            # Example: [LLMAgent|DeepSeek|deepseek-v4-flash|Red|2]
            match = re.search(r'LLMAgent\|([^|]+)\|([^|]+)\|Red\|', line)
            if match:
                # match.group(1) = provider (e.g., DeepSeek, OpenAI)
                # match.group(2) = model name (e.g., deepseek-v4-flash, gpt-4)
                model_name = match.group(2)
                break

    # If still not found, fallback to a simple split
    if not model_name:
        for line in lines:
            if 'LLMAgent' in line and '|Red|' in line:
                parts = line.split('|')
                if len(parts) >= 4:
                    model_name = parts[2]   # as in original attempt
                    break

    # Second pass: parse each line
    for line in lines:
        # Winner detection
        if 'LevelGoal' in line and 'won' in line:
            if 'Green won' in line:
                winner = 'Green'
            elif 'Red won' in line:
                winner = 'Red'

        # Only process lines belonging to Red agents
        if 'LLMAgent' not in line or '|Red|' not in line:
            continue

        # Extract agent ID
        id_match = red_id_pattern.search(line)
        if not id_match:
            continue
        agent_id = id_match.group(1)
        agent = red_agents[agent_id]

        # 1) Movement attempts (MOVE line)
        if 'MOVE {' in line:
            agent['movement_attempts'] += 1

        # 2) Attack attempts (ATTK line)
        if 'ATTK {' in line:
            agent['attack_attempts'] += 1

        # 3) Target type (TRGT line) - store for the next successful attack
        if 'TRGT' in line:
            if 'TRGT TARGET' in line:
                agent['last_target_type'] = 'TARGET'
            elif 'TRGT WAVE' in line:
                agent['last_target_type'] = 'WAVE'
            elif 'TRGT LLM {ENEMY}' in line:
                agent['last_target_type'] = 'ENEMY'
            else:
                agent['last_target_type'] = 'UNKNOWN'

        # 4) Successful attack (Attacked succeeded)
        if 'Attacked succeeded' in line:
            agent['successful_attacks'] += 1
            if agent['last_target_type']:
                agent['successful_attack_targets'].append(agent['last_target_type'])
            else:
                agent['successful_attack_targets'].append('UNKNOWN')

        # 5) Kills and request times from the DATA summary line (when agent is destroyed)
        if 'DATA {"internalWrongMovementCount"' in line:
            kills_match = re.search(r'"kills":(\d+)', line)
            if kills_match:
                agent['kills'] = int(kills_match.group(1))
            req_time_match = re.search(r'"averageRequestTime":([\d.]+)', line)
            if req_time_match:
                agent['avg_request_time'] = float(req_time_match.group(1))
            req_count_match = re.search(r'"averageRequestTimeCount":(\d+)', line)
            if req_count_match:
                agent['request_count'] = int(req_count_match.group(1))

    # Aggregate across all Red agents
    total_movements = sum(a['movement_attempts'] for a in red_agents.values())
    total_attack_attempts = sum(a['attack_attempts'] for a in red_agents.values())
    total_successful_attacks = sum(a['successful_attacks'] for a in red_agents.values())
    total_kills = sum(a['kills'] for a in red_agents.values())

    # Count enemy-targeted successful attacks
    enemy_targeted_success = sum(
        sum(1 for t in a['successful_attack_targets'] if t == 'ENEMY')
        for a in red_agents.values()
    )
    pct_enemy_targeted = enemy_targeted_success / total_successful_attacks if total_successful_attacks > 0 else 0.0

    # Weighted average request time
    total_req_time = 0
    total_req_count = 0
    for a in red_agents.values():
        if a['avg_request_time'] is not None and a['request_count'] > 0:
            total_req_time += a['avg_request_time'] * a['request_count']
            total_req_count += a['request_count']
    avg_request_time = total_req_time / total_req_count if total_req_count > 0 else None

    red_won = 1 if winner == 'Red' else 0

    # Output CSV line
    output = f"{filepath}{delimiter}{model_name}{delimiter}{red_won}{delimiter}{total_movements}{delimiter}{total_attack_attempts}{delimiter}{total_successful_attacks}{delimiter}{pct_enemy_targeted:.4f}{delimiter}{total_kills}{delimiter}{avg_request_time if avg_request_time else ''}"
    print(output)

if __name__ == '__main__':
    if len(sys.argv) != 2:
        print("Usage: python regresser_csv.py <logfile>", file=sys.stderr)
        sys.exit(1)
    parse_log_file(sys.argv[1])