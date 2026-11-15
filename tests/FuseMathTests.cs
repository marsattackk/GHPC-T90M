using System;
using T90M.Common;
using Xunit;

namespace T90M.Tests
{
	// The 3ОФ82 air-burst fuse's arithmetic. Each expected value is worked out by hand in the comment above it. Metres and
	// seconds.
	public class FuseMathTests
	{
		static void Near(double expected, double actual, string what, double tolerance) =>
			Assert.True(Math.Abs(expected - actual) <= tolerance, $"{what}: {actual} (expected {expected} +- {tolerance})");

		[Fact]
		public void The_burst_point_for_each_setting_and_impact_under_the_valid_range()
		{
			// Short: 1000 m lased, 10 m short, the muzzle 6 m ahead of the laser: 1000 - 10 - 6 = 984 m from the muzzle.
			Assert.True(FuseMath.BurstDistance(false, true, 1000f, 10f, 300f, 6f, out float shortOf));
			Near(984, shortOf, "short of the target", 1e-4);
			// Over: at the range, 1000 - 6 = 994 m.
			Assert.True(FuseMath.BurstDistance(false, false, 1000f, 10f, 300f, 6f, out float over));
			Near(994, over, "over the target", 1e-4);
			Assert.False(FuseMath.BurstDistance(true, false, 1000f, 10f, 300f, 6f, out _));
			Assert.False(FuseMath.BurstDistance(false, true, 299f, 10f, 300f, 6f, out _));
			Assert.True(FuseMath.BurstDistance(false, true, 300f, 10f, 300f, 6f, out _));
		}

		[Fact]
		public void The_flight_time_is_interpolated_inside_the_step()
		{
			// A round at a steady 1000 m/s in 2 ms steps: 1000 m takes 1 s, 0.5 m takes 0.5 ms (inside the first step).
			float Steady(float distance, float maxTime)
			{
				float travelled = 0f;
				return FuseMath.TimeToDistance(distance, 0.002f, maxTime, () => travelled += 2f);
			}
			Near(1.0, Steady(1000f, 20f), "1000 m", 1e-3);
			Near(0.0005, Steady(0.5f, 20f), "0.5 m", 1e-7);
			Near(0, Steady(0f, 20f), "at the muzzle", 0);
			Near(0, Steady(-4f, 20f), "behind the muzzle", 0);
			Near(-1, Steady(2000f, 1f), "out of reach in 1 s", 0);
			// A round slowing down, steps of 2, 1.9, 1.8 m: 3.9 m after two steps, 5.7 after three, so 5 m is reached at
			// 0.004 + 0.002 x (5 - 3.9) / 1.8 = 0.0052222 s.
			float step = 2.1f;
			float run = 0f;
			Near(0.0052222, FuseMath.TimeToDistance(5f, 0.002f, 20f, () => run += step -= 0.1f), "slowing round", 1e-6);
		}

		[Fact]
		public void The_burst_time_is_the_set_time_plus_scatter_never_before_the_first_frame()
		{
			Near(1.25, FuseMath.BurstTime(1.2f, 0.05f), "set plus scatter", 1e-6);
			Near(0.02, FuseMath.BurstTime(0.01f, -0.5f), "floored", 1e-9);
		}

		[Fact]
		public void The_burst_falls_in_the_frame_that_reaches_its_time()
		{
			// A frame from 1.00 to 1.02 s: a burst at 1.01 is 0.01 s in; at 1.03 it is next frame's; one already passed is now.
			Assert.True(FuseMath.Due(1.0f, 0.02f, 1.01f, out float tau));
			Near(0.01, tau, "tau", 1e-6);
			Assert.False(FuseMath.Due(1.0f, 0.02f, 1.03f, out _));
			Assert.True(FuseMath.Due(1.0f, 0.02f, 0.99f, out float late));
			Near(0, late, "no step back", 0);
		}

		[Fact]
		public void The_scatter_sample_is_a_standard_normal()
		{
			// Box-Muller: sqrt(-2 ln 1) = 0; sqrt(-2 ln e^-0.5) x cos 0 = 1.
			Near(0, FuseMath.Gaussian(1f, 0.3f), "first sample 1", 1e-6);
			Near(1, FuseMath.Gaussian((float)Math.Exp(-0.5), 0f), "first sample e^-0.5", 1e-6);
			var random = new Random(3);
			double sum = 0.0;
			double squares = 0.0;
			const int count = 200000;
			for (int i = 0; i < count; i++)
			{
				double sample = FuseMath.Gaussian(1f - (float)random.NextDouble(), (float)random.NextDouble());
				sum += sample;
				squares += sample * sample;
			}
			double mean = sum / count;
			Near(0, mean, "mean", 0.01);
			Near(1, Math.Sqrt(squares / count - mean * mean), "standard deviation", 0.01);
		}

		[Fact]
		public void Element_penetration_and_the_over_the_target_height()
		{
			// 10 mm at 1000 m/s, twice the speed: 4 x 10 = 40 mm.
			Near(40, FuseMath.ElementPen(10f, 1000f, 2000f), "pen at 2000 m/s", 1e-4);
			Near(3, FuseMath.OverHeight(true, 1.1f, 1000f, 300f, 3f), "over, timed, valid range", 0);
			Near(0, FuseMath.OverHeight(false, 1.1f, 1000f, 300f, 3f), "short of the target", 0);
			Near(0, FuseMath.OverHeight(true, -1f, 1000f, 300f, 3f), "over but on impact", 0);
			Near(0, FuseMath.OverHeight(true, 1.1f, 299f, 300f, 3f), "range now invalid", 0);
		}
	}
}
