using System;
using T90M.Common;
using Xunit;

namespace T90M.Tests
{
	// The target tracker's point filter, loss clock and inertial memory. Each expected value is worked out by hand in the
	// comment above it. Metres and seconds. The target runs along x, 1000 m ahead along z.
	public class TrackFilterTests
	{
		static void Near(double expected, double actual, string what, double tolerance) =>
			Assert.True(Math.Abs(expected - actual) <= tolerance, $"{what}: {actual} (expected {expected} +- {tolerance})");

		static V3 At(float x) => new V3(x, 0f, 1000f);

		// A target at path(t), sampled every 0.1 s with noise(t) added to what the sample sees, the point coasting at 60 frames
		// a second in between, as the tracker runs it. Returns the filter and the largest |point - target| over the last third.
		static (TrackFilter filter, double maxErr) Follow(Func<double, double> path, Func<double, double> noise, double seconds)
		{
			var filter = new TrackFilter();
			filter.Capture(At((float)path(0.0)), 0f);
			const double dt = 1.0 / 60.0;
			int frames = (int)Math.Round(seconds / dt);
			double nextSample = 0.1;
			double maxErr = 0.0;
			for (int i = 1; i <= frames; i++)
			{
				double t = i * dt;
				filter.Coast((float)dt);
				if (t >= nextSample - 1e-9)
				{
					nextSample += 0.1;
					filter.Correct(At((float)(path(t) + noise(t))), (float)t);
				}
				if (i > frames * 2 / 3) maxErr = Math.Max(maxErr, Math.Abs(filter.Point.x - path(t)));
			}
			return (filter, maxErr);
		}

		[Fact]
		public void A_target_at_a_steady_speed_is_followed_without_lag_and_its_speed_learnt()
		{
			// An alpha-beta track has no steady error on a constant speed.
			var (filter, maxErr) = Follow(t => 10.0 * t, t => 0.0, 15.0);
			Near(0, maxErr, "error (m)", 0.05);
			Near(10.0, filter.Speed.x, "speed (m/s)", 0.1);
			Assert.True(filter.Measured);
		}

		[Fact]
		public void A_tank_rocking_at_2_Hz_barely_moves_the_point()
		{
			// ±0.3 m at 2 Hz: Alpha 0.15 per 0.1 s sample is a time constant of about 0.6 s, far slower than the rock.
			var (_, maxErr) = Follow(t => 0.0, t => 0.3 * Math.Sin(2.0 * Math.PI * 2.0 * t), 15.0);
			Near(0, maxErr, "rock left (m)", 0.1);
		}

		[Fact]
		public void A_target_speeding_up_is_followed_about_two_metres_behind()
		{
			// The lag on an acceleration is a·T²/Beta = 2 x 0.1² / 0.01 = 2 m.
			var (filter, _) = Follow(t => t * t, t => 0.0, 20.0);
			Near(2.0, 400.0 - filter.Point.x, "lag (m)", 0.5);
		}

		[Fact]
		public void One_missed_sample_is_not_a_loss_but_LossConfirm_of_them_is()
		{
			var filter = new TrackFilter { LossConfirm = 0.3f };
			filter.Capture(At(0f), 0f);
			Assert.False(filter.Missed(1.0f));
			Assert.False(filter.Missed(1.2f));
			Assert.True(filter.Missed(1.31f));
			// A sample that sees the target stops the clock and the next miss starts a new one.
			filter.Correct(At(0f), 1.4f);
			Assert.False(filter.Missed(1.5f));
			Assert.False(filter.Missed(1.7f));
			Assert.True(filter.Missed(1.81f));
		}

		[Fact]
		public void Inertial_predicts_from_the_frozen_point_holds_and_gives_up_after_InertialMax()
		{
			var filter = new TrackFilter { InertialMax = 6f };
			filter.Capture(At(100f), 0f);
			filter.Freeze(new V3(10f, 0f, -2f), 10f, hold: 0.5f);
			// 2 s on: 100 + 2 x 10 = 120 across, 1000 - 2 x 2 = 996 ahead.
			var predicted = filter.Predicted(12f);
			Near(120, predicted.x, "predicted x", 1e-4);
			Near(996, predicted.z, "predicted z", 1e-4);
			Assert.False(filter.Searching(10.4f));
			Assert.True(filter.Searching(10.5f));
			Assert.False(filter.Expired(16f));
			Assert.True(filter.Expired(16.01f));
		}

		[Fact]
		public void The_search_window_is_SearchWidth_half_sizes_and_never_under_2_mrad_of_half_size()
		{
			var filter = new TrackFilter { SearchWidth = 3f };
			filter.Capture(At(0f), 0f);
			filter.Freeze(new V3(0f, 0f, 0f), 0f);
			var eye = new V3(0f, 0f, 0f);
			// Half-size 2.5 mrad: window 7.5 mrad, 7.5 m at 1000 m.
			Assert.True(filter.InWindow(eye, At(7.4f), 0.0025f, 1f));
			Assert.False(filter.InWindow(eye, At(7.6f), 0.0025f, 1f));
			// Half-size 0.5 mrad counts as 2: window 6 mrad.
			Near(0.006, filter.Window(0.0005f), "smallest window (rad)", 1e-7);
			Assert.True(filter.InWindow(eye, new V3(0f, 5.9f, 1000f), 0.0005f, 1f));
			Assert.False(filter.InWindow(eye, new V3(0f, 6.1f, 1000f), 0.0005f, 1f));
		}

		[Fact]
		public void A_recapture_restarts_where_the_target_was_seen_on_the_frozen_speed()
		{
			var filter = new TrackFilter();
			filter.Capture(At(0f), 0f);
			filter.Freeze(new V3(8f, 0f, 0f), 1f);
			filter.Recapture(At(30f), 4f);
			Near(30, filter.Point.x, "point", 1e-6);
			Near(8, filter.Speed.x, "seeded speed", 1e-6);
			Assert.False(filter.Measured);
			filter.Coast(0.5f);
			Near(34, filter.Point.x, "after 0.5 s on 8 m/s", 1e-5);
			// Right after a (re-)capture the gain is AlphaStart: a sample 1 m ahead moves the point 0.6 m.
			filter.Correct(At(35f), 4f);
			Near(34.6, filter.Point.x, "AlphaStart step", 1e-4);
		}

		[Fact]
		public void The_gains_ease_from_their_capture_values_over_Settle()
		{
			// Halfway through Settle (0.75 of 1.5 s) alpha is (0.6 + 0.15) / 2 = 0.375, and after it 0.15.
			var early = new TrackFilter();
			early.Capture(At(0f), 0f);
			early.Correct(At(1f), 0.75f);
			Near(0.375, early.Point.x, "half-eased alpha", 1e-5);
			var late = new TrackFilter();
			late.Capture(At(0f), 0f);
			late.Correct(At(1f), 5f);
			Near(0.15, late.Point.x, "settled alpha", 1e-6);
			// Beta is per sample: 0.01 x 1 m / 0.1 s = 0.1 m/s.
			Near(0.1, late.Speed.x, "settled beta (m/s)", 1e-6);
		}
	}
}
