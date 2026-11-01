using T90M.Common;

namespace T90M
{
	internal static class Drive
	{
		internal static readonly Mobility.Settings Settings = new Mobility.Settings
		{
			MaxPower = 1130f,
			MaxRpm = 2000f,
			FinalDrive = 5.297f,
			ShiftUpRpm = 1900f,
			ReverseGears = new[] { -14.35f * 4.52f / 4.17f },
			MaxForwardKmh = 60f,
			ReverseCapKmh = 12.5f,
			CombatMassKg = 51000f,
		};
	}
}
