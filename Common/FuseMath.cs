using System;

namespace T90M.Common
{
	internal static class FuseMath
	{
		internal static bool BurstDistance(
			bool impact,
			bool shortOfTarget,
			float range,
			float shortOf,
			float minRange,
			float muzzleAhead,
			out float distance)
		{
			distance = 0f;
			if (impact || range < minRange) return false;
			float burstRange = shortOfTarget ? range - shortOf : range;
			distance = burstRange - muzzleAhead;
			return true;
		}

		internal static float TimeToDistance(float distance, float step, float maxTime, Func<float> advance)
		{
			if (distance <= 0f) return 0f;
			float previous = 0f;
			for (float elapsed = 0f; elapsed < maxTime; elapsed += step)
			{
				float next = advance();
				if (next >= distance)
				{
					float part = (distance - previous) / Math.Max(1e-5f, next - previous);
					return elapsed + step * (part < 0f ? 0f : part > 1f ? 1f : part);
				}
				previous = next;
			}
			return -1f;
		}

		internal static float BurstTime(float setTime, float scatter) => Math.Max(0.02f, setTime + scatter);

		internal static bool Due(float traveled, float dt, float burstAt, out float tau)
		{
			tau = 0f;
			if (traveled + dt < burstAt) return false;
			tau = Math.Max(0f, burstAt - traveled);
			return true;
		}

		internal static float Gaussian(float first, float second)
			=> (float)(Math.Sqrt(-2.0 * Math.Log(first)) * Math.Cos(2.0 * Math.PI * second));

		internal static float ElementPen(float pen, float penSpeed, float speed) => pen * (speed / penSpeed) * (speed / penSpeed);

		internal static float OverHeight(bool overSet, float fuseTime, float range, float minRange, float over)
			=> overSet && fuseTime >= 0f && range >= minRange ? over : 0f;
	}
}
