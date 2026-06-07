"""
Sphere Packing for DSPmod — Maximum Density Ruin Placement
===========================================================

Problem: Place ruins on a planet surface (sphere radius R=200.2) with
minimum spacing d=52 units, maximizing the number of ruins.

This is the Tammes Problem / spherical cap packing problem.

Algorithms implemented:
  1. Poisson Disc Sampling (fast, guaranteed constraint)
  2. Fibonacci + Coulomb Energy-Gradient Optimization (denser)

Usage:
    python scripts/sphere_packing.py              # default: Poisson + Coulomb
    python scripts/sphere_packing.py --poisson    # fast Poisson disc only
    python scripts/sphere_packing.py --dense      # search for max density
    python scripts/sphere_packing.py --N 180      # specific target N

Outputs:
    scripts/ruin_positions.json  — coordinates as JSON
    Also prints C# Vector3 array for direct use in the mod.

Theoretical background:
    Sphere surface area: 4 * pi * R^2 = 503,661
    Per-point exclusion area: pi * (d/2)^2 = 2,124
    Theoretical max points: ~237
    Poisson disc achieves: ~127 (54% of theoretical)
    Coulomb achieves: ~180 (76% of theoretical)
"""

import numpy as np
import json
import sys
import time
from typing import Tuple, Optional


# =============================================================================
# Fibonacci Lattice Initialization
# =============================================================================

def fibonacci_sphere(n: int, R: float = 1.0) -> np.ndarray:
    """Generate n uniformly-distributed points on a sphere using Fibonacci lattice."""
    pts = np.empty((n, 3), dtype=np.float64)
    phi = np.pi * (3.0 - np.sqrt(5.0))  # golden angle

    for i in range(n):
        y = 1.0 - (i / max(n - 1, 1)) * 2.0
        r = np.sqrt(1.0 - y * y)
        theta = phi * i
        pts[i, 0] = np.cos(theta) * r
        pts[i, 1] = y
        pts[i, 2] = np.sin(theta) * r

    return pts * R


# =============================================================================
# Coulomb Energy-Gradient Optimization
# =============================================================================

