#!/usr/bin/env python3
"""Estimate how many spaced points fit on a spherical planet surface.

The game-side spacing rule uses local 3D chord distance between positions, not
surface arc length. This script therefore keeps all generated points on a
sphere of radius 200 by default and checks ordinary Euclidean distance.

The result is an estimate: finding the exact maximum packing on a sphere is a
hard optimization problem. The script reports both a constructive count that it
found, a simple area upper bound, and a triangular-lattice area estimate.
"""

from __future__ import annotations

import argparse
import csv
import math
import random
from collections import defaultdict
from dataclasses import dataclass
from typing import Iterable, Sequence

from tqdm import tqdm


DEFAULT_RADIUS = 200.0
DEFAULT_DISTANCES = (52.5, 53.0, 53.5, 54.5, 55.0)
GOLDEN_ANGLE = math.pi * (3.0 - math.sqrt(5.0))


@dataclass(frozen=True)
class Point:
    x: float
    y: float
    z: float


@dataclass(frozen=True)
class EstimateResult:
    distance: float
    angular_distance_degrees: float
    area_upper_bound: int
    triangular_lattice_center: int
    count: int
    actual_min_distance: float
    points: tuple[Point, ...]


def chord_to_angle(radius: float, chord_distance: float) -> float:
    """Convert chord distance to central angle in radians."""

    if radius <= 0:
        raise ValueError("radius must be positive")
    if chord_distance <= 0:
        raise ValueError("distance must be positive")
    if chord_distance > 2.0 * radius:
        raise ValueError("distance cannot exceed the sphere diameter")
    return 2.0 * math.asin(chord_distance / (2.0 * radius))


def area_upper_bound(radius: float, chord_distance: float) -> int:
    """Return the spherical-cap area upper bound for this spacing."""

    alpha = chord_to_angle(radius, chord_distance)
    cap_area = 2.0 * math.pi * radius * radius * (1.0 - math.cos(alpha / 2.0))
    sphere_area = 4.0 * math.pi * radius * radius
    return math.floor(sphere_area / cap_area)


def sphere_area(radius: float) -> float:
    if radius <= 0:
        raise ValueError("radius must be positive")
    return 4.0 * math.pi * radius * radius


def equilateral_triangle_area(side_length: float) -> float:
    if side_length <= 0:
        raise ValueError("side_length must be positive")
    return round(math.sqrt(3.0) * side_length * side_length / 4.0, 6)


def triangular_lattice_center_count(radius: float, chord_distance: float) -> int:
    """Estimate count from planar triangular packing area per point.

    A triangular lattice has two equilateral triangles around each point on
    average, so the per-point area is sqrt(3) / 2 * distance^2.
    """

    per_point_area = equilateral_triangle_area(chord_distance) * 2.0
    return max(1, round(sphere_area(radius) / per_point_area))


def search_counts(center: int, lower_bound: int, upper_bound: int) -> Iterable[int]:
    if lower_bound <= 0:
        raise ValueError("lower_bound must be positive")
    if lower_bound > upper_bound:
        raise ValueError("lower_bound cannot exceed upper_bound")

    center = min(max(center, lower_bound), upper_bound)
    for count in range(center, lower_bound - 1, -1):
        yield count
    for count in range(center + 1, upper_bound + 1):
        yield count


def fibonacci_points(count: int, radius: float, epsilon: float, phase: float) -> list[Point]:
    """Generate a nearly even deterministic point set on a sphere."""

    if count <= 0:
        return []
    if count == 1:
        return [Point(0.0, 0.0, radius)]

    points: list[Point] = []
    denominator = count - 1.0 + 2.0 * epsilon
    for index in range(count):
        z_unit = 1.0 - 2.0 * (index + epsilon) / denominator
        ring_radius = math.sqrt(max(0.0, 1.0 - z_unit * z_unit))
        angle = index * GOLDEN_ANGLE + phase
        points.append(
            Point(
                radius * ring_radius * math.cos(angle),
                radius * ring_radius * math.sin(angle),
                radius * z_unit,
            )
        )
    return points


def random_unit_point(rng: random.Random, radius: float) -> Point:
    z_unit = rng.uniform(-1.0, 1.0)
    ring_radius = math.sqrt(max(0.0, 1.0 - z_unit * z_unit))
    angle = rng.uniform(0.0, 2.0 * math.pi)
    return Point(
        radius * ring_radius * math.cos(angle),
        radius * ring_radius * math.sin(angle),
        radius * z_unit,
    )


def squared_distance(a: Point, b: Point) -> float:
    dx = a.x - b.x
    dy = a.y - b.y
    dz = a.z - b.z
    return dx * dx + dy * dy + dz * dz


