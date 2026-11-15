using System;

namespace T90M.Common
{
	internal sealed class TrackFilter
	{
		internal float Alpha = 0.15f, Beta = 0.01f, AlphaStart = 0.6f, BetaStart = 0.3f, Settle = 1.5f;
		internal float SampleInterval = 0.1f;
		internal float LossConfirm = 0.3f;
		internal float InertialMax = 6f;
		internal float SearchWidth = 3f;

		internal V3 Point;
		internal V3 Speed;
		internal bool Measured;
		internal V3 FrozenPoint, FrozenSpeed;
		internal float FrozenAt, HoldUntil;
		internal float CapturedAt { get; private set; }

		float _lostSince = -1f;

		internal float LostFor(float now) => _lostSince < 0f ? 0f : now - _lostSince;
		internal float InertialFor(float now) => now - FrozenAt;

		internal void Capture(V3 point, float now)
		{
			Point = point;
			Speed = new V3(0f, 0f, 0f);
			Measured = false;
			CapturedAt = now;
			_lostSince = -1f;
		}

		internal void Recapture(V3 point, float now)
		{
			Point = point;
			Speed = FrozenSpeed;
			Measured = false;
			CapturedAt = now;
			_lostSince = -1f;
		}

		internal void Correct(V3 centre, float now)
		{
			float k = Settle > 0f ? Clamp01((now - CapturedAt) / Settle) : 1f;
			var toCentre = centre - Point;
			Point = Point + toCentre * Lerp(AlphaStart, Alpha, k);
			Speed = Speed + toCentre * (Lerp(BetaStart, Beta, k) / Math.Max(SampleInterval, 0.02f));
			Measured = true;
			_lostSince = -1f;
		}

		internal bool Missed(float now)
		{
			if (_lostSince < 0f)
			{
				_lostSince = now;
				return false;
			}
			return now - _lostSince >= LossConfirm;
		}

		internal void Coast(float dt) => Point = Point + Speed * dt;

		internal void Freeze(V3 speed, float now, float hold = 0f)
		{
			FrozenPoint = Point;
			FrozenSpeed = speed;
			FrozenAt = now;
			HoldUntil = now + hold;
			_lostSince = -1f;
		}

		internal V3 Predicted(float now) => FrozenPoint + FrozenSpeed * (now - FrozenAt);
		internal bool Searching(float now) => now >= HoldUntil;
		internal bool Expired(float now) => now - FrozenAt > InertialMax;

		internal float Window(float halfMax) => SearchWidth * Math.Max(halfMax, 0.002f);

		internal static float Angle(V3 eye, V3 a, V3 b)
		{
			V3 u = a - eye, v = b - eye;
			double lengths = (double)u.Length * v.Length;
			if (lengths < 1e-15) return 0f;
			double cos = V3.Dot(u, v) / lengths;
			return (float)Math.Acos(cos < -1.0 ? -1.0 : cos > 1.0 ? 1.0 : cos);
		}

		internal bool InWindow(V3 eye, V3 centre, float halfMax, float now)
			=> Angle(eye, centre, Predicted(now)) <= Window(halfMax);

		static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
		static float Lerp(float a, float b, float t) => a + (b - a) * t;
	}
}
