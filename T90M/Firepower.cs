using System.Collections.Generic;
using GHPC.Effects;
using GHPC.Vehicle;
using MelonLoader;
using T90M.Common;
using UnityEngine;

namespace T90M
{
	// Rounds, how many go where (carousel, hull rack, bustle box), and the Telnik's fuse.
	internal static class Firepower
	{
		internal static readonly PortAmmo.Mix Counts = new PortAmmo.Mix   // mine. No source gives a regulation mix
		{
			Apfsds = 20,
			Heat = 8,
			He = 4,
			Missile = 4,
			Extra = new[] { 4 },
		};
		internal static readonly PortAmmo.Mix CarouselCountsWithMissile = new PortAmmo.Mix
		{
			Apfsds = 12,
			Heat = 4,
			He = 2,
			Extra = new[] { 2 },
		};
		internal static readonly PortAmmo.Mix HullRearCounts = new PortAmmo.Mix { Apfsds = 4, Heat = 1, He = 1 };
		internal static readonly PortAmmo.Mix BustleCounts = new PortAmmo.Mix { Apfsds = 4, Heat = 3, He = 1, Extra = new[] { 2 } };

		// Five boxes of 400 rounds, 2000 in all, from the T-90M's own table (the T-90A carries eight belts of 250).
		internal const int PktBelt = 400;
		const int SpareRack = 4;
		static readonly BlowoutRack.Settings Bustle = new BlowoutRack.Settings
		{
			Tag = T90MMod.FriendlyName,
			Prefix = "T90M_",
			RackPath = "T-72 TURRET COLLIDERS/SPARE AMMO RACK",
			RackName = "bustle box",
			RetrievalDelay = 20f,   // made up. Nobody times a crewman passing a round in through the roof
			StorageDelay = 2f,
			CompartmentName = "turret bustle",
			// The M1's 2 m for 180 s burnt for ages on a box this size, so these are mine. Lids still go on GHPC's flame height.
			ResidualFlame = 0.5f,
			ResidualBurn = 30f,
			FireScale = 0.5f,   // the vents' flames drawn at half the size GHPC gives them
			VolumeName = "Bustle Volume (COMPARTMENT)",
			UpReference = "---MESH---/HULL/TURRET",
			// The lids: 10 mm steel (the box's armour, thickness my reading) in War Thunder's lid sizes, 0.37 x 0.82 m
			// (24 kg) at the sides and 0.51 x 0.88 m (35 kg) in the centre. They are thrown at the M1's speeds (6 and
			// 14 m/s), at the M1's flame heights (2 m for the centre, 1 m for the sides), through vents of the lids'
			// area (radius 0.38 and 0.31 m), with the M1's flame kinds.
			Lids = new[]
			{
				new BlowoutRack.Lid
				{
					Name = "BUSTLE_LID_C",
					ExitName = "blowout center",
					ExitRadius = 0.38f,
					Source = FlammableSourceType.TopLarge,
					MassKg = 35f,
					FlameHeightTrigger = 2f,
					ForceForFlameTrigger = 35f * 6f
				},
				new BlowoutRack.Lid
				{
					Name = "BUSTLE_LID_L",
					ExitName = "blowout left",
					ExitRadius = 0.31f,
					Source = FlammableSourceType.CrewHatch,
					MassKg = 24f,
					FlameHeightTrigger = 1f,
					ForceForFlameTrigger = 24f * 14f
				},
				new BlowoutRack.Lid
				{
					Name = "BUSTLE_LID_R",
					ExitName = "blowout right",
					ExitRadius = 0.31f,
					Source = FlammableSourceType.CrewHatch,
					MassKg = 24f,
					FlameHeightTrigger = 1f,
					ForceForFlameTrigger = 24f * 14f
				},
			},
		};

		internal const float MangoPrevalence = 0.5f;

