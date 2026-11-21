using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GHPC.Player;
using GHPC.State;
using GHPC.Vehicle;
using MelonLoader;
using T90M.Common;
using UnityEngine;

namespace T90M
{
	// The «Арена-М» kit: the active protection KBM fitted on a T-90M for trials, on the shared Aps module and PortKits. A T-90M
	// carries it when its switch is on (your own tank) or its roll against the share setting wins (every other one). A tank with
	// neither stays the T-90M without it. A kit tank gets the second War Thunder model's turret parts (radars, launcher boxes, the
	// reshaped rear) with the parts they replace hidden, the extra armour, the modules, 1150 kg more mass, no turret bustle box
	// (30 rounds: 22 in the carousel, 8 in the hull) and the Arena-M itself.
	// Sources by short name: Rossiyskaya Gazeta 2021 («Раскрыты особенности работы комплекса активной защиты "Арена-М"»), 2023
	// and 2024, Janes (IDEX 2025: KBM's Arena on the T-90M), KBM's Arena-E page, Russian Wikipedia («Арена (активная защита)»),
	// btvt.info («Комплекс активной защиты танка "Арена"»), Militarnyi (the T-90M's Arena-M), www1.ru (2025). Real Arena-M
	// figures are rare: the Arena-E's stand in where the Arena-M has none, the Drozd's (the shared module's defaults) where
	// neither has, and each value says which.
	internal static partial class Arena
	{
		internal const string KitName = "arena";
		const string Label = "Arena-M";
		// KBM gives the Arena-E at 1000 to 1300 kg: the middle. The extra armour's weight is not published (inferred).
		internal const float KitMassKg = 1150f;

		// A rocket bone from War Thunder's model (x forward, y up, z right) into the T-72M1 prefab's frame (x right, y up, z
		// forward). The offset, 3.6 cm down and 83.7 cm back, is the one the whole model got when its road wheels were lined
		// up with the T-72M1's, so a bone lands where its tube is drawn.
		static Vector3 Bone(float x, float y, float z) => new Vector3(z, y - 0.036f, x - 0.837f);

		// The 12 shafts on the turret (Rossiyskaya Gazeta 2023 and 2024: 12) at the model's rocket bones, in two launcher modules
		// of six, a side each (Janes: left and right, six rounds each). The model has 13 bones: bone 07, at the back on the
		// turret's centre line, is left unused and bone 08 joins the right rear box, six a side (inferred). The four boxes are
		// the model's four launchers. Each serves a sector of the turret's circle that overlaps its neighbours', read off where
		// the box sits on the turret and «360 deg» all round (Rossiyskaya Gazeta 2021), inferred. A round climbs out of its
		// tube, turns towards the threat and bursts over it (ClimbBurst), so a box's axis is only where it points at the start:
		// out from the turret and 60 deg up, inferred (the old Arena's munition stands 25 to 40 deg off the vertical, btvt.info).
		static readonly Aps.BlockSpec[] Blocks =
		{
			new Aps.BlockSpec
			{
				Name = "front left",
				Side = 0,
				Module = "arena_launcher_fl",
				AzMin = -120f,
				AzMax = 10f,
				AxisAz = -55f,
				AxisEl = 60f,
				Mouths = new[]
				{
					Bone(0.011f, 1.975f, -1.345f),   // bone 01
					Bone(-0.137f, 1.976f, -1.302f),   // bone 02
					Bone(-0.387f, 1.982f, -1.177f),   // bone 03
				},
			},
			new Aps.BlockSpec
			{
				Name = "rear left",
				Side = 0,
				Module = "arena_launcher_rl",
				AzMin = -200f,   // 160 to 300 round the back
				AzMax = -60f,
				AxisAz = -130f,
				AxisEl = 60f,
				Mouths = new[]
				{
					Bone(-0.640f, 1.986f, -1.085f),   // bone 04
					Bone(-1.647f, 1.899f, -0.445f),   // bone 05
					Bone(-1.654f, 1.901f, -0.153f),   // bone 06
				},
			},
			new Aps.BlockSpec
			{
				Name = "rear right",
				Side = 1,
				Module = "arena_launcher_rr",
				AzMin = 60f,
				AzMax = 200f,
				AxisAz = 130f,
				AxisEl = 60f,
				Mouths = new[]
				{
					Bone(-0.557f, 1.830f, 1.201f),   // bone 09
					Bone(-0.541f, 1.854f, 1.123f),   // bone 10
					Bone(-0.223f, 1.844f, 1.201f),   // bone 11
					Bone(-1.661f, 1.900f, 0.428f),   // bone 08, joined to this box (inferred)
				},
			},
			new Aps.BlockSpec
			{
				Name = "front right",
				Side = 1,
				Module = "arena_launcher_fr",
				AzMin = -10f,
				AzMax = 120f,
				AxisAz = 55f,
				AxisEl = 60f,
				Mouths = new[]
				{
					Bone(0.108f, 1.845f, 1.359f),   // bone 12
					Bone(0.209f, 1.809f, 1.398f),   // bone 13
				},
			},
		};

