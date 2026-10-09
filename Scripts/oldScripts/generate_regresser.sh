#!/bin/bash

if [ $# -ne 2 ]; then
    echo "Usage: $0 INPUT_FOLDER OUTPUT_FOLDER"
    exit 1
fi

INPUT_FOLDER="$1"
OUTPUT_FOLDER="$2"

if [ ! -d "$INPUT_FOLDER" ]; then
    echo "Error: Input folder '$INPUT_FOLDER' does not exist"
    exit 1
fi

# Create output folder if it doesn't exist
mkdir -p "$OUTPUT_FOLDER"

OUTPUT_FILE="$OUTPUT_FOLDER/regression.csv"

echo "file;model;win;movements;attack_attempts;successful_attacks;pct_enemy_targeted;kills;avg_request_time" > "$OUTPUT_FILE"

# Process each log file in the input folder
for LOG_FILE in "$INPUT_FOLDER"/*.log; do
    # Skip if no .log files found
    if [ ! -e "$LOG_FILE" ]; then
        echo "No .log files found in $INPUT_FOLDER"
        break
    fi

    # Get the base filename without path
    FILENAME=$(basename "$LOG_FILE")
    echo "Processing: $FILENAME"
    python3 regresser_csv.py "$LOG_FILE" >> "$OUTPUT_FILE"

    # Check if the command succeeded
    if [ $? -eq 0 ]; then
        echo "  Success"
    else
        echo "  Failed"
    fi
done

echo "Processing complete!"