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

    def test_progress_message_includes_current_count_and_best_result(self):
        event = estimator.ProgressEvent(
            distance=10.6,
            tested_counts=12,
            count=5150,
            center=5167,
            lower_bound=2841,
            upper_bound=5689,
            local_best_distance=10.41,
            best_count=5120,
            best_distance=10.61,
            success=False,
        )

        self.assertEqual(
            estimator.format_progress(event),
            (
                "distance=10.6 tested=12 count=5150 range=2841..5689 "
                "center=5167 local_min=10.4100 best=5120@10.6100"
            ),
        )

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