		// The six radar panels on the model (Militarnyi: six, or four with one secondary), each watching the arc its place on
		// the turret gives it (inferred). Janes puts the radar on the top of the turret above the cheeks, extra sensors at the
		// rear and no radar facing back. A bearing is covered while one live panel holds it, so a destroyed panel blinds its arc.
		static readonly Aps.SensorSpec[] Sensors =
		{
			new Aps.SensorSpec { Name = "front left", Module = "arena_radar_1", AzMin = -90f, AzMax = 10f },
			new Aps.SensorSpec { Name = "front right", Module = "arena_radar_2", AzMin = -10f, AzMax = 90f },
			new Aps.SensorSpec { Name = "left", Module = "arena_radar_3", AzMin = -150f, AzMax = -40f },
			new Aps.SensorSpec { Name = "rear left", Module = "arena_radar_4", AzMin = 160f, AzMax = 240f },
			new Aps.SensorSpec { Name = "rear right", Module = "arena_radar_5", AzMin = 120f, AzMax = 200f },
			new Aps.SensorSpec { Name = "right", Module = "arena_radar_6", AzMin = 40f, AzMax = 150f },
		};

		internal static readonly Aps.Settings Settings = new Aps.Settings
		{
			Tag = "APS T-90M Arena-M",
			Name = Label,
			OnAtSpawn = true,   // armed from the start. The crew's on / off switch is the vehicle menu (Janes: the commander's switch)
			// WarmUp stays the module's 300 s after a switch-on: no Arena-M or Arena-E figure found, so the Drozd's.
			HatchInterlock = false,   // none found for the Arena-M (the Drozd's blocks firing with a hatch open)
			RoundsPerBlock = 6,   // only a cap: a box gets a round per mouth, 3 and 3 on the left, 4 and 2 on the right
			HudPerBlock = true,   // four boxes and six radars: the HUD shows each box's rounds and the radars lost
			Blocks = Blocks,
			Sensors = Sensors,
			// In the turret's rear box (Janes: the rear of the turret holds the computing system), inferred.
			ComputerModule = "arena_computer",
			ElMin = -6f,
			ElMax = 20f,   // -6 to +20 deg (Rossiyskaya Gazeta 2021). The 2024 piece also claims the upper hemisphere but gives no figure
			// KBM gives the Arena-E «около 50» m, but at 50 m a 1000 m/s round (the Arena-M's figure below) is past the burst point
			// before 0.07 s of reaction and the round's flight are over (even the Arena-E's own 700 m/s needs ~85 m). The track
			// starts where the fastest threat can still be met: 1000 m/s x (0.07 + ~0.05 s) + the 8 m intercept, ~130 m (derived).
			SearchRange = 150f,   // inferred: a little over the track range
			TrackRange = 130f,   // derived, see above
			MinRange = 10f,   // inferred
			SpeedMin = 70f,   // 70 to 700 m/s (KBM's Arena-E page)
			SpeedMax = 1000f,   // up to 1000 m/s for the Arena-M (www1.ru, 2025)
			ReactionTime = 0.07f,   // 0.07 s (KBM's Arena-E page)
			// SizeMin, RoundSpeed, BurstMin, BurstMax, FragReach, OtherSide, the kill chances and the fragments' speed, mass and
			// penetration: no Arena-M figure found, so the module's own values, which the Drozd uses too.
			// No figure for the Arena-M's fragments either («направленный поток тяжелых осколков», Rossiyskaya Gazeta 2021): the
			// Drozd's count and its split of what a hit threat does.
			FragCount = 230,
			PBrokenUp = 0.25f,
			WarheadFragShare = 0.25f,
			Cooldown = 0.3f,   // ready again after 0.2 to 0.4 s (Russian Wikipedia, the old Arena)
			PickOverlap = true,   // the boxes' sectors overlap: of those that cover a bearing, the one whose burst misses least
			ClimbBurst = true,   // the round climbs out of its tube and bursts over the threat (Rossiyskaya Gazeta 2021: turns towards it)
			InterceptRange = 8f,   // inferred: well inside the radar's 50 m, close to the turret
			BurstHeight = 2f,   // inferred
			ConeReach = 4f,   // inferred
			FragConeHalf = 35f,   // the old Arena's cone, 25 to 40 deg round the line to the threat (btvt.info): the middle
			RoundsPerThreat = 2,   // two rounds at one threat (Rossiyskaya Gazeta 2021, Lenta 2021)
			DangerRange = 30f,   // 20 to 30 m for friendly infantry (KBM's Arena-E page): the logged danger zone only
			ReloadPerRound = 60f,   // mine, as the Drozd's: a round a minute from the vehicle menu while nearly still
			ReloadMaxSpeed = 0.5f,   // mine, as the Drozd's
			// The round leaves its shaft like a mortar bomb from a tube («выстреливается», Rossiyskaya Gazeta 2021), not like a
			// missile: the game's own smoke grenade launch, a short thump from a tube (no other game sound is a tube launch), not
			// the 9M14 missile's. The Drozd keeps the module's default.
			LaunchEvent = Aps.GroundLaunchEvent,
			VisualPrefix = "T90M_",   // the protected box: the tank's outline, the kit's parts in it (margin 0)
		};

