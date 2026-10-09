import sys
import file_utils
import re

def extract_distance(line):
    # 2026-02-12 00:04:49;[ERROR];Could not find path from (10, 7) to (10, 11).
    # Extract all coordinate pairs like (x, y)
    pattern = r'\((\d+),\s*(\d+)\)'
    matches = re.findall(pattern, line)

    if len(matches) >= 2:
        # Convert to integers
        (x1, y1) = map(int, matches[0])
        (x2, y2) = map(int, matches[1])

        # Manhattan distance
        distance = abs(x1 - x2) + abs(y1 - y2)
        print(f"Manhattan distance between {matches[0]} and {matches[1]}: {distance}")

        return distance
    else:
        return -1

def main():
    folder = sys.argv[1]
    files = file_utils.get_valid_files_from_folder(folder)
    output_lines = []
    avg_distance = 0
    avg_valid_distance = 0
    invalid_distance = 0
    for file in files:
        could_not_find_path = file_utils.find_lines_containing_string("Could not find path", file)
        output_lines.append(f"{file}: {len(could_not_find_path)}")
        for line in could_not_find_path:
            print(line)
            distance = extract_distance(line)
            if distance != -1:
                avg_distance = avg_distance + distance
                avg_valid_distance = avg_valid_distance + 1
            else:
                invalid_distance = invalid_distance + 1
            output_lines.append(f"{could_not_find_path};distance={distance}")
    print("=" * 40)
    print("Totals")
    avg_distance = avg_distance / avg_valid_distance
    print(f"Avg Distance = {avg_distance:.2f}")
    print(f"Avg Valid Distance = {avg_valid_distance:.2f}")
    print(f"Invalid Distances = {invalid_distance:.2f}")
    # print(';'.join(output_lines))

if __name__ == "__main__":
    main()


