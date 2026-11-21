using T90M.Common;

namespace T90M
{
	// V-92S2F engine and gearing.
	internal static class Drive
	{
		const float ReverseTrackFactor = 4.52f / 4.17f;   // GHPC's tracks run ~8 % over NWH's gear formula
		// Cruise-control target, not the speed. A cap at the real 4.18 held it at 2.4. The gearing limits it instead.
		const float ReverseCapKmh = 12.5f;

		internal static readonly Mobility.Settings Settings = new Mobility.Settings
		{
			Tag = T90MMod.FriendlyName,
			// The V-92S2F's 831 kW (1130 hp).
			MaxPower = 1130f,
			// 33.3 revolutions a second, 2000 rpm (medium confidence).
			MaxRpm = 2000f,
			// Solved for the T-72A manual's speeds at 2000 rpm, as for the T-90A.
			FinalDrive = 5.297f,
			// NWH clamps the up-shift to 0.95 x max rpm. The T-72M1's 2250 sits over the new 2000 and it'd never shift up.
			ShiftUpRpm = 1900f,
			// The T-72 gearbox's one reverse, ratio 14.35 (the T-72A manual), times the track factor above.
			ReverseGears = new[] { -14.35f * ReverseTrackFactor },
			// The maximum road speed, at least 60 km/h.
			MaxForwardKmh = 60f,
			ReverseCapKmh = ReverseCapKmh,
			// 51 t with ammunition and crew, up to 2 % more (the T-90M's own table).
			CombatMassKg = 51000f,
		};
	}
}