		// Who carries it

		static PortKits.Kit _kit;
		static readonly System.Random Roller = PortKits.NewRandom();
		static readonly List<Vehicle> Pending = new List<Vehicle>();
		static readonly HashSet<string> Warned = new HashSet<string>();
		static PortKits.Tally _tally = new PortKits.Tally();
		static bool _ready;
		static bool _showcase;
		static bool _lineShown;
		static bool _missingWarned;
		static bool _shareWarned;

		// Marks a T-90M that has had its decision, so it is never decided twice.
		sealed class Decided : MonoBehaviour { }

		internal static void Load(AssetBundle bundle)
		{
			var kits = PortKits.Load(bundle, T90MMod.FriendlyName);
			_kit = kits?.FirstOrDefault(kit => string.Equals(kit.Name, KitName, StringComparison.OrdinalIgnoreCase));
		}

		// The two settings as this launch reads them: the share kept to 0..100, said once when it was not.
		static void Read(out bool mine, out int share)
		{
			mine = T90MMod.ArenaMine.Value;
			int given = T90MMod.ArenaShare.Value;
			share = PortKits.ClampShare(given, out bool changed);
			if (changed && !_shareWarned)
			{
				_shareWarned = true;
				MelonLogger.Warning(
					$"{T90MMod.FriendlyName}: 'Arena-M kit share' is {given}; it has to be 0 to 100, so it is read as {share}.");
			}
			DevForce(ref mine, ref share);
		}

		// At start-up: a share outside 0 to 100 is said right away, not at the first tank.
		internal static void CheckSettings()
		{
			Read(out _, out _);
		}

		static bool Wanted()
		{
			Read(out bool mine, out int share);
			return mine || share > 0;
		}

