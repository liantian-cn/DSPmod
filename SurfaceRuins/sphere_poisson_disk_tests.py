import math
import unittest

import sphere_poisson_disk as sampler


class SpherePoissonDiskTests(unittest.TestCase):
    def test_sample_points_respect_chord_distance_and_radius(self):
        points = sampler.sample_sphere_poisson_disk(
            min_distance=55.0,
            radius=200.2,
            attempts=12,
            seed=1234,
        )

        self.assertGreater(len(points), 20)
        self.assertGreaterEqual(sampler.minimum_distance(points), 55.0 - 1e-6)
        for point in points:
            radius = math.sqrt(point.x * point.x + point.y * point.y + point.z * point.z)
            self.assertAlmostEqual(radius, 200.2, places=6)

    def test_emit_points_uses_csv_header(self):
        points = (sampler.Point(1.0, 2.0, 3.0),)

        self.assertEqual(
            sampler.format_points_csv(points),
            "index,x,y,z\n1,1.000000000,2.000000000,3.000000000\n",
        )


if __name__ == "__main__":
    unittest.main()
