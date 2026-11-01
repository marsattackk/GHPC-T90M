using System;
using System.Collections.Generic;
using GHPC.Effects;
using GHPC.Vehicle;
using MelonLoader;
using T90M.Common;
using UnityEngine;

namespace T90M
{
	internal static class Firepower
	{
		internal static readonly int[] Counts = { 20, 8, 4, 4, 4 };   // mine, no source gives a regulation mix
		internal static readonly int[] CarouselCounts = { 12, 4, 2, 0, 2 };
		internal static readonly int[] HullRearCounts = { 4, 1, 1, 0, 0 };
		internal static readonly int[] BustleCounts = { 4, 3, 1, 0, 2 };

		const int SpareRack = 4;
		static readonly BlowoutRack.Settings Bustle = new BlowoutRack.Settings
		{
			Prefix = "T90M_",
			RackPath = "T-72 TURRET COLLIDERS/SPARE AMMO RACK",
			RackName = "bustle box",
			RetrievalDelay = 20f,   // made up, nobody times a crewman passing a round in through the roof
			StorageDelay = 2f,
			CompartmentName = "turret bustle",
			// The M1's 2 m for 180 s burnt for ages on a box this size, these are mine. Lids still go on GHPC's flame height.
			ResidualFlame = 0.5f,
			ResidualBurn = 30f,
			VolumeName = "Bustle Volume (COMPARTMENT)",
			UpReference = "---MESH---/HULL/TURRET",
			Lids = new[]
			{
				new BlowoutRack.Lid { Name = "BUSTLE_LID_C", ExitName = "blowout center", ExitRadius = 0.38f, Source = FlammableSourceType.TopLarge, MassKg = 35f, FlameHeightTrigger = 2f, ForceForFlameTrigger = 35f * 6f },
				new BlowoutRack.Lid { Name = "BUSTLE_LID_L", ExitName = "blowout left", ExitRadius = 0.31f, Source = FlammableSourceType.CrewHatch, MassKg = 24f, FlameHeightTrigger = 1f, ForceForFlameTrigger = 24f * 14f },
				new BlowoutRack.Lid { Name = "BUSTLE_LID_R", ExitName = "blowout right", ExitRadius = 0.31f, Source = FlammableSourceType.CrewHatch, MassKg = 24f, FlameHeightTrigger = 1f, ForceForFlameTrigger = 24f * 14f },
			},
		};

		internal const float MangoPrevalence = 0.5f;

		internal static PortAmmo.Settings Ammo() => new PortAmmo.Settings
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
					Id = "3BM60", Name = "3BM60 Svinets-2", Source = "clip_3BM32", Texture = "round_3bm60_co.png", Atlas = "125mm_co", Shape = "3BM60",
					Set = a =>
					{
						a.RhaPenetration = 693f;
						a.Mass = 4.5f;
						a.MuzzleVelocity = 1660f;
						a.Coeff = 0.093f;
					},
					Prevalence = 1f,
				},
				new PortAmmo.Round
				{
					Id = "3BM42", Name = "3BM42 Mango", Source = "clip_3BM32", Texture = "round_3bm42_co.png", Atlas = "125mm_co",
					Set = a =>
					{
						a.RhaPenetration = 578f;
						a.Mass = 4.85f;
						a.MuzzleVelocity = 1700f;
						a.Coeff = 0.16f;
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
				Id = "9M119M1", Name = "9M119M1 Invar-M", Source = "clip_9M112M", Texture = "round_9m119m1_co.png", Atlas = "kobra_co", Shape = "9M119M",
				Set = a =>
				{
					a.RhaPenetration = 850f;
					a.Tandem = true;
					a.Mass = 17.2f;
					// GHPC flies a missile at one constant speed, 5 km in 17 s.
					a.MuzzleVelocity = 294f;
					a.MaximumRange = 5000f;
					a.ArmingDistance = 100f;
					a.RangedFuseTime = 5000f / 294f + 0.3f;
					a.Guidance = AmmoType.GuidanceType.Laser;
				},
			},
			EraDefeating = new[] { "9M119M1 Invar-M" },
			Extra = new[]
			{
				new PortAmmo.Round
				{
					Id = "3OF82", Name = "3OF82 Telnik", Source = "clip_3OF26", Texture = "round_3of82_co.png", Atlas = "125mm_co",
					Set = a =>
					{
						a.TntEquivalentKg = 3.97f * 3.0f / 3.4f;
					},
				},
			},
			TotalCounts = Counts,
			// GHPC restocks from the end of this list: hull rack first, the bustle box last.
			Racks = new[]
			{
				new PortAmmo.Rack { Index = 0, Counts = CarouselCounts, MissileSlots = new[] { 21, 20 } },
				new PortAmmo.Rack { Index = SpareRack, Counts = BustleCounts, NoRestockInto = true },
				new PortAmmo.Rack { Index = 2, Counts = HullRearCounts, MissileSlots = new[] { 10, 9 } },
			},
			DropOtherRacks = true,
			GuidancePosition = new Vector3(-0.727f, 0.4631f, -5.7249f),
			GuidanceName = "T90M guidance computer",
			BeamName = "T90M guidance beam",
			CoaxFeed = "PKT",
			CoaxRounds = 2000,
			CoaxBelt = 400,
		};

