using System;
using T90M.Common;
using Xunit;

namespace T90M.Tests
{
	// The target tracker's laying loop against a target crossing at a known speed and range, worked out by hand: the line's
	// yaw is atan(speed t / range), so its rate is speed / range rad/s. Yaw in degrees, errors in mrad.
	public class TrackServoTests
	{
		const double Dt = 1.0 / 60.0;

		static void Near(double expected, double actual, string what, double tolerance) =>
			Assert.True(Math.Abs(expected - actual) <= tolerance, $"{what}: {actual} (expected {expected} +- {tolerance})");

		static double Mrad(double degrees) => degrees * Math.PI / 180.0 * 1000.0;

		static double Degrees(double mrad) => mrad * 0.001 * 180.0 / Math.PI;

		// A target crossing at `speed` m/s at `range` m. The aim moves by (1 - inertia) of each step: 0 is an ideal platform,
		// 0.5 a sight platform that follows with inertia. With noiseHz the seen line jumps by up to noiseMrad that many times
		// a second, like a sampled image. Returns the true line less the aim at the end (mrad), the last step's rate (deg/s)
		// and the largest error over the last 40 % of the run.
		static (double err, double rate, double maxErr) Cross(TrackServo servo, float speed, float range, float inertia, float seconds,
			int noiseHz = 0, float noiseMrad = 0f)
		{
			double aim = 0.0;
			double t = 0.0;
			double prevSeen = 0.0;
			double err = 0.0;
			double rate = 0.0;
			double maxErr = 0.0;
			double noise = 0.0;
			var random = new Random(7);
			int frames = (int)Math.Round(seconds / Dt);
			int every = noiseHz > 0 ? Math.Max(1, (int)Math.Round(1.0 / (noiseHz * Dt))) : 0;
			for (int i = 0; i < frames; i++)
			{
				t += Dt;
				double want = Math.Atan(speed * t / range) * 180.0 / Math.PI;
				if (every > 0 && i % every == 0) noise = Degrees((random.NextDouble() * 2.0 - 1.0) * noiseMrad);
				double seen = want + noise;
				servo.Step((float)(seen - aim), 0f, (float)(seen - prevSeen), 0f, (float)Dt, out float stepYaw, out _);
				prevSeen = seen;
				aim += (1.0 - inertia) * stepYaw;
				err = Mrad(want - aim);
				rate = stepYaw / Dt;
				if (t > seconds * 0.6) maxErr = Math.Max(maxErr, Math.Abs(err));
			}
			return (err, rate, maxErr);
		}

		[Fact]
		public void A_crossing_target_at_3_km_is_held_without_lag_at_its_true_rate()
		{
			var servo = new TrackServo { SlewRate = 5f, PullIn = 0.1f };
			var (err, rate, _) = Cross(servo, 10f, 3000f, 0f, 3f);
			Near(0, err, "lag (mrad)", 0.01);
			// 10 m/s at 3 km: 1/300 rad/s. At t = 3 s the line's rate is only 0.01 % under that.
			double trueRate = 10.0 / 3000.0 * 180.0 / Math.PI;
			Near(trueRate, rate, "rate (deg/s)", trueRate * 0.02);
		}

		[Fact]
		public void The_integral_holds_a_platform_that_follows_with_inertia()
		{
			var servo = new TrackServo { SlewRate = 5f, PullIn = 0.1f };
			var (err, _, _) = Cross(servo, 10f, 1000f, 0.5f, 4f);
			Near(0, err, "lag with inertia 0.5 (mrad)", 0.05);
			var proportional = new TrackServo { SlewRate = 5f, PullIn = 0.1f, Integral = false };
			var (errP, _, _) = Cross(proportional, 10f, 1000f, 0.5f, 4f);
			Assert.True(Math.Abs(errP) > 0.3, $"without the integral a steady lag is expected, got {errP:0.000} mrad");
		}

		[Fact]
		public void The_pull_onto_the_target_settles_in_half_a_second_without_overshoot()
		{
			var servo = new TrackServo { SlewRate = 5f, PullIn = 0.1f };
			double aim = Degrees(-0.5);
			double t = 0.0;
			double after = 0.0;
			for (int i = 0; i < 60; i++)
			{
				t += Dt;
				servo.Step((float)(0.0 - aim), 0f, 0f, 0f, (float)Dt, out float stepYaw, out _);
				aim += stepYaw;
				double left = Mrad(-aim);   // still to go; negative is past the target
				Assert.True(left >= -0.02, $"overshoot of {left:0.000} mrad at {t:0.00} s");
				if (t > 0.5) after = Math.Max(after, Math.Abs(left));
			}
			Near(0, after, "error after 0.5 s (mrad)", 0.01);
		}

		[Fact]
		public void A_step_never_exceeds_the_slew_rate()
		{
			var servo = new TrackServo { SlewRate = 5f, PullIn = 0.1f };
			servo.Step(20f, -20f, 0f, 0f, (float)Dt, out float stepYaw, out float stepPitch);
			Near(5.0 / 60.0, stepYaw, "yaw step at the slew limit", 1e-6);
			Near(-5.0 / 60.0, stepPitch, "pitch step at the slew limit", 1e-6);
		}

		[Fact]
		public void One_frame_of_30_ms_lays_like_two_of_15()
		{
			var one = new TrackServo { Integral = false };
			var two = new TrackServo { Integral = false };
			one.Step(1f, 0f, 0f, 0f, 0.03f, out float whole, out _);
			two.Step(1f, 0f, 0f, 0f, 0.015f, out float first, out _);
			two.Step(1f - first, 0f, 0f, 0f, 0.015f, out float second, out _);
			Near(whole, first + second, "two half frames against one frame", 1e-5);
		}

		[Fact]
		public void The_targets_move_this_frame_is_not_counted_twice()
		{
			// The aim is on the line and the line moved 0.01 deg this frame: the step is exactly that 0.01 deg.
			var servo = new TrackServo { Integral = false };
			servo.Step(0.01f, 0f, 0.01f, 0f, (float)Dt, out float step, out _);
			Near(0.01, step, "step for one frame's move with nothing left over", 1e-7);
		}

		[Fact]
		public void A_sampled_line_with_scatter_is_followed_without_amplifying_it()
		{
			var servo = new TrackServo { SlewRate = 5f, PullIn = 0.1f };
			var (_, _, maxErr) = Cross(servo, 10f, 3000f, 0f, 4f, 10, 0.1f);
			Assert.True(maxErr < 0.25, $"error {maxErr:0.000} mrad against 0.1 mrad of scatter sampled at 10 Hz");
		}

		[Fact]
		public void Wrap_brings_an_angle_into_plus_minus_180()
		{
			Near(-170, TrackServo.Wrap(190f), "190", 1e-4);
			Near(170, TrackServo.Wrap(-190f), "-190", 1e-4);
			Near(10, TrackServo.Wrap(370f), "370", 1e-4);
			Near(0, TrackServo.Wrap(0f), "0", 1e-4);
		}
	}
}
