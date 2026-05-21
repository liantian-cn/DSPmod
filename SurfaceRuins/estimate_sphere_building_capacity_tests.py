import math
import unittest

import estimate_sphere_building_capacity as estimator


class SphereBuildingCapacityTests(unittest.TestCase):
    def test_default_distances_match_requested_values(self):
        self.assertEqual(estimator.DEFAULT_DISTANCES, (52.5, 53.0, 53.5, 54.5, 55.0))

    def test_area_upper_bound_for_distance_55_radius_200(self):
        self.assertEqual(estimator.area_upper_bound(200.0, 55.0), 210)

    def test_triangular_lattice_estimate_uses_two_triangles_per_point(self):
        self.assertEqual(estimator.equilateral_triangle_area(55.0), 1309.863423)
        self.assertEqual(estimator.triangular_lattice_center_count(200.0, 55.0), 192)

    def test_search_counts_start_near_triangular_lattice_estimate(self):
        counts = list(estimator.search_counts(center=10, lower_bound=7, upper_bound=13))
        self.assertEqual(counts, [10, 9, 8, 7, 11, 12, 13])

    def test_emit_points_defaults_to_output_csv(self):
        args = estimator.build_parser().parse_args(["--emit-points", "10.6"])
        self.assertEqual(args.output, "output.csv")

    def test_emit_points_accepts_custom_output_path(self):
        args = estimator.build_parser().parse_args(
            ["--emit-points", "10.6", "--output", "wind.csv"]
        )
        self.assertEqual(args.output, "wind.csv")

    def test_spacing_validation_detects_close_points(self):
        points = (
            estimator.Point(0.0, 0.0, 200.0),
            estimator.Point(0.0, 9.0, 199.797397),
        )

        self.assertFalse(estimator.is_spacing_valid(points, 10.0))
        self.assertTrue(estimator.is_spacing_valid(points, 8.0))

    def test_spacing_validation_matches_minimum_distance_result(self):
        points = estimator.fibonacci_points(count=64, radius=200.0, epsilon=0.5, phase=0.0)
        min_distance = estimator.minimum_distance(points)

        self.assertTrue(estimator.is_spacing_valid(points, min_distance - 1e-6))
        self.assertFalse(estimator.is_spacing_valid(points, min_distance + 1e-6))

    def test_generated_points_respect_requested_chord_distance(self):
        result = estimator.estimate_for_distance(
            radius=200.0,
            distance=55.0,
            starts=2,
            iterations=80,
            seed=1234,
        )
        self.assertGreater(result.count, 0)
        self.assertGreaterEqual(result.actual_min_distance + 1e-6, 55.0)
        for point in result.points:
            radius = math.sqrt(point.x * point.x + point.y * point.y + point.z * point.z)
            self.assertAlmostEqual(radius, 200.0, places=6)


if __name__ == "__main__":
    unittest.main()