def coulomb_optimize(
    points: np.ndarray,
    R: float,
    target_dist: float = 52.0,
    iterations: int = 5000,
    power: float = 6.0,
    lr: float = 0.5,
    momentum: float = 0.9,
    T_start: float = 0.0,
    T_end: float = 0.0,
    seed: int = 42,
    verbose: bool = True,
) -> np.ndarray:
    """
    Minimize Tammes energy E = sum (sigma/d)^p via gradient descent
    with optional simulated annealing noise.

    Works on the unit sphere internally. Uses cosine-annealed LR
    with warm restarts. When T_start > 0, adds Gaussian noise that
    decays exponentially to help escape local minima.

    Parameters:
        points: (n, 3) initial coordinates on sphere of radius R
        R: Sphere radius
        target_dist: Desired minimum pairwise distance
        iterations: Number of optimization steps
        power: Energy power (6 -> 1/d^8 force)
        lr: Peak learning rate
        momentum: Heavy-ball momentum coefficient
        T_start: Initial SA temperature (>0 to enable SA)
        T_end: Final SA temperature
        seed: Random seed for SA noise
        verbose: Print progress every 1000 iters

    Returns:
        Optimized (n, 3) coordinates on sphere of radius R
    """
    rng = np.random.RandomState(seed)
    pts = points.copy().astype(np.float64) / R
    sigma = target_dist / R
    sigma_p = sigma ** power
    velocity = np.zeros_like(pts)

    for it in range(iterations):
        # Pairwise distances
        diff = pts[np.newaxis, :, :] - pts[:, np.newaxis, :]
        dist_sq = np.sum(diff * diff, axis=2)
        np.fill_diagonal(dist_sq, np.inf)
        dist = np.sqrt(dist_sq)

        # Force magnitude: p * sigma^p / d^(p+1)
        force_mag = power * sigma_p / (dist ** (power + 1))
        np.fill_diagonal(force_mag, 0.0)

        # Direction pushing i away from j
        with np.errstate(divide="ignore", invalid="ignore"):
            dir_away = -diff / dist[:, :, np.newaxis]
            np.nan_to_num(dir_away, copy=False)

        forces = np.sum(dir_away * force_mag[:, :, np.newaxis], axis=1)

        # Project to tangent plane
        radial = np.sum(forces * pts, axis=1, keepdims=True)
        tangential = forces - radial * pts

        # Gradient clipping
        max_step = 0.03
        mag = np.sqrt(np.sum(tangential * tangential, axis=1, keepdims=True))
        scale = np.minimum(1.0, max_step / np.maximum(mag, 1e-12))
        tangential = tangential * scale

        # SA noise
        if T_start > 0 and T_end > 0:
            T = T_start * (T_end / T_start) ** (it / max(iterations - 1, 1))
            noise_3d = rng.randn(len(pts), 3).astype(np.float64)
            n_radial = np.sum(noise_3d * pts, axis=1, keepdims=True)
            n_tangent = noise_3d - n_radial * pts
            n_norm = np.sqrt(np.sum(n_tangent * n_tangent, axis=1, keepdims=True))
            n_tangent = n_tangent / np.maximum(n_norm, 1e-12) * T
            tangential = tangential + n_tangent

        # Cosine annealing LR with warm restarts
        cycle_len = iterations // 4
        cycle_pos = it % cycle_len
        lr_current = lr * 0.5 * (1.0 + np.cos(np.pi * cycle_pos / cycle_len))

        # Heavy-ball update
        velocity = momentum * velocity + lr_current * tangential
        pts = pts + velocity

        # Reproject to unit sphere
        pts = pts / np.sqrt(np.sum(pts * pts, axis=1, keepdims=True))

        if verbose and (it + 1) % 2000 == 0:
            min_d = _min_pairwise_distance_unit(pts) * R
            t_str = f", T={T:.6f}" if T_start > 0 else ""
            print(f"    iter {it + 1}/{iterations}: min={min_d:.4f}{t_str}")

    return pts * R


def multi_phase_optimize(
    points: np.ndarray,
    R: float,
    target_dist: float = 52.0,
    phases: list = None,
    seed: int = 42,
    verbose: bool = True,
) -> np.ndarray:
    """
    Multi-phase graduated optimization.

    Runs a sequence of optimization phases, typically:
      Phase 1: p=4, T=0.01→0.0001, 5000 iters (coarse, SA escape)
      Phase 2: p=6, T=0.003→0.00003, 5000 iters (refinement)
      Phase 3: p=8, T=0, 5000 iters (fine-tuning, no noise)

    Each phase starts from the previous phase's result.
    Lower power in early phases creates a smoother energy landscape
    that's easier to navigate; higher power later locks in precision.
    """
    if phases is None:
        phases = [
            {"power": 4, "T_start": 0.012, "T_end": 0.0001, "iterations": 4000, "lr": 0.6, "momentum": 0.85},
            {"power": 6, "T_start": 0.003, "T_end": 0.00003, "iterations": 4000, "lr": 0.5, "momentum": 0.9},
            {"power": 8, "T_start": 0.0, "T_end": 0.0, "iterations": 4000, "lr": 0.4, "momentum": 0.9},
        ]

    pts = points.copy()
    total_iters = sum(p["iterations"] for p in phases)

    if verbose:
        print(f"    Multi-phase optimization ({total_iters} total iters, {len(phases)} phases):")

    for pi, ph in enumerate(phases):
        if verbose:
            sa_str = f", SA={ph['T_start']:.4f}" if ph['T_start'] > 0 else ""
            print(f"    Phase {pi+1}: p={ph['power']}, {ph['iterations']} iters{sa_str}")

        pts = coulomb_optimize(
            pts, R, target_dist=target_dist,
            iterations=ph["iterations"], power=ph["power"],
            lr=ph["lr"], momentum=ph["momentum"],
            T_start=ph["T_start"], T_end=ph["T_end"],
            seed=seed + pi, verbose=verbose,
        )

    return pts