		internal static readonly AirBurst.Settings Fuse = new AirBurst.Settings
		{
			Tag = T90MMod.FriendlyName,
			RoundName = "3OF82 Telnik",
			MenuName = "3OF82 Telnik fuse",
			Default = AirBurst.Mode.Short,
			ShortOf = 10f,
			Over = 3f,
			MinRange = 300f,
			TimeSd = 0.05f,
			Elements = 450,
			HalfAngle = 20f,
			BlockSpeed = 400f,
			ElementMass = 0.003f,
			ElementArea = 4.86e-5f,
			ElementCaliber = 7.9f,
			ElementCoeff = 0.6f,   // GHPC's Coeff is half the Cd, a tumbling cylinder is about 1.2
			ElementPen = 6f,
			ElementPenSpeed = 1200f,
		};

		static readonly GunSetup.Settings Gun = new GunSetup.Settings
		{
			Tag = T90MMod.FriendlyName,
			TurretSpeed = 40f,
			GunSpeed = 28f,
			GunLimits = new Vector2(-(6f + 13f / 60f), 13f + 47f / 60f),
			DepressionProfile = "gun_depression.txt",
			AutoloaderCycle = 6.5f,
			GunName = "125mm Gun 2A46M-5",
			CodexName = "T90M 2A46M-5",
		};

		static bool _bustleLogged;

		internal static void Apply(Vehicle vehicle, Dictionary<string, (Mesh mesh, Transform bone)> volumes, bool atSpawn = true)
		{
			if (!atSpawn)
			{
				try { GunSetup.Apply(vehicle, Gun); }
				catch (Exception e) { MelonLogger.Error($"{T90MMod.FriendlyName} gun setup failed: " + e); }
				return;
			}
			try
			{
				var problems = new List<string>();
				var v = volumes != null && volumes.TryGetValue(Bustle.VolumeName, out var x) ? x : (null, null);
				BlowoutRack.Apply(vehicle, Bustle, v.mesh, v.bone, problems);
				if (problems.Count > 0 && !_bustleLogged) MelonLogger.Warning($"{T90MMod.FriendlyName} bustle box: {string.Join("; ", problems)}; its rounds burn with the crew's.");
				_bustleLogged = true;
			}
			catch (Exception e) { MelonLogger.Error($"{T90MMod.FriendlyName} bustle box failed: " + e); }
			try { PortAmmo.ApplyAtSpawn(vehicle); }
			catch (Exception e) { MelonLogger.Error($"{T90MMod.FriendlyName} ammo setup failed: " + e); }
			try { AirBurst.Attach(vehicle); }
			catch (Exception e) { MelonLogger.Error($"{T90MMod.FriendlyName} 3OF82 fuse setup failed: " + e); }
			try { PortAmmo.ApplyCoaxAtSpawn(vehicle); }
			catch (Exception e) { MelonLogger.Error($"{T90MMod.FriendlyName} coax setup failed: " + e); }
			try { GunSetup.Apply(vehicle, Gun); }
			catch (Exception e) { MelonLogger.Error($"{T90MMod.FriendlyName} gun setup failed: " + e); }
		}
	}
}