		// Each scene load: in a mission the decisions wait for PlayerReady. The player's tank is not known before it: GHPC's
		// mission composer hands it to the player then, after the T-90Ms were spawned and converted. The Showcase's T-90M is
		// placed after PlayerReady, belongs to nobody and is there to be looked at and driven: it follows the player's switch.
		internal static void OnSceneLoaded(bool menu, bool showcase)
		{
			_showcase = showcase && !menu;
			if (_ready)
			{
				_tally = new PortKits.Tally();
				_lineShown = false;
			}
			_ready = false;
			if (menu)
			{
				Pending.Clear();
				return;
			}
			// Every mission: the settings are read when a tank is offered (Offer), so a tank converted before PlayerReady is
			// always decided then, whatever was known at the scene load.
			StateController.RunOrDefer(GameState.PlayerReady, _ => AtPlayerReady(), GameStatePriority.Low);
		}

		static IEnumerator AtPlayerReady()
		{
			if (_ready) yield break;
			_ready = true;
			var waiting = Pending.ToList();
			Pending.Clear();
			foreach (var vehicle in waiting) if (vehicle != null) DecideNow(vehicle);
			if (!_lineShown) MelonCoroutines.Start(SayCounts());
		}

		// A moment after PlayerReady, once the tanks still being set up have had their decision too.
		static IEnumerator SayCounts()
		{
			_lineShown = true;
			yield return new WaitForSecondsRealtime(2f);
			Read(out _, out int share);
			if (_tally.Total > 0) MelonLogger.Msg(_tally.Line(Label, T90MMod.FriendlyName, share));
		}

		// After a T-90M's conversion and late setup (Conversion.LateSetup): decided at once when the mission is past PlayerReady
		// (the player's tank is known), else at PlayerReady. Nothing at all while both settings are at their defaults.
		internal static void Offer(Vehicle vehicle)
		{
			try
			{
				if (vehicle == null || vehicle.GetComponent<Decided>() != null || !Wanted()) return;
				if (_kit == null)
				{
					if (!_missingWarned)
					{
						_missingWarned = true;
						MelonLogger.Warning($"{T90MMod.FriendlyName}: this build has no Arena-M parts; the Arena-M settings do nothing.");
					}
					return;
				}
				if (!_ready)
				{
					if (!Pending.Contains(vehicle)) Pending.Add(vehicle);
					return;
				}
				DecideNow(vehicle);
			}
			catch (Exception exception)
			{
				MelonLogger.Error($"{T90MMod.FriendlyName} Arena-M decision failed (this tank stays as it is): {exception}");
			}
		}

		static void DecideNow(Vehicle vehicle)
		{
			if (vehicle.GetComponent<Decided>() != null) return;
			vehicle.gameObject.AddComponent<Decided>();
			Read(out bool mine, out int share);
			bool isPlayer = _showcase || (PlayerInput.Instance != null && PlayerInput.Instance.CurrentPlayerUnit == vehicle);
			bool carries = PortKits.Decide(isPlayer, mine, share, Roller, out double draw);
			_tally.Add(isPlayer, carries);
			DevDecided(vehicle, isPlayer, mine, share, draw, carries);
			if (_showcase)
				MelonLogger.Msg($"{Label}: the Showcase's {T90MMod.FriendlyName} {(carries ? "yes" : "no")} (as 'Arena-M kit on my tank')");
			if (carries) Fit(vehicle);
		}

		// The kit tank itself

		static readonly ConversionStep<Vehicle>[] Steps =
		{
			new ConversionStep<Vehicle>
			{
				What = "Arena-M parts",
				Otherwise = "no Arena-M parts, armour or modules; the rest of the kit still follows",
				Run = FitParts,
			},
			new ConversionStep<Vehicle> { What = "Arena-M mass", Otherwise = "the T-90M's own mass", Run = AddMass },
			new ConversionStep<Vehicle>
			{
				What = "no bustle box",
				Otherwise = "the bustle box and its 10 rounds stay",
				Run = DropBustle,
			},
			new ConversionStep<Vehicle>
			{
				What = "Arena-M active protection",
				Otherwise = "the kit's parts without the active protection",
				Run = FitProtection,
			},
		};

		static bool _noParts;