def _min_pairwise_distance_unit(pts: np.ndarray) -> float:
    """Min pairwise distance for points on unit sphere."""
    diff = pts[np.newaxis, :, :] - pts[:, np.newaxis, :]
    dist = np.sqrt(np.sum(diff * diff, axis=2))
    np.fill_diagonal(dist, np.inf)
    return float(np.min(dist))


# =============================================================================
# Poisson Disc Sampling on Sphere
# =============================================================================

def poisson_disc_sphere(
    R: float,
    d_min: float,
    K: int = 30,
    seed: int = 42,
    verbose: bool = True,
) -> np.ndarray:
    """
    Poisson disc sampling on a sphere (Bridson's algorithm, adapted).

    GUARANTEES minimum distance d_min. Fills the sphere until no more
    points can be placed. Very fast (< 1 second).

    Parameters:
        R: Sphere radius
        d_min: Minimum distance between points
        K: Candidates per active point (higher = slightly denser, slower)
        seed: Random seed
        verbose: Print progress

    Returns:
        Array of points with d >= d_min for all pairs
    """
    rng = np.random.RandomState(seed)
    cell_size = d_min
    d_min_sq = d_min * d_min

    def _key(p):
        return (int(np.floor(p[0] / cell_size)),
                int(np.floor(p[1] / cell_size)),
                int(np.floor(p[2] / cell_size)))

    def _sample_annulus(center):
        theta_min = 2.0 * np.arcsin(d_min / (2.0 * R))
        theta_max = 2.0 * np.arcsin(min(2.0 * d_min, 2.0 * R) / (2.0 * R))

        cos_max, cos_min = np.cos(theta_max), np.cos(theta_min)
        theta = np.arccos(cos_max + rng.random() * (cos_min - cos_max))
        phi = rng.random() * 2.0 * np.pi

        # Local tangent basis at center
        u = np.array([1.0, 0.0, 0.0]) if abs(center[0]) < 0.9 * R else np.array([0.0, 1.0, 0.0])
        u = u - np.dot(u, center) * center / (R * R)
        u = u / np.linalg.norm(u)
        v = np.cross(center, u)
        v = v / np.linalg.norm(v)

        axis = u * np.cos(phi) + v * np.sin(phi)
        axis = axis / np.linalg.norm(axis)

        # Rodrigues rotation
        cos_t, sin_t = np.cos(theta), np.sin(theta)
        cand = cos_t * center + sin_t * np.cross(axis, center) + \
               (1.0 - cos_t) * np.dot(axis, center) * axis
        return cand / np.linalg.norm(cand) * R

    def _is_valid(candidate, points, grid):
        ck = _key(candidate)
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    nk = (ck[0] + dx, ck[1] + dy, ck[2] + dz)
                    if nk not in grid:
                        continue
                    for idx in grid[nk]:
                        diff = candidate - points[idx]
                        if np.dot(diff, diff) < d_min_sq:
                            return False
        return True

    # Start from north pole
    p0 = np.array([0.0, 0.0, 1.0]) * R
    points = [p0]
    active = [0]
    grid = {_key(p0): [0]}

    while active:
        ai = rng.randint(len(active))
        center = points[active[ai]]
        found = False

        for _ in range(K):
            candidate = _sample_annulus(center)
            if _is_valid(candidate, points, grid):
                new_idx = len(points)
                points.append(candidate)
                active.append(new_idx)
                ck = _key(candidate)
                grid.setdefault(ck, []).append(new_idx)
                found = True
                break

        if not found:
            active.pop(ai)

    if verbose:
        print(f"    Poisson disc: {len(points)} points generated")

    return np.array(points)


# =============================================================================
# Distance Utilities
# =============================================================================

def min_pairwise_distance(points: np.ndarray) -> float:
    """Minimum Euclidean distance between any two points."""
    diff = points[np.newaxis, :, :] - points[:, np.newaxis, :]
    dist = np.sqrt(np.sum(diff * diff, axis=2))
    np.fill_diagonal(dist, np.inf)
    return float(np.min(dist))