		internal static PortAmmo.Settings Settings() => new PortAmmo.Settings
		{
			Tag = T90MMod.FriendlyName,
			UnitKey = T90MMod.UnitKey,
			Prefix = "t90m",
			SourceUnit = "T80B",
			// GHPC matches rounds by name, so these can't share one with the T-90A's or the two lists mix.
			Apfsds = new[]
			{
				new PortAmmo.Round
				{
					Id = "3BM60",
					Name = "3BM60 Svinets-2",
					Source = "clip_3BM32",
					Texture = "round_3bm60_co.png",
					Atlas = "125mm_co",
					Shape = "3BM60",
					Set = ammo =>
					{
						ammo.RhaPenetration = 693f;
						// The 4.5 kg is the core's mass from the one Russian list that gives it; the flight mass isn't
						// published. The coefficient is paired with it for a 3.5 % velocity loss per 1000 m. Muzzle
						// velocity 1660 m/s and 8.4 kg with the sabot are the maker's.
						ammo.Mass = 4.5f;
						ammo.MuzzleVelocity = 1660f;
						ammo.Coeff = 0.093f;
					},
					Prevalence = 1f,
				},
				new PortAmmo.Round
				{
					Id = "3BM42",
					Name = "3BM42 Mango",
					Source = "clip_3BM32",
					Texture = "round_3bm42_co.png",
					Atlas = "125mm_co",
					Set = ammo =>
					{
						ammo.RhaPenetration = 578f;
						ammo.Mass = 4.85f;
						ammo.MuzzleVelocity = 1700f;
						ammo.Coeff = 0.16f;
					},
					Prevalence = MangoPrevalence,
				},
			},
			DefaultApfsds = () => T90MMod.Apfsds?.Value,
			ApfsdsListName = "125mm APFSDS (T-90M)",
			MenuSuffix = " (T-90M)",
			FixedHeat = "clip_3BK18M",
			Missile = new PortAmmo.Round
			{
				Id = "9M119M1",
				Name = "9M119M1 Invar-M",
				Source = "clip_9M112M",
				Texture = "round_9m119m1_co.png",
				Atlas = "kobra_co",
				Shape = "9M119M",
				Set = ammo =>
				{
					ammo.RhaPenetration = 850f;
					ammo.Tandem = true;
					ammo.Mass = 17.2f;
					// GHPC flies a missile at one constant speed, 5 km in 17 s.
					ammo.MuzzleVelocity = 294f;
					ammo.MaximumRange = 5000f;
					ammo.ArmingDistance = 100f;
					ammo.RangedFuseTime = 5000f / 294f + 0.3f;
					ammo.Guidance = AmmoType.GuidanceType.Laser;
				},
			},
			EraDefeating = new[] { "9M119M1 Invar-M" },
			Extra = new[]
			{
				new PortAmmo.Round
				{
					Id = "3OF82",
					Name = "3OF82 Telnik",
					Source = "clip_3OF26",
					Texture = "round_3of82_co.png",
					Atlas = "125mm_co",
					Set = ammo =>
					{
						ammo.TntEquivalentKg = 3.97f * 3.0f / 3.4f;
					},
				},
			},
			TotalCounts = Counts,
			// GHPC restocks from the end of this list: hull rack first, the bustle box last.
			Racks = new[]
			{
				new PortAmmo.Rack { Index = 0, Counts = CarouselCountsWithMissile, MissileSlots = new[] { 21, 20 } },
				new PortAmmo.Rack { Index = SpareRack, Counts = BustleCounts, NoRestockInto = true },
				new PortAmmo.Rack { Index = 2, Counts = HullRearCounts, MissileSlots = new[] { 10, 9 } },
			},
			DropOtherRacks = true,
			GuidancePosition = new Vector3(-0.727f, 0.4631f, -5.7249f),
			GuidanceName = "T90M guidance computer",
			BeamName = "T90M guidance beam",
			CoaxFeed = "PKT",
			CoaxRounds = T90MMod.CoaxRounds,
			CoaxBelt = PktBelt,
		};

		internal static readonly AirBurst.Settings Fuse = new AirBurst.Settings
		{
			Tag = T90MMod.FriendlyName,
			RoundName = "3OF82 Telnik",
			MenuName = "3OF82 Telnik fuse",
			Default = AirBurst.Mode.Short,
			// Burst 10 m short: the Telnik's table gives its fragment circle for a burst at 10 m (3.64 m across); what
			// the T-90M's computer uses isn't published, so that is my reading.
			ShortOf = 10f,
			// The height over the aiming point for the burst over the target: my own number.
			Over = 3f,
			// No valid range under the Sosna-U's laser minimum of 300 m.
			MinRange = 300f,
			// The fuse's mean time scatter, 0.05 s, taken as one standard deviation.
			TimeSd = 0.05f,
			Elements = 450,   // of 3 g each (exhibition data)
			// The axial beam's half angle, 20 deg (3.64 m across at 10 m).
			HalfAngle = 20f,
			// The fragment block's design speed, 400 m/s, added to the round's own.
			BlockSpeed = 400f,
			ElementMass = 0.003f,
			// 3 g of steel as a cylinder as long as it is wide: 7.9 mm and a 0.486 cm2 end face (my own working-out).
			ElementArea = 4.86e-5f,
			ElementCaliber = 7.9f,
			ElementCoeff = 0.6f,   // GHPC's Coeff is half the Cd, a tumbling cylinder is about 1.2
			// Penetration of 6 mm RHA at 1200 m/s: my own number (GHPC's 7.62 mm rifle rounds: 5 mm).
			ElementPen = 6f,
			ElementPenSpeed = 1200f,
		};

		internal static readonly GunSetup.Settings Gun = new GunSetup.Settings
		{
			Tag = T90MMod.FriendlyName,
			// Turret slew up to 40 deg/s (the stabiliser of the modernised T-90MS; medium confidence for the T-90M);
			// the T-72M1's is 20.
			TurretSpeed = 40f,
			// No elevation speed is published for the T-90M. Its elevation drive is hydraulic; 28 deg/s comes from a
			// patent for that drive, as on the T-90A.
			GunSpeed = 28f,
			// Depression -6 deg 13' and elevation +13 deg 47' over the nose, the T-72A manual's figures.
			GunLimits = new Vector2(-(6f + 13f / 60f), 13f + 47f / 60f),
			DepressionProfile = "gun_depression.txt",
			// No AutoloaderCycle: the autoloader keeps the game's own T-72M1 timing, about 7.2 s a round.
			GunName = "125mm Gun 2A46M-5",
			CodexName = "T90M 2A46M-5",
		};

		static bool _bustleLogged;

		internal static void FitBustle(Vehicle vehicle, Dictionary<string, (Mesh mesh, Transform bone)> volumes)
		{
			var problems = new List<string>();
			var bustleVolume = volumes != null && volumes.TryGetValue(Bustle.VolumeName, out var found) ? found : (null, null);
			BlowoutRack.Apply(vehicle, Bustle, bustleVolume.mesh, bustleVolume.bone, problems);
			if (problems.Count > 0 && !_bustleLogged)
				MelonLogger.Warning($"{T90MMod.FriendlyName} bustle box: {string.Join("; ", problems)}; its rounds burn with the crew's.");
			_bustleLogged = true;
		}
	}
}
