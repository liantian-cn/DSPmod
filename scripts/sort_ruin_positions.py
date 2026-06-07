#!/usr/bin/env python3
"""
Sort ruin positions using the "greedy minimum cumulative distance" algorithm:

1. Keep the first point as-is.
2. For each remaining point, select the one that minimizes the sum of distances
   to all previously selected points.
3. Write the reordered coordinates back to both ruin_positions.json and
   RuinPositions.cs.
"""

import json
import math
import os
import sys

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
JSON_PATH = os.path.join(SCRIPT_DIR, "ruin_positions.json")
CS_PATH = os.path.join(SCRIPT_DIR, "..", "HardFog", "RuinPositions.cs")


def load_coords(path: str) -> list[dict]:
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)
    return data["coordinates"]


def save_json(path: str, coords: list[dict], meta: dict) -> None:
    output = {
        "count": meta["count"],
        "radius": meta["radius"],
        "min_spacing": meta["min_spacing"],
        "coordinates": coords,
    }
    with open(path, "w", encoding="utf-8") as f:
        json.dump(output, f, indent=2, ensure_ascii=False)
        f.write("\n")


def vec3_dist(a: dict, b: dict) -> float:
    dx = a["x"] - b["x"]
    dy = a["y"] - b["y"]
    dz = a["z"] - b["z"]
    return math.sqrt(dx * dx + dy * dy + dz * dz)


def sort_coords(coords: list[dict]) -> list[dict]:
    """Greedy sort: each next point minimizes sum of distances to all prior points."""
    remaining = list(coords)
    result = [remaining.pop(0)]  # keep first point

    while remaining:
        # Precompute distances from each remaining point to all selected points
        best_idx = 0
        best_sum = float("inf")
        for i, cand in enumerate(remaining):
            total = sum(vec3_dist(cand, sel) for sel in result)
            if total < best_sum:
                best_sum = total
                best_idx = i
        result.append(remaining.pop(best_idx))

    return result


def format_cs_line(x: float, y: float, z: float) -> str:
    return f"new Vector3({x}f, {y}f, {z}f)"


def generate_cs_all_section(coords: list[dict]) -> str:
    lines = []
    lines.append("        public static readonly Vector3[] All =")
    lines.append("        {")
    for i, c in enumerate(coords):
        comma = "," if i < len(coords) - 1 else ""
        lines.append(f"            {format_cs_line(c['x'], c['y'], c['z'])}{comma}")
    lines.append("        };")
    return "\n".join(lines)


def update_cs_file(path: str, coords: list[dict]) -> None:
    with open(path, "r", encoding="utf-8") as f:
        content = f.read()

    # Find the All array bounds
    start_marker = "public static readonly Vector3[] All ="
    end_marker = "        };"

    start_idx = content.index(start_marker)
    # Rewind to the beginning of the line so we replace the indentation too.
    line_start = content.rfind("\n", 0, start_idx) + 1
    end_idx = content.index(end_marker, start_idx) + len(end_marker)

    new_section = generate_cs_all_section(coords)
    new_content = content[:line_start] + new_section + content[end_idx:]

    with open(path, "w", encoding="utf-8") as f:
        f.write(new_content)


def main():
    # Load JSON metadata and coordinates
    with open(JSON_PATH, "r", encoding="utf-8") as f:
        data = json.load(f)
    meta = {
        "count": data["count"],
        "radius": data["radius"],
        "min_spacing": data["min_spacing"],
    }
    coords = data["coordinates"]
    print(f"Loaded {len(coords)} coordinates from {JSON_PATH}")

    # Sort
    sorted_coords = sort_coords(coords)
    print(f"Sorted {len(sorted_coords)} coordinates")

    # Write JSON
    save_json(JSON_PATH, sorted_coords, meta)
    print(f"Wrote sorted coordinates to {JSON_PATH}")

    # Write C#
    update_cs_file(CS_PATH, sorted_coords)
    print(f"Wrote sorted coordinates to {CS_PATH}")

    # Print first/last few to verify
    print("\nFirst 5:")
    for c in sorted_coords[:5]:
        print(f"  ({c['x']:+.4f}, {c['y']:+.4f}, {c['z']:+.4f})")
    print("Last 5:")
    for c in sorted_coords[-5:]:
        print(f"  ({c['x']:+.4f}, {c['y']:+.4f}, {c['z']:+.4f})")


if __name__ == "__main__":
    main()