class SpatialHash:
    def __init__(self, min_distance: float) -> None:
        if min_distance <= 0:
            raise ValueError("min_distance must be positive")
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


def is_spacing_valid(points: Sequence[Point], min_distance: float) -> bool:
    grid = SpatialHash(min_distance)
    for point in points:
        if not grid.can_place(point):
            return False
        grid.add(point)
    return True


def minimum_distance(points: Sequence[Point], stop_below: float | None = None) -> float:
    if len(points) < 2:
        return math.inf

    best = math.inf
    stop_below_squared = None if stop_below is None else stop_below * stop_below
    for index, point in enumerate(points):
        for other in points[:index]:
            distance_squared = squared_distance(point, other)
            if stop_below_squared is not None and distance_squared < stop_below_squared:
                return math.sqrt(distance_squared)
            if distance_squared < best:
                best = distance_squared
    return math.sqrt(best)


def normalize_to_radius(x: float, y: float, z: float, radius: float) -> Point:
    length = math.sqrt(x * x + y * y + z * z)
    if length <= 1e-12:
        return Point(0.0, 0.0, radius)
    scale = radius / length
    return Point(x * scale, y * scale, z * scale)


def relax_points(
    points: Sequence[Point],
    radius: float,
    target_distance: float,
    iterations: int,
) -> list[Point]:
    """Push close pairs apart and keep points projected to the sphere."""

    relaxed = list(points)
    if len(relaxed) < 2 or iterations <= 0:
        return relaxed

    target_squared = target_distance * target_distance
    for iteration in range(iterations):
        forces = [[0.0, 0.0, 0.0] for _ in relaxed]
        close_pairs = 0

        for i, a in enumerate(relaxed):
            for j in range(i):
                b = relaxed[j]
                dx = a.x - b.x
                dy = a.y - b.y
                dz = a.z - b.z
                distance_squared = dx * dx + dy * dy + dz * dz
                if distance_squared >= target_squared or distance_squared <= 1e-12:
                    continue

                distance = math.sqrt(distance_squared)
                push = (target_distance - distance) / target_distance
                nx = dx / distance
                ny = dy / distance
                nz = dz / distance
                forces[i][0] += nx * push
                forces[i][1] += ny * push
                forces[i][2] += nz * push
                forces[j][0] -= nx * push
                forces[j][1] -= ny * push
                forces[j][2] -= nz * push
                close_pairs += 1

        if close_pairs == 0:
            break

        step = 0.35 * (1.0 - iteration / max(1, iterations))
        next_points: list[Point] = []
        for point, force in zip(relaxed, forces):
            next_points.append(
                normalize_to_radius(
                    point.x + force[0] * step,
                    point.y + force[1] * step,
                    point.z + force[2] * step,
                    radius,
                )
            )
        relaxed = next_points

    return relaxed