		// Never at spawn: PlayerReady or later. A SpawnOnly row added here would be skipped with its Late warning.
		static void Fit(Vehicle vehicle)
		{
			_noParts = false;
			// The last argument skips a row when it returns true. Once FitParts finds no kit in the bundle the others stay off.
			ConversionSteps.Run(T90MMod.FriendlyName, Steps, vehicle, vehicle.name, false, step => _noParts);
		}

		static PortKits.Context Context()
		{
			// The turret and gun bones were moved onto the model's own axes at conversion, and the armour pieces are built round
			// the base rig's pivots: the kit's go in as if the bones were still there.
			var axes = PortAxes.FromRig(T90MMod.Rig, BaseRigT72M1.TurretPivot, BaseRigT72M1.GunPivot, T90MMod.FriendlyName);
			return new PortKits.Context
			{
				Tag = T90MMod.FriendlyName,
				Prefix = "T90M_",
				ArmourPrefab = Armour.Prefab,
				ArmourValues = Armour.KitValues,
				Modules = Conversion.Modules,
				BoneMoved = group => axes == null
					? Vector3.zero
					: group == "TURRET"
						? axes.NewTurret - axes.BaseTurret
						: group == "GUN" ? axes.NewGun - axes.BaseGun : Vector3.zero,
			};
		}

		static void FitParts(Vehicle vehicle)
		{
			var problems = new List<string>();
			string done = PortKits.Apply(vehicle, _kit, Context(), problems);
			if (done == null)
			{
				// a new DLL on an older bundle: a tank with the mass and the APS but none of the parts would be worse than none
				_noParts = true;
				DevFitted(vehicle, "parts", "none in the bundle");
				Say(problems, "Arena-M kit");
				return;
			}
			done += "; " + Conversion.NumbersOnKitLamp(vehicle);
			// The kit's tubes point elsewhere than the plain ones of the same number: the screens' grenades follow the kit's caps.
			var screens = ShtoraScreen.Of(vehicle);
			if (screens != null) done += "; " + screens.Aim();
			DevFitted(vehicle, "parts", done);
			Say(problems, "Arena-M kit");
		}

		// A settings object of its own with only the mass in it: nothing else of the drive changes and the shared one stays as it is.
		static void AddMass(Vehicle vehicle)
		{
			var settings = new Mobility.Settings
			{
				Tag = T90MMod.FriendlyName,
				CombatMassKg = Drive.Settings.CombatMassKg + KitMassKg,
			};
			string done = Mobility.Apply(vehicle, settings);
			DevFitted(vehicle, "mass", done);
		}

		static void DropBustle(Vehicle vehicle)
		{
			var problems = new List<string>();
			string done = Firepower.DropBustle(vehicle, problems);
			DevFitted(vehicle, "bustle box", done);
			Say(problems, "Arena-M, bustle box");
		}

		static void FitProtection(Vehicle vehicle)
		{
			var settings = Settings.Copy();
			// On the turret's axis at roof height (the radar panels' bearings are taken from there): the axis is the model's own,
			// as the rig map has it, the height the panels' (inferred).
			var turret = T90MMod.Rig != null && T90MMod.Rig.HasAxes ? T90MMod.Rig.Turret : BaseRigT72M1.TurretPivot;
			settings.RadarOrigin = new Vector3(0f, 2.1f, turret.z);
			string done = Aps.Apply(vehicle, settings);
			DevFitted(vehicle, "active protection", done);
		}

		// What did not work, said once per kind a session.
		static void Say(List<string> problems, string what)
		{
			var fresh = problems.Where(problem => Warned.Add(what + ": " + problem)).ToList();
			if (fresh.Count > 0) MelonLogger.Warning($"{T90MMod.FriendlyName} {what}: {string.Join("; ", fresh)}.");
		}

		// Dev hooks: optional methods the author's own test harness fills in. They compile to nothing in a normal build, and so
		// does every argument written into a call to one: do the work first, then hand the hook its result.
		static partial void DevForce(ref bool mine, ref int share);

		static partial void DevDecided(Vehicle vehicle, bool isPlayer, bool mine, int share, double draw, bool carries);

		static partial void DevFitted(Vehicle vehicle, string what, string result);
	}
}
