#!/bin/bash
python3 renamer.py ProcessLogs
./parse_all_logs_from.sh ProcessLogs ProcessedLogs
./parse_model_logs.sh ProcessLogs ProcessedModelLogs