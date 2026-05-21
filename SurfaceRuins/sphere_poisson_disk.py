#!/usr/bin/env python3
"""Fast Poisson disk sampling on a sphere using game-style chord distance.

The sampler places points on a sphere and validates spacing with ordinary 3D
Euclidean distance. That matches Dyson Sphere Program's local-position spacing
checks better than surface arc length.
"""

from __future__ import annotations

import argparse
import csv
import io
import math
import random
import sys
from collections import defaultdict
from dataclasses import dataclass
from typing import Iterable, Sequence


DEFAULT_RADIUS = 200.2
DEFAULT_ATTEMPTS = 30
DEFAULT_SEED = 20260521


@dataclass(frozen=True)
class Point:
    x: float
    y: float
    z: float


def squared_distance(a: Point, b: Point) -> float:
    dx = a.x - b.x
    dy = a.y - b.y
    dz = a.z - b.z
    return dx * dx + dy * dy + dz * dz


def minimum_distance(points: Sequence[Point]) -> float:
    if len(points) < 2:
        return math.inf

    best = math.inf
    for index, point in enumerate(points):
        for other in points[:index]:
            best = min(best, squared_distance(point, other))
    return math.sqrt(best)


def chord_to_angle(radius: float, chord_distance: float) -> float:
    if radius <= 0:
        raise ValueError("radius must be positive")
    if chord_distance <= 0:
        raise ValueError("min_distance must be positive")
    if chord_distance > radius * 2.0:
        raise ValueError("min_distance cannot exceed the sphere diameter")
    return 2.0 * math.asin(chord_distance / (radius * 2.0))


def normalize_to_radius(x: float, y: float, z: float, radius: float) -> Point:
    length = math.sqrt(x * x + y * y + z * z)
    if length <= 1e-12:
        return Point(0.0, 0.0, radius)

    scale = radius / length
    return Point(x * scale, y * scale, z * scale)


def random_point_on_sphere(rng: random.Random, radius: float) -> Point:
    z_unit = rng.uniform(-1.0, 1.0)
    ring_radius = math.sqrt(max(0.0, 1.0 - z_unit * z_unit))
    angle = rng.uniform(0.0, math.tau)
    return Point(
        radius * ring_radius * math.cos(angle),
        radius * ring_radius * math.sin(angle),
        radius * z_unit,
    )


def tangent_basis(center: Point) -> tuple[Point, Point, Point]:
    normal = normalize_to_radius(center.x, center.y, center.z, 1.0)
    if abs(normal.z) < 0.9:
        ax, ay, az = 0.0, 0.0, 1.0
    else:
        ax, ay, az = 1.0, 0.0, 0.0

    ux = ay * normal.z - az * normal.y
    uy = az * normal.x - ax * normal.z
    uz = ax * normal.y - ay * normal.x
    u = normalize_to_radius(ux, uy, uz, 1.0)

    vx = normal.y * u.z - normal.z * u.y
    vy = normal.z * u.x - normal.x * u.z
    vz = normal.x * u.y - normal.y * u.x
    v = normalize_to_radius(vx, vy, vz, 1.0)
    return normal, u, v


def random_annulus_point(
    rng: random.Random,
    center: Point,
    radius: float,
    min_angle: float,
    max_angle: float,
) -> Point:
    normal, u, v = tangent_basis(center)
    cos_alpha = rng.uniform(math.cos(max_angle), math.cos(min_angle))
    sin_alpha = math.sqrt(max(0.0, 1.0 - cos_alpha * cos_alpha))
    beta = rng.uniform(0.0, math.tau)

    direction_x = (
        normal.x * cos_alpha
        + (u.x * math.cos(beta) + v.x * math.sin(beta)) * sin_alpha
    )
    direction_y = (
        normal.y * cos_alpha
        + (u.y * math.cos(beta) + v.y * math.sin(beta)) * sin_alpha
    )
    direction_z = (
        normal.z * cos_alpha
        + (u.z * math.cos(beta) + v.z * math.sin(beta)) * sin_alpha
    )
    return normalize_to_radius(direction_x, direction_y, direction_z, radius)


