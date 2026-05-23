#!/usr/bin/env python3
"""Generate symmetric icosphere ruin positions for SurfaceRuins.cs."""

from __future__ import annotations

import argparse
import csv
import math
import re
from dataclasses import dataclass
from pathlib import Path


DEFAULT_RADIUS = 200.2
DEFAULT_FREQUENCY = 4
DEFAULT_CSV_PATH = Path(__file__).with_suffix(".csv")
LOW_LATITUDE_MAX = 28.5
MID_LATITUDE_MAX = 46.5


@dataclass(frozen=True)
class RuinPoint:
    index: int
    x: float
    y: float
    z: float
    latitude_deg: float
    abs_latitude_deg: float
    band: str

    def as_tuple(self) -> tuple[float, float, float]:
        return (self.x, self.y, self.z)


def normalize(point: tuple[float, float, float]) -> tuple[float, float, float]:
    x, y, z = point
    length = math.sqrt(x * x + y * y + z * z)
    return (x / length, y / length, z / length)


def scaled(point: tuple[float, float, float], radius: float) -> tuple[float, float, float]:
    x, y, z = normalize(point)
    return (x * radius, y * radius, z * radius)


def latitude_degrees(point: tuple[float, float, float]) -> float:
    x, y, z = point
    length = math.sqrt(x * x + y * y + z * z)
    if length <= 0.0:
        return 0.0
    ratio = max(-1.0, min(1.0, z / length))
    return math.degrees(math.asin(ratio))


def band_for_latitude(abs_latitude_deg: float) -> str:
    if abs_latitude_deg < LOW_LATITUDE_MAX:
        return "low"
    if abs_latitude_deg < MID_LATITUDE_MAX:
        return "mid"
    return "high"


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


def generate_points(frequency: int, radius: float) -> list[RuinPoint]:
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

    ordered_points = sorted(points.values(), key=lambda p: (-p[2], math.atan2(p[1], p[0]), p[0]))
    records: list[RuinPoint] = []
    for index, (x, y, z) in enumerate(ordered_points):
        latitude = latitude_degrees((x, y, z))
        records.append(
            RuinPoint(
                index=index,
                x=x,
                y=y,
                z=z,
                latitude_deg=latitude,
                abs_latitude_deg=abs(latitude),
                band=band_for_latitude(abs(latitude)),
            )
        )

    return records


def format_csharp_points(points: list[RuinPoint]) -> str:
    lines = []
    for index, point in enumerate(points):
        suffix = "," if index < len(points) - 1 else ""
        lines.append(f"            new Vector3({point.x:.6f}f, {point.y:.6f}f, {point.z:.6f}f){suffix}")
    return "\n".join(lines)


def grouped_points(points: list[RuinPoint]) -> dict[str, list[RuinPoint]]:
    groups = {"low": [], "mid": [], "high": []}
    for point in points:
        groups[point.band].append(point)
    return groups


def format_csharp_array(name: str, points: list[RuinPoint]) -> str:
    return "\n".join(
        [
            f"        private static readonly Vector3[] {name} =",
            "        {",
            format_csharp_points(points),
            "        };",
        ]
    )


def format_csharp_grouped_arrays(points: list[RuinPoint]) -> str:
    groups = grouped_points(points)
    return "\n\n".join(
        [
            format_csharp_array("LowLatitudeRuinPositions", groups["low"]),
            format_csharp_array("MidLatitudeRuinPositions", groups["mid"]),
            format_csharp_array("HighLatitudeRuinPositions", groups["high"]),
        ]
    )


def replace_ruin_positions(source: str, points_text: str) -> str:
    grouped_pattern = re.compile(
        r"        private static readonly Vector3\[\] LowLatitudeRuinPositions =\s*\{\n"
        r".*?"
        r"\n        \};\n\n"
        r"        private static readonly Vector3\[\] MidLatitudeRuinPositions =\s*\{\n"
        r".*?"
        r"\n        \};\n\n"
        r"        private static readonly Vector3\[\] HighLatitudeRuinPositions =\s*\{\n"
        r".*?"
        r"\n        \};",
        re.DOTALL,
    )
    updated, count = grouped_pattern.subn(points_text, source, count=1)
    if count == 1:
        return updated

    single_pattern = re.compile(
        r"        private static readonly Vector3\[\] RuinPositions =\s*\{\n"
        r".*?"
        r"\n        \};",
        re.DOTALL,
    )
    updated, count = single_pattern.subn(points_text, source, count=1)
    if count == 1:
        return updated

    raise ValueError("Could not find ruin position arrays in SurfaceRuins.cs")


def write_csv(csv_path: Path, points: list[RuinPoint]) -> None:
    csv_path.parent.mkdir(parents=True, exist_ok=True)
    with csv_path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["index", "x", "y", "z", "latitude_deg", "abs_latitude_deg", "band"])
        for point in points:
            writer.writerow(
                [
                    point.index,
                    f"{point.x:.6f}",
                    f"{point.y:.6f}",
                    f"{point.z:.6f}",
                    f"{point.latitude_deg:.6f}",
                    f"{point.abs_latitude_deg:.6f}",
                    point.band,
                ]
            )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--radius", type=float, default=DEFAULT_RADIUS)
    parser.add_argument("--frequency", type=int, default=DEFAULT_FREQUENCY)
    parser.add_argument("--write", type=Path, help="Replace RuinPositions in the given C# file.")
    parser.add_argument("--csv", type=Path, default=DEFAULT_CSV_PATH, help="Write a CSV sidecar with latitude bands.")
    args = parser.parse_args()

    points = generate_points(args.frequency, args.radius)
    points_text = format_csharp_points(points)

    if args.write:
        source_bytes = args.write.read_bytes()
        newline = "\r\n" if b"\r\n" in source_bytes else "\n"
        source = source_bytes.decode("utf-8")
        updated = replace_ruin_positions(source, format_csharp_grouped_arrays(points))
        args.write.write_text(updated.replace("\n", newline), encoding="utf-8", newline="")
    else:
        print(points_text)

    if args.csv:
        write_csv(args.csv, points)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