def nearest_neighbor_stats(points: np.ndarray) -> dict:
    """Statistics of nearest-neighbor distances."""
    diff = points[np.newaxis, :, :] - points[:, np.newaxis, :]
    dist = np.sqrt(np.sum(diff * diff, axis=2))
    np.fill_diagonal(dist, np.inf)
    nn = np.min(dist, axis=1)
    return {
        "min": float(np.min(nn)),
        "max": float(np.max(nn)),
        "mean": float(np.mean(nn)),
        "std": float(np.std(nn)),
        "all": sorted(nn.tolist()),
    }


# =============================================================================
# Pruning (safety net)
# =============================================================================

def prune_close_points(points: np.ndarray, min_dist: float) -> np.ndarray:
    """
    Greedily remove points until all pairs satisfy d >= min_dist.
    Removes the point with the most violations each round.
    """
    pts = list(points.copy())
    while True:
        arr = np.array(pts)
        diff = arr[np.newaxis, :, :] - arr[:, np.newaxis, :]
        dist = np.sqrt(np.sum(diff * diff, axis=2))
        np.fill_diagonal(dist, np.inf)
        violations = np.sum(dist < min_dist, axis=1)
        max_v = int(np.max(violations))
        if max_v == 0:
            break
        pts.pop(int(np.argmax(violations)))
    return np.array(pts)


# =============================================================================
# Search for Max N (Fibonacci + Coulomb)
# =============================================================================

def find_max_dense(
    R: float,
    target_dist: float,
    n_high: int = None,
    n_low: int = None,
    n_seeds: int = 3,
    verbose: bool = True,
) -> Tuple[int, np.ndarray]:
    """
    Exhaustive binary search for the maximum N using multi-phase
    optimization with multiple random seeds.

    For each candidate N:
      1. Generate Fibonacci init + random perturbation (n_seeds variants)
      2. Run multi-phase optimization on each
      3. Keep the best result (highest min pairwise distance)
      4. Use binary search to find the exact N threshold

    This is significantly more thorough than the linear search and
    should find configurations closer to the theoretical maximum.

    Returns (N, points). Returns (0, empty) if search fails.
    """
    sphere_area = 4.0 * np.pi * R * R
    cap_area = np.pi * (target_dist / 2.0) ** 2
    theoretical_max = int(sphere_area / cap_area)

    if n_high is None:
        # Upper bound: optimizer can handle ~87% of optimal density
        n_high = min(int((0.90 * 4.0 * R / target_dist) ** 2), theoretical_max + 5)
    if n_low is None:
        n_low = max(1, int((0.72 * 4.0 * R / target_dist) ** 2))

    if verbose:
        print(f"\n  Theoretical maximum: ~{theoretical_max}")
        print(f"  Binary search range: [{n_low}, {n_high}]")
        print(f"  Seeds per candidate: {n_seeds}")
        print(f"  Optimization: 3-phase (p=4->6->8), ~12000 iters per seed")
        print()

    def try_n(n: int) -> Tuple[bool, float, np.ndarray]:
        """Try a single N value with multiple seeds. Returns (ok, best_min, best_pts)."""
        best_min = 0.0
        best_pts = None

        for seed in range(n_seeds):
            # Fibonacci init with deterministic seed-based perturbation
            pts = fibonacci_sphere(n, R)
            if seed > 0:
                # Perturb by small random rotation for diversity
                rng = np.random.RandomState(seed * 137 + n * 59)
                axis = rng.randn(3)
                axis = axis / np.linalg.norm(axis)
                angle = rng.random() * 0.3  # up to ~17 deg perturbation
                # Rodrigues rotation
                cos_a, sin_a = np.cos(angle), np.sin(angle)
                pts = cos_a * pts + sin_a * np.cross(axis, pts) + \
                      (1 - cos_a) * np.dot(pts, axis[:, np.newaxis]).ravel()[:, np.newaxis] * axis
                # Reproject
                pts = pts / np.sqrt(np.sum(pts * pts, axis=1, keepdims=True)) * R

            pts = multi_phase_optimize(
                pts, R, target_dist=target_dist,
                seed=seed * 100 + n, verbose=False,
            )
            min_d = min_pairwise_distance(pts)

            if min_d > best_min:
                best_min = min_d
                best_pts = pts.copy()

            if min_d >= target_dist:
                # Early exit: already good enough
                break

        return best_min >= target_dist, best_min, best_pts

    # Binary search for the threshold
    lo, hi = n_low, n_high
    best_n, best_pts = 0, None
    results = {}  # cache results to avoid re-computation

    while lo <= hi:
        mid = (lo + hi) // 2

        if mid in results:
            ok, min_d, pts = results[mid]
        else:
            if verbose:
                print(f"  Testing N={mid}...", end=" ", flush=True)
            t0 = time.perf_counter()
            ok, min_d, pts = try_n(mid)
            elapsed = time.perf_counter() - t0
            results[mid] = (ok, min_d, pts)
            if verbose:
                status = f"min={min_d:.4f} OK" if ok else f"min={min_d:.4f}"
                print(f"{status}  ({elapsed:.1f}s)")

        if ok:
            best_n = mid
            best_pts = pts
            lo = mid + 1
            if verbose:
                print(f"    -> new best: N={best_n}")
        else:
            hi = mid - 1

    return best_n, best_pts


