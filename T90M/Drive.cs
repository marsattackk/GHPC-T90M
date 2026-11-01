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
			// NWH clamps the up-shift to 0.95 x max rpm. The T-72M1's 2250 sits over the new 2000 and it'd never shift up.
			ShiftUpRpm = 1900f,
			ReverseGears = new[] { -14.35f * 4.52f / 4.17f },
			MaxForwardKmh = 60f,
			// Cruise-control target, not the speed. A cap at the real 4.18 held it at 2.4, the gearing limits it instead.
			ReverseCapKmh = 12.5f,
			CombatMassKg = 51000f,
		};
	}
}
