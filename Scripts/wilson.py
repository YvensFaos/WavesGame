#!/usr/bin/env python3
"""
Wilson confidence interval calculator for a proportion.
Usage: python wilson.py <n_tests> <n_wins>
Example: python wilson.py 10 8
"""

import sys
import math


def wilson_interval(n, wins, confidence=0.95):
    """Return (lower, upper) Wilson confidence interval for a proportion."""
    if n <= 0:
        raise ValueError("Number of tests must be > 0")
    if wins < 0 or wins > n:
        raise ValueError("Wins must be between 0 and n")

    # z-value for 95% confidence (two-tailed)
    z = 1.96

    p_hat = wins / n
    # Wilson score interval formula
    denominator = 1 + (z ** 2 / n)
    centre = (p_hat + (z ** 2) / (2 * n)) / denominator
    half_width = (z * math.sqrt((p_hat * (1 - p_hat) + (z ** 2) / (4 * n)) / n)) / denominator

    lower = centre - half_width
    upper = centre + half_width

    # Clip to [0, 1] for extreme cases (shouldn't be needed but safe)
    lower = max(0.0, lower)
    upper = min(1.0, upper)

    return lower, upper


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Error: Please provide exactly two arguments: n_tests n_wins")
        print(f"Usage: {sys.argv[0]} <n_tests> <n_wins>")
        sys.exit(1)

    try:
        n = int(sys.argv[1])
        wins = int(sys.argv[2])
    except ValueError:
        print("Error: Arguments must be integers")
        sys.exit(1)

    try:
        lower, upper = wilson_interval(n, wins)
    except ValueError as e:
        print(f"Error: {e}")
        sys.exit(1)

    # Print as percentages with one decimal place
    print(f"Wilson 95% confidence interval for {wins}/{n} ({wins / n * 100:.1f}%):")
    print(f"Lower bound: {lower * 100:.1f}%")
    print(f"Upper bound: {upper * 100:.1f}%")