# =============================================================================
# Output Formatters
# =============================================================================

def format_as_csharp_array(points: np.ndarray, indent: int = 8) -> str:
    """Format points as a C# Vector3 array literal."""
    lines = ["new Vector3[] {"]
    for i, pt in enumerate(points):
        comma = "," if i < len(points) - 1 else ""
        lines.append(
            f"{' ' * indent}new Vector3({pt[0]:.6f}f, {pt[1]:.6f}f, {pt[2]:.6f}f){comma}"
        )
    lines.append(f"{' ' * (indent - 4)}}}")
    return "\n".join(lines)


def save_points(points: np.ndarray, filepath: str) -> None:
    """Save points to JSON."""
    data = {
        "count": len(points),
        "radius": 200.2,
        "min_spacing": 52.0,
        "coordinates": [
            {"x": round(float(p[0]), 6),
             "y": round(float(p[1]), 6),
             "z": round(float(p[2]), 6)}
            for p in points
        ],
    }
    with open(filepath, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)
    print(f"\n  Saved {len(points)} points to {filepath}")


# =============================================================================
# Visualization
# =============================================================================

def plot_sphere(points: np.ndarray, R: float, title: str = "Sphere Packing") -> None:
    """3D visualization. Requires matplotlib."""
    try:
        import matplotlib.pyplot as plt
    except ImportError:
        print("\n  matplotlib not available, skipping plot")
        return

    fig = plt.figure(figsize=(10, 10))
    ax = fig.add_subplot(111, projection="3d")

    u = np.linspace(0, 2 * np.pi, 40)
    v = np.linspace(0, np.pi, 40)
    x = R * np.outer(np.cos(u), np.sin(v))
    y = R * np.outer(np.sin(u), np.sin(v))
    z = R * np.outer(np.ones_like(u), np.cos(v))
    ax.plot_wireframe(x, y, z, color="gray", alpha=0.12, linewidth=0.5)

    ax.scatter(
        points[:, 0], points[:, 1], points[:, 2],
        c="crimson", s=12, alpha=0.85, edgecolors="darkred", linewidth=0.3,
    )

    ax.set_xlabel("X"); ax.set_ylabel("Y"); ax.set_zlabel("Z")
    ax.set_title(title, fontsize=13)
    ax.set_box_aspect([1, 1, 1])
    plt.tight_layout()
    plt.show()


# =============================================================================
# Main
# =============================================================================

