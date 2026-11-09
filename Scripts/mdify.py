#!/usr/bin/env python3
"""
mdify.py — convert summarise.py plain-text output to Markdown for Notion.

Usage:
    python3 summarise.py alllogs.jsonl --view model-ranking | python3 mdify.py
    python3 summarise.py alllogs.jsonl > all.txt && python3 mdify.py all.txt > all.md

Options:
    --wide N      tables with more than N columns become fenced code blocks
                  instead of Markdown tables (default 8; Notion tables get
                  cramped past ~8 columns)
    --separator   emit '---' between top-level sections
"""

import argparse
import re
import sys

DIVIDER_RE      = re.compile(r'^=+$')
DASH_RE         = re.compile(r'^-+$')
TABLE_DIV_RE    = re.compile(r'^  [- ]+$')


def is_section_divider(line):
    s = line.strip()
    return len(s) >= 20 and DIVIDER_RE.match(s) is not None


def is_bare_dash(line):
    s = line.strip()
    return len(s) >= 5 and DASH_RE.match(s) is not None


def is_table_divider(line):
    if not TABLE_DIV_RE.match(line):
        return False
    return line.count("  ") >= 2 and line.strip().startswith("-")


def widths_from_divider(line):
    return [len(c) for c in line.strip().split("  ")]


def slice_row(line, widths):
    body = line[2:] if line.startswith("  ") else line
    total = sum(widths) + 2 * (len(widths) - 1)
    body = body.ljust(total)
    cells, pos = [], 0
    for w in widths:
        cells.append(body[pos:pos + w].strip())
        pos += w + 2
    return cells


def escape_cell(s):
    return s.replace("|", r"\|")


def emit_md_table(headers, rows):
    out = ["| " + " | ".join(escape_cell(h) for h in headers) + " |",
           "| " + " | ".join("---" for _ in headers) + " |"]
    for r in rows:
        out.append("| " + " | ".join(escape_cell(c) for c in r) + " |")
    return out


def convert(text, wide_limit=8, emit_separator=False):
    lines = text.splitlines()
    out, i, n = [], 0, len(lines)

    while i < n:
        line = lines[i]
        stripped = line.strip()

        if not stripped:
            if out and out[-1] != "":
                out.append("")
            i += 1
            continue

        # --- section divider: === / TITLE / ===
        if is_section_divider(line):
            if (i + 2 < n
                    and not is_section_divider(lines[i + 1])
                    and is_section_divider(lines[i + 2])):
                title = lines[i + 1].strip()
                out.extend(["", f"## {title}", ""])
                i += 3
                continue
            if emit_separator:
                out.extend(["", "---", ""])
            i += 1
            continue

        # --- table: header line followed by a dash-group divider
        if i + 1 < n and is_table_divider(lines[i + 1]):
            widths  = widths_from_divider(lines[i + 1])
            headers = slice_row(line, widths)
            rows, j = [], i + 2
            while (j < n and lines[j].strip()
                   and not is_section_divider(lines[j])
                   and not is_table_divider(lines[j])):
                cells = slice_row(lines[j], widths)
                if any(cells):
                    rows.append(cells)
                j += 1
            if len(headers) > wide_limit:
                out.append("```text")
                out.extend(lines[i:j])
                out.append("```")
            else:
                out.extend(emit_md_table(headers, rows))
            out.append("")
            i = j
            continue

        # --- bare dash line: promote previous line to a heading
        if is_bare_dash(line):
            k = len(out) - 1
            while k >= 0 and out[k] == "":
                k -= 1
            if k >= 0 and not out[k].startswith(("#", "|", "```")):
                out[k] = f"### {out[k].strip()}"
            i += 1
            continue

        # --- 4+ space indented block: fence if it looks like an ASCII board
        if line.startswith("    "):
            j, block = i, []
            while j < n and lines[j].startswith("    "):
                block.append(lines[j])
                j += 1
            if len(block) >= 5:
                if out and out[-1] != "":
                    out.append("")
                out.append("```text")
                out.extend(block)
                out.append("```")
                out.append("")
            else:
                out.extend(b.strip() for b in block)
                out.append("")
            i = j
            continue

        # --- 2-space indented prose: strip indent
        if line.startswith("  "):
            out.append(stripped)
            i += 1
            continue

        out.append(line)
        i += 1

    cleaned = []
    for l in out:
        if l == "" and cleaned and cleaned[-1] == "":
            continue
        cleaned.append(l)
    return "\n".join(cleaned).strip() + "\n"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input", nargs="?", default="-")
    ap.add_argument("--wide", type=int, default=8)
    ap.add_argument("--separator", action="store_true")
    args = ap.parse_args()

    text = sys.stdin.read() if args.input == "-" else open(args.input, encoding="utf-8").read()
    sys.stdout.write(convert(text, wide_limit=args.wide,
                             emit_separator=args.separator))


if __name__ == "__main__":
    main()