class SpatialHash:
    def __init__(self, min_distance: float) -> None:
        self.min_distance = min_distance
        self.min_distance_squared = min_distance * min_distance
        self.cell_size = min_distance / math.sqrt(3.0)
        self.neighbor_range = math.ceil(min_distance / self.cell_size)
        self.cells: dict[tuple[int, int, int], list[Point]] = defaultdict(list)

    def cell_key(self, point: Point) -> tuple[int, int, int]:
        return (
            math.floor(point.x / self.cell_size),
            math.floor(point.y / self.cell_size),
            math.floor(point.z / self.cell_size),
        )

    def add(self, point: Point) -> None:
        self.cells[self.cell_key(point)].append(point)

    def can_place(self, point: Point) -> bool:
        cx, cy, cz = self.cell_key(point)
        span = self.neighbor_range
        for x in range(cx - span, cx + span + 1):
            for y in range(cy - span, cy + span + 1):
                for z in range(cz - span, cz + span + 1):
                    for other in self.cells.get((x, y, z), ()):
                        if squared_distance(point, other) < self.min_distance_squared:
                            return False
        return True


def sample_sphere_poisson_disk(
    min_distance: float,
    radius: float = DEFAULT_RADIUS,
    attempts: int = DEFAULT_ATTEMPTS,
    seed: int = DEFAULT_SEED,
    max_points: int | None = None,
) -> list[Point]:
    """Return a saturated Bridson-style Poisson disk sample on a sphere."""

    if attempts <= 0:
        raise ValueError("attempts must be positive")

    min_angle = chord_to_angle(radius, min_distance)
    max_angle = min(math.pi, min_angle * 2.0)
    rng = random.Random(seed)
    points = [random_point_on_sphere(rng, radius)]
    active = [points[0]]
    grid = SpatialHash(min_distance)
    grid.add(points[0])

    while active:
        if max_points is not None and len(points) >= max_points:
            break

        active_index = rng.randrange(len(active))
        center = active[active_index]
        accepted = False

        for _ in range(attempts):
            candidate = random_annulus_point(rng, center, radius, min_angle, max_angle)
            if not grid.can_place(candidate):
                continue

            points.append(candidate)
            active.append(candidate)
            grid.add(candidate)
            accepted = True
            break

        if not accepted:
            active[active_index] = active[-1]
            active.pop()

    return points


def format_points_csv(points: Iterable[Point]) -> str:
    output = io.StringIO()
    writer = csv.writer(output, lineterminator="\n")
    writer.writerow(("index", "x", "y", "z"))
    for index, point in enumerate(points, start=1):
        writer.writerow((index, f"{point.x:.9f}", f"{point.y:.9f}", f"{point.z:.9f}"))
    return output.getvalue()


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Sample sphere points with Bridson-style Poisson disk spacing."
    )
    parser.add_argument("min_distance", type=float, help="Minimum 3D chord distance.")
    parser.add_argument("--radius", type=float, default=DEFAULT_RADIUS, help="Sphere radius.")
    parser.add_argument(
        "--attempts",
        type=int,
        default=DEFAULT_ATTEMPTS,
        help="Candidate attempts before retiring an active point.",
    )
    parser.add_argument("--seed", type=int, default=DEFAULT_SEED)
    parser.add_argument("--max-points", type=int, help="Stop after this many accepted points.")
    parser.add_argument("--emit-points", action="store_true", help="Print CSV point coordinates.")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    points = sample_sphere_poisson_disk(
        min_distance=args.min_distance,
        radius=args.radius,
        attempts=args.attempts,
        seed=args.seed,
        max_points=args.max_points,
    )

    if args.emit_points:
        sys.stdout.write(format_points_csv(points))
    else:
        print(f"radius: {args.radius:g}")
        print(f"min_distance: {args.min_distance:g}")
        print(f"attempts: {args.attempts}")
        print(f"seed: {args.seed}")
        print(f"count: {len(points)}")
        print(f"actual_min_distance: {minimum_distance(points):.6f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