def main():
    import argparse

    parser = argparse.ArgumentParser(
        description="Sphere packing for DSPmod ruin placement"
    )
    parser.add_argument("--poisson", action="store_true",
                        help="Use Poisson disc sampling (fast, ~127 pts)")
    parser.add_argument("--dense", action="store_true",
                        help="Search for maximum density (slower, ~180 pts)")
    parser.add_argument("--N", type=int, default=None,
                        help="Generate exactly N points (with Coulomb optimization)")
    parser.add_argument("--R", type=float, default=200.2,
                        help="Sphere radius (default: 200.2)")
    parser.add_argument("--d", type=float, default=52.0,
                        help="Minimum spacing (default: 52.0)")
    parser.add_argument("--plot", action="store_true",
                        help="Show 3D visualization")
    parser.add_argument("--output", type=str, default="scripts/ruin_positions.json",
                        help="Output JSON path")

    args = parser.parse_args()

    R = args.R
    d_min = args.d

    print("=" * 62)
    print("  Sphere Packing — DSPmod Ruin Placement")
    print("=" * 62)
    angular_sep = 2 * np.degrees(np.arcsin(d_min / (2 * R)))
    print(f"  Radius: R={R},  Min spacing: d={d_min}")
    print(f"  Angular separation: {angular_sep:.4f} deg")
    print(f"  Theoretical max: ~{int(4*np.pi*R*R/(np.pi*(d_min/2)**2))} points")
    print("=" * 62)

    # --- Choose strategy ---
    if args.N is not None:
        # Fixed N mode with multi-phase optimization
        print(f"\n  Generating exactly N={args.N} points (multi-phase)...")
        pts = fibonacci_sphere(args.N, R)
        pts = multi_phase_optimize(pts, R, target_dist=d_min, verbose=True)
        min_d = min_pairwise_distance(pts)

        if min_d < d_min:
            print(f"\n  WARNING: min distance {min_d:.4f} < {d_min}")
            print(f"  Pruning to satisfy constraint...")
            pts = prune_close_points(pts, d_min)
            min_d = min_pairwise_distance(pts)

        print(f"\n  Result: {len(pts)} points, min distance = {min_d:.4f}")

    elif args.poisson:
        # Fast Poisson disc
        print(f"\n  Poisson disc sampling (K=30)...")
        pts = poisson_disc_sphere(R, d_min, K=30, verbose=True)
        min_d = min_pairwise_distance(pts)

        # Polish with Coulomb for uniformity
        print(f"  Coulomb polish...")
        pts = coulomb_optimize(pts, R, target_dist=d_min, iterations=2000, verbose=False)
        min_d = min_pairwise_distance(pts)

        print(f"\n  Result: {len(pts)} points, min distance = {min_d:.4f}")

    elif args.dense:
        # Search for maximum density
        print(f"\n  Exhaustive binary search (multi-phase + multi-seed)...")
        n, pts = find_max_dense(R, d_min, n_seeds=2, verbose=True)

        if n == 0:
            # Fallback to Poisson disc
            print(f"\n  Dense search failed, falling back to Poisson disc...")
            pts = poisson_disc_sphere(R, d_min, K=30, verbose=True)
            n = len(pts)

        min_d = min_pairwise_distance(pts)
        print(f"\n  Result: {n} points, min distance = {min_d:.4f}")

    else:
        # Default: Poisson disc + Coulomb polish
        print(f"\n  Default: Poisson disc + Coulomb polish...")
        pts = poisson_disc_sphere(R, d_min, K=30, verbose=True)
        pts = coulomb_optimize(pts, R, target_dist=d_min, iterations=2000, verbose=True)
        n = len(pts)
        min_d = min_pairwise_distance(pts)

        print(f"\n  Result: {n} points, min distance = {min_d:.4f}")

    # --- Stats ---
    stats = nearest_neighbor_stats(pts)
    print(f"  NN distances: min={stats['min']:.4f}, max={stats['max']:.4f}, "
          f"mean={stats['mean']:.4f}, std={stats['std']:.4f}")
    print(f"  Uniformity:   min/max = {stats['min']/stats['max']:.4f}")

    # --- Save ---
    save_points(pts, args.output)

    # --- C# format preview ---
    print(f"\n  C# Vector3 array (first 5 of {len(pts)}):")
    print(format_as_csharp_array(pts[:5]))
    print(f"  // ...")

    # --- Plot ---
    if args.plot:
        plot_sphere(pts, R, f"Sphere R={R}: {len(pts)} pts, min spacing={d_min}")


if __name__ == "__main__":
    main()