def candidate_sets(count: int, radius: float, starts: int, seed: int) -> Iterable[list[Point]]:
    epsilons = (0.5, 1.0, 2.0, 3.33, 5.0, 8.0)
    phase_count = max(1, starts)

    for epsilon in epsilons:
        for phase_index in range(phase_count):
            phase = 2.0 * math.pi * phase_index / phase_count
            yield fibonacci_points(count, radius, epsilon, phase)

    rng = random.Random(seed + count * 1009)
    for _ in range(max(0, starts // 2)):
        yield [random_unit_point(rng, radius) for _ in range(count)]


def find_constructive_set(
    radius: float,
    distance: float,
    starts: int,
    iterations: int,
    seed: int,
    show_progress: bool = False,
) -> tuple[int, float, tuple[Point, ...]]:
    upper_bound = area_upper_bound(radius, distance)
    center = triangular_lattice_center_count(radius, distance)
    lower_bound = max(1, math.floor(center * 0.55))
    best_count = 0
    best_distance = 0.0
    best_points: tuple[Point, ...] = ()
    counts = list(search_counts(center, lower_bound, upper_bound))
    count_iterable = tqdm(
        counts,
        desc=f"d={distance:g}",
        unit="count",
        dynamic_ncols=True,
        disable=not show_progress,
    )

    for count in count_iterable:
        local_best_distance = 0.0
        local_best_points: tuple[Point, ...] = ()
        closest_candidate: list[Point] | None = None

        for candidate in candidate_sets(count, radius, starts, seed):
            if is_spacing_valid(candidate, distance):
                before_distance = minimum_distance(candidate)
                count_iterable.set_postfix_str(f"found={count} min={before_distance:.4f}")
                return count, before_distance, tuple(candidate)

            before_distance = minimum_distance(candidate, stop_below=distance * 0.985)
            if before_distance < distance * 0.985:
                continue
            before_distance = minimum_distance(candidate)

            if before_distance > local_best_distance:
                local_best_distance = before_distance
                local_best_points = tuple(candidate)
                closest_candidate = candidate

        if (
            closest_candidate is not None
            and iterations > 0
            and local_best_distance >= distance * 0.985
        ):
            relaxed = relax_points(
                closest_candidate,
                radius,
                distance,
                max(1, iterations // 5),
            )
            after_distance = minimum_distance(relaxed)
            if after_distance >= distance:
                count_iterable.set_postfix_str(f"found={count} min={after_distance:.4f}")
                return count, after_distance, tuple(relaxed)
            if after_distance > local_best_distance:
                local_best_distance = after_distance
                local_best_points = tuple(relaxed)

        if local_best_distance > best_distance:
            best_count = count
            best_distance = local_best_distance
            best_points = local_best_points
            count_iterable.set_postfix_str(f"best={best_count} min={best_distance:.4f}")

    return best_count, best_distance, best_points


def estimate_for_distance(
    radius: float,
    distance: float,
    starts: int = 6,
    iterations: int = 250,
    seed: int = 20260521,
    show_progress: bool = False,
) -> EstimateResult:
    count, actual_min_distance, points = find_constructive_set(
        radius=radius,
        distance=distance,
        starts=starts,
        iterations=iterations,
        seed=seed,
        show_progress=show_progress,
    )
    angle = chord_to_angle(radius, distance)
    return EstimateResult(
        distance=distance,
        angular_distance_degrees=math.degrees(angle),
        area_upper_bound=area_upper_bound(radius, distance),
        triangular_lattice_center=triangular_lattice_center_count(radius, distance),
        count=count,
        actual_min_distance=actual_min_distance,
        points=points,
    )


def parse_distances(values: Sequence[str] | None) -> tuple[float, ...]:
    if not values:
        return DEFAULT_DISTANCES

    distances: list[float] = []
    for value in values:
        for part in value.split(","):
            part = part.strip()
            if part:
                distances.append(float(part))
    return tuple(distances)


def format_distance(value: float) -> str:
    return f"{value:.3f}".rstrip("0").rstrip(".")


def print_table(results: Sequence[EstimateResult]) -> None:
    rows = [
        (
            "distance",
            "angle_deg",
            "area_upper",
            "triangular_center",
            "found_count",
            "actual_min_distance",
        )
    ]
    for result in results:
        rows.append(
            (
                format_distance(result.distance),
                f"{result.angular_distance_degrees:.4f}",
                str(result.area_upper_bound),
                str(result.triangular_lattice_center),
                str(result.count),
                f"{result.actual_min_distance:.4f}",
            )
        )

    widths = [max(len(row[column]) for row in rows) for column in range(len(rows[0]))]
    for row in rows:
        print("  ".join(value.rjust(widths[index]) for index, value in enumerate(row)))


def write_points_csv(result: EstimateResult, output) -> None:
    writer = csv.writer(output)
    writer.writerow(("index", "x", "y", "z"))
    for index, point in enumerate(result.points, start=1):
        writer.writerow((index, f"{point.x:.9f}", f"{point.y:.9f}", f"{point.z:.9f}"))


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Estimate how many spaced points fit on a sphere with chord-distance spacing."
    )
    parser.add_argument("--radius", type=float, default=DEFAULT_RADIUS)
    parser.add_argument("--distances", nargs="*", help="Distances, comma-separated or space-separated.")
    parser.add_argument("--starts", type=int, default=6, help="Initial candidate variants per distance.")
    parser.add_argument("--iterations", type=int, default=250, help="Repulsion iterations per candidate.")
    parser.add_argument("--seed", type=int, default=20260521)
    parser.add_argument(
        "--emit-points",
        type=float,
        metavar="DISTANCE",
        help="Write CSV coordinates for one distance instead of printing the summary table.",
    )
    parser.add_argument("--output", default="output.csv", help="CSV path used with --emit-points.")
    parser.add_argument("--no-progress", action="store_true", help="Disable tqdm for --emit-points.")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)

    distances = parse_distances(args.distances)
    if args.emit_points is not None:
        result = estimate_for_distance(
            radius=args.radius,
            distance=args.emit_points,
            starts=args.starts,
            iterations=args.iterations,
            seed=args.seed,
            show_progress=not args.no_progress,
        )
        with open(args.output, "w", newline="", encoding="utf-8") as output:
            write_points_csv(result, output)
        return 0

    results = [
        estimate_for_distance(
            radius=args.radius,
            distance=distance,
            starts=args.starts,
            iterations=args.iterations,
            seed=args.seed,
        )
        for distance in distances
    ]
    print_table(results)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
