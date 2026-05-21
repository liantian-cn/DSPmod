import math
import unittest

import estimate_sphere_ruin_capacity as estimator


class SphereRuinCapacityTests(unittest.TestCase):
    def test_default_distances_match_requested_values(self):
        self.assertEqual(estimator.DEFAULT_DISTANCES, (52.5, 53.0, 53.5, 54.5, 55.0))

    def test_area_upper_bound_for_distance_55_radius_200(self):
        self.assertEqual(estimator.area_upper_bound(200.0, 55.0), 210)

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
