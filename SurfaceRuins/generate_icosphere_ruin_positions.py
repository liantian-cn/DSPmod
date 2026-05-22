#!/usr/bin/env python3
"""Generate symmetric icosphere ruin positions for SurfaceRuins.cs."""

from __future__ import annotations

import argparse
import math
import re
from pathlib import Path


DEFAULT_RADIUS = 200.2
DEFAULT_FREQUENCY = 4


def normalize(point: tuple[float, float, float]) -> tuple[float, float, float]:
    x, y, z = point
    length = math.sqrt(x * x + y * y + z * z)
    return (x / length, y / length, z / length)


def scaled(point: tuple[float, float, float], radius: float) -> tuple[float, float, float]:
    x, y, z = normalize(point)
    return (x * radius, y * radius, z * radius)


def base_icosahedron() -> tuple[list[tuple[float, float, float]], list[tuple[int, int, int]]]:
    phi = (1.0 + math.sqrt(5.0)) / 2.0
    vertices = [
        (-1.0, phi, 0.0),
        (1.0, phi, 0.0),
        (-1.0, -phi, 0.0),
        (1.0, -phi, 0.0),
        (0.0, -1.0, phi),
        (0.0, 1.0, phi),
        (0.0, -1.0, -phi),
        (0.0, 1.0, -phi),
        (phi, 0.0, -1.0),
        (phi, 0.0, 1.0),
        (-phi, 0.0, -1.0),
        (-phi, 0.0, 1.0),
    ]
    faces = [
        (0, 11, 5),
        (0, 5, 1),
        (0, 1, 7),
        (0, 7, 10),
        (0, 10, 11),
        (1, 5, 9),
        (5, 11, 4),
        (11, 10, 2),
        (10, 7, 6),
        (7, 1, 8),
        (3, 9, 4),
        (3, 4, 2),
        (3, 2, 6),
        (3, 6, 8),
        (3, 8, 9),
        (4, 9, 5),
        (2, 4, 11),
        (6, 2, 10),
        (8, 6, 7),
        (9, 8, 1),
    ]
    return vertices, faces


def generate_points(frequency: int, radius: float) -> list[tuple[float, float, float]]:
    if frequency < 1:
        raise ValueError("frequency must be at least 1")

    vertices, faces = base_icosahedron()
    points: dict[tuple[float, float, float], tuple[float, float, float]] = {}

    for a_index, b_index, c_index in faces:
        ax, ay, az = vertices[a_index]
        bx, by, bz = vertices[b_index]
        cx, cy, cz = vertices[c_index]
        for i in range(frequency + 1):
            for j in range(frequency + 1 - i):
                k = frequency - i - j
                point = (
                    (ax * i + bx * j + cx * k) / frequency,
                    (ay * i + by * j + cy * k) / frequency,
                    (az * i + bz * j + cz * k) / frequency,
                )
                unit = normalize(point)
                key = tuple(round(value, 12) for value in unit)
                points[key] = scaled(point, radius)

    return sorted(points.values(), key=lambda p: (-p[2], math.atan2(p[1], p[0]), p[0]))


def format_csharp_points(points: list[tuple[float, float, float]]) -> str:
    lines = []
    for index, (x, y, z) in enumerate(points):
        suffix = "," if index < len(points) - 1 else ""
        lines.append(f"            new Vector3({x:.6f}f, {y:.6f}f, {z:.6f}f){suffix}")
    return "\n".join(lines)


def replace_ruin_positions(source: str, points_text: str) -> str:
    pattern = re.compile(
        r"(private static readonly Vector3\[\] RuinPositions =\s*\{\n)"
        r".*?"
        r"(\n        \};)",
        re.DOTALL,
    )
    replacement = rf"\1{points_text}\2"
    updated, count = pattern.subn(replacement, source, count=1)
    if count != 1:
        raise ValueError("Could not find RuinPositions array in SurfaceRuins.cs")
    return updated


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--radius", type=float, default=DEFAULT_RADIUS)
    parser.add_argument("--frequency", type=int, default=DEFAULT_FREQUENCY)
    parser.add_argument("--write", type=Path, help="Replace RuinPositions in the given C# file.")
    args = parser.parse_args()

    points = generate_points(args.frequency, args.radius)
    points_text = format_csharp_points(points)

    if args.write:
        source_bytes = args.write.read_bytes()
        newline = "\r\n" if b"\r\n" in source_bytes else "\n"
        source = source_bytes.decode("utf-8")
        updated = replace_ruin_positions(source, points_text)
        args.write.write_text(updated.replace("\n", newline), encoding="utf-8", newline="")
    else:
        print(points_text)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
