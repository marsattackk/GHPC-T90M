using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GHPC;
using GHPC.Camera;
using GHPC.Equipment.Optics;
using GHPC.PhysicsHelpers;
using GHPC.Utility;
using GHPC.Vehicle;
using GHPC.Weapons;
using MelonLoader;
using T90M.Common;
using UnityEngine;

namespace T90M
{
	using Donor = T90M.Common.RemoteMg.Donor;

	// The commander's PK PAN panorama and the remote Kord on top of it.
	internal static partial class Commander
	{
		static string Tag => T90MMod.FriendlyName;

		internal const string Name = "12.7mm Machine Gun 6P49MT Kord";
		internal const int BeltRounds = 300;   // one box on the mount, no spare inside (inferred from the box's place)
		internal const int Belts = 1;
		// The 6P49MT has a variable rate of fire; 200 and 100 are the two rates the tank's description gives (the T-90A's plain
		// 6P49 fires at its cyclic 600-650).
		internal const float RoundsPerMinute = 200f;
		internal const float LowRoundsPerMinute = 100f;
		const int BeltB32PerTracer = 3;

		// The manual's firing angles for the remote station: -10 / +45 deg.
		static readonly Vector2 Elevation = new Vector2(-10f, 45f);
		// The same figure: 316 deg of traverse.
		internal const float FireSector = 316f;
		// The middle of the blind sector, over the gunner's Sosna-U head and hatch: my own choice, the manual gives
		// only the size of the gap.
		internal const float BlindCentre = -45f;
		// The station follows the panorama's line of sight, so its slew speeds are the panorama's: at least 45 deg/s in
		// traverse and 10 in elevation. The station's own aren't published.
		const float DrumSpeed = 45f;
		const float CradleSpeed = 10f;

		// The panorama's TV fields are 7.4 x 5.6 and 2.5 x 1.9 deg; these are the heights.
		const float TvWide = 5.6f;
		const float TvNarrow = 1.9f;
		static readonly GunnerFcs.Settings PanFcs = new GunnerFcs.Settings
		{
			Tag = T90MMod.FriendlyName + " PK PAN",
			// The panorama's laser range, 300 to 7500 m.
			LaserMin = 300f,
			LaserMax = 7500f,
			// The panorama's elevation limits, -15 / +45 deg.
			LineOfSightLimits = new Vector2(-15f, 45f),
			// The step of a hand-entered range isn't published; the Sosna-U's is used (my reading).
			RangeStep = 100,
			// The range display counts in 5 m.
			DisplayRangeIncrement = 5,
			// The donor station's own fire gate; none is published for this one.
			FireGateAngle = -1f,
			FireWhenLaid = () => T90MMod.FireWhenLaid == null || T90MMod.FireWhenLaid.Value,
			ManualMode = false,
		};
		// The panorama's window on the model, measured on War Thunder's mesh.
		static readonly Vector3 Eye = new Vector3(0.499f, 2.597f, -1.606f);

		static readonly FieldInfo SlotReparent = Reflect.Field(typeof(CameraSlot), "_reparentTarget");

		internal class Station : RemoteMg.Station
		{
			internal AimablePlatform Drum;
			internal WeaponSystem Weapon;
			internal FireControlSystem Fcs;
			internal FireControlSystem MainFcs;
			internal UsableOptic Tv;
			internal UsableOptic Thermal;
			internal UsableOptic DublTv;
			internal UsableOptic DublThermal;
			internal UsableOptic Pdt;
			internal Transform Turret;
			internal bool LowRate;
			internal float Rate => LowRate ? LowRoundsPerMinute : RoundsPerMinute;
			internal void SetLowRate(bool low)
			{
				LowRate = low;
				RemoteMg.SetRate(Weapon, Rate);
			}

			internal override IEnumerable<CameraSlot> Sights => new[] { Tv?.slot, DublTv?.slot, Pdt?.slot }.Where(s => s != null);
			internal override IEnumerable<CameraSlot> WeaponSights => new[] { Tv?.slot, Thermal?.slot }.Where(s => s != null);
			internal float Yaw => Drum != null ? Mathf.DeltaAngle(0f, Drum.GetCurrentEulerAngle()) : 0f;
			internal bool InBlindSector => Mathf.Abs(Mathf.DeltaAngle(Yaw, BlindCentre)) < (360f - FireSector) / 2f;
			internal bool PanoramaView => Active(Tv) || Active(Thermal);
			internal bool DublView => Active(DublTv) || Active(DublThermal);
			static bool Active(UsableOptic o) => o != null && o.slot != null && o.slot.IsActive;
		}

		static readonly Dictionary<WeaponSystem, Station> ByWeapon = new Dictionary<WeaponSystem, Station>();
		// What the station's building steps share.
		sealed class Fit
		{
			internal Vehicle Vehicle;
			internal Transform Root;
			internal Transform Turret;
			internal WeaponsManager Weapons;
			internal FireControlSystem MainFcs;
			internal Donor Donor;
			internal RigMap Rig;
			internal RigMap.Mount DrumMount;
			internal RigMap.Mount CradleMount;
			internal float Pitch;
			internal List<string> Problems = new List<string>();
			internal Transform DrumT;
			internal Transform Frame;
			internal Transform CradleT;
			internal Dictionary<string, Transform> VisOf = new Dictionary<string, Transform>();
			internal GameObject Holder;
			internal RemoteMg.Gun Gun;
			internal GameObject FcsGo;
			internal FireControlSystem Fcs;
			internal GameObject TvGo;
			internal Transform Head;
			internal GameObject Scripts;
			internal AimablePlatform Drum;
			internal AimablePlatform Cradle;
			internal KordBelt.Belt Belt;
			internal KordBrass Brass;
			internal UsableOptic TvOptic;
			internal UsableOptic ThermalOptic;
			internal UsableOptic DublTv;
			internal UsableOptic DublThermal;

			internal Vector3 World(Vector3 prefab) => Root.TransformPoint(prefab);

			internal Transform Carrier(string group) =>
				group == DrumMount.Group ? Frame : group == CradleMount.Group ? CradleT : null;

			// The model has the Kord pitched 1.17 deg nose down with the turret, GHPC lays the cradle level on the aim.
			internal void Level(Transform part) => part.RotateAround(CradleT.position, CradleT.right, -Pitch);
		}

		internal static void Apply(Vehicle vehicle)
		{
			DevFitting(vehicle);
			var fit = Begin(vehicle);
			if (fit == null) return;
			BuildFrames(fit);
			FitParts(fit);
			FitArmour(fit);
			if (!CloneGunAndFcs(fit)) return;
			BuildDrives(fit);
			BuildOptics(fit);
			WakeParts(fit);
			FitBelt(fit);
			Register(fit);
			foreach (var problem in fit.Problems) MelonLogger.Warning($"{Tag} commander's station: {problem}");
		}

		// The rig mounts, the donor and the turret the station needs; null (and a warning) when one is missing.
		static Fit Begin(Vehicle vehicle)
		{
			var root = vehicle.transform;
			var rig = T90MMod.Rig;
			RigMap.Mount M(string group) => rig?.Mounts?.FirstOrDefault(m => m.Group == group);
			var drumM = M("RWS");
			var cradleM = M("RWS_CRADLE");
			if (drumM == null || cradleM == null)
			{
				MelonLogger.Warning($"{Tag}: the bundle has no remote-station mounts (old bundle?); the station stays fixed and unarmed.");
				return null;
			}
			var donor = Donor.Get(Tag);
			if (donor == null || donor.CommanderOptic == null)
			{
				string why = donor == null ? "was not found" : "has no Commander's Optic";
				MelonLogger.Warning($"{Tag}: the T-64B station donor {why}; the station stays fixed and unarmed.");
				return null;
			}
			var turret = root.Find("---MESH---/HULL/TURRET");
			var weapons = vehicle.GetComponent<WeaponsManager>();
			if (turret == null || weapons?.Weapons == null || weapons.Weapons.Length < 2)
			{
				MelonLogger.Warning($"{Tag}: no turret bone or main gun / coax rows; the station stays fixed.");
				return null;
			}
			return new Fit
			{
				Vehicle = vehicle,
				Root = root,
				Turret = turret,
				Weapons = weapons,
				MainFcs = weapons.Weapons[0].FCS,
				Donor = donor,
				Rig = rig,
				DrumMount = drumM,
				CradleMount = cradleM,
				Pitch = rig.HasAxes ? rig.PitchDeg : 0f,
			};
		}

		// The empty transforms the station's parts hang on: the drum's axis, the drum, its frame and the elevation cradle.
		static void BuildFrames(Fit fit)
		{
			var axisT = new GameObject("T90M RWS axis").transform;
			axisT.SetParent(fit.Turret, false);
			axisT.SetPositionAndRotation(
				fit.World(fit.DrumMount.Pivot),
				fit.Root.rotation * Quaternion.FromToRotation(Vector3.up, fit.DrumMount.Axis.normalized));
			fit.DrumT = new GameObject("T90M RWS drum").transform;
			fit.DrumT.SetParent(axisT, false);
			fit.Frame = new GameObject("T90M RWS").transform;
			fit.Frame.SetParent(fit.DrumT, false);
			fit.Frame.SetPositionAndRotation(fit.World(fit.DrumMount.Pivot), fit.Root.rotation);
			fit.CradleT = new GameObject("T90M RWS cradle").transform;
			fit.CradleT.SetParent(fit.Frame, false);
			fit.CradleT.SetPositionAndRotation(
				fit.World(fit.CradleMount.Pivot),
				fit.Root.rotation * Quaternion.FromToRotation(Vector3.right, fit.CradleMount.Axis.normalized));
		}

		// The bundle's station parts, moved under the drum's frame or the cradle.
		static void FitParts(Fit fit)
		{
			foreach (var mount in fit.Rig.Mounts)
			{
				var vis = fit.Turret.GetComponentsInChildren<Transform>(true).FirstOrDefault(p => p.name == "T90M_" + mount.Group)
					?? fit.Turret.GetComponentsInChildren<Transform>(true).FirstOrDefault(p => p.name == mount.Group);
				var carrier = mount.Axis != Vector3.zero ? fit.Carrier(mount.Group) : fit.Carrier(mount.Rides);
				if (vis == null || carrier == null)
				{
					fit.Problems.Add($"part '{mount.Group}' {(vis == null ? "not in the bundle" : "has no drive")}");
					continue;
				}
				vis.name = "T90M_" + mount.Group;
				fit.VisOf[mount.Group] = vis;
				vis.SetParent(carrier, true);
				if (carrier == fit.CradleT) fit.Level(vis);
			}
		}

		// The armour colliders of the drum and the cradle.
		static void FitArmour(Fit fit)
		{
			var root = fit.Root;
			Transform Group(string name, Transform follow) => Followers.ColliderGroup(root, name, follow, "ARMOR", "AAR");
			var drumGroup = Group("T90M RWS COLLIDERS", fit.Frame);
			var cradleGroup = Group("T90M RWS_CRADLE COLLIDERS", fit.CradleT);
			if (Armour.Prefab == null || Armour.Values == null) return;
			string PathOf(Transform part)
			{
				var path = part.name;
				for (var parent = part.parent;
					parent != null && parent != root;
					parent = parent.parent) path = parent.name + "/" + path;
				return path;
			}
			var left = new List<string>();
			DmArmourApply.Apply(
				fit.Vehicle,
				Armour.Prefab,
				Armour.Values,
				Tag,
				left,
				null,
				new[]
				{
					(fit.DrumMount.Group, PathOf(fit.Frame), "RWS COLLIDERS"),
					(fit.CradleMount.Group, PathOf(fit.CradleT), "RWS_CRADLE COLLIDERS"),
				});
			foreach (Transform piece in cradleGroup.Find("ARMOR")) fit.Level(piece);
			RaycastColliderGroup.AddNewColliderSetToParentGroup(drumGroup);
			RaycastColliderGroup.AddNewColliderSetToParentGroup(cradleGroup);
			if (left.Count > 0) fit.Problems.Add("armour: " + string.Join("; ", left));
		}

		// The Kord and its fire control cloned from the donor into a holder that stays inactive until the end; false when the
		// gun fails, with the station left unbuilt.
		static bool CloneGunAndFcs(Fit fit)
		{
			var root = fit.Root;
			fit.Holder = new GameObject("T90M RWS (setup)");
			fit.Holder.SetActive(false);
			fit.Holder.transform.SetParent(root, false);
			fit.Gun = RemoteMg.CloneGun(
				fit.Donor,
				fit.Holder.transform,
				new RemoteMg.GunSettings
				{
					Name = Name,
					DamageName = "6P49MT Kord",
					BeltRounds = BeltRounds,
					Belts = Belts,
					RoundsPerMinute = RoundsPerMinute,
					B32PerTracer = BeltB32PerTracer
				},
				Tag);
			if (fit.Gun == null)
			{
				Object.Destroy(fit.Holder);
				return false;
			}
			// Ends up last under the root, GetComponentInChildren<FireControlSystem>() elsewhere has to find the gunner's.
			var (fcsGo, fcs, tvGo, _, _) = RemoteMg.CloneFcs(fit.Donor, fit.Holder.transform, "T90M Kord fire control");
			fit.FcsGo = fcsGo;
			fit.Fcs = fcs;
			fit.TvGo = tvGo;
			fit.Head = new GameObject("T90M PK PAN head").transform;
			fit.Head.SetParent(fit.Frame, false);
			fit.Head.SetPositionAndRotation(fit.World(Eye), root.rotation);
			var reference = new GameObject("T90M Kord aim reference").transform;
			reference.SetParent(fit.Frame, false);
			reference.SetPositionAndRotation(fit.World(Eye), root.rotation);
			fcs.ReferenceTransform = reference;
			fcs.LaserOrigin = null;
			fcs.LinkedWeaponSystems = new[] { fit.Gun.Weapon };
			return true;
		}

		// The drum and cradle drives, both stabilised, handed to the Kord's fire control.
		static void BuildDrives(Fit fit)
		{
			fit.Scripts = new GameObject("---T90M RWS SCRIPTS---");
			fit.Scripts.transform.SetParent(fit.Root, false);
			fit.Drum = RemoteMg.Platform(
				fit.Scripts.transform,
				"RWS drive",
				fit.Donor.CupolaDrive,
				fit.DrumT,
				EnumHelpers.Axis3.Y,
				Vector2.zero,
				DrumSpeed,
				null);
			fit.Cradle = RemoteMg.Platform(
				fit.Scripts.transform,
				"RWS elevation drive",
				fit.Donor.ElevationDrive,
				fit.CradleT,
				EnumHelpers.Axis3.X,
				Elevation,
				CradleSpeed,
				null);
			// GHPC ignores a goal more than 90 deg off, the drum froze on a quick slew behind.
			fit.Drum.ProtectFromBadProjection = false;
			fit.Cradle.ProtectFromBadProjection = false;
			fit.Drum.Stabilized = fit.Cradle.Stabilized = true;
			fit.Fcs.Mounts = new[] { fit.Drum, fit.Cradle };
		}

		// The panorama's TV and thermal channels, the same two for Дубль, and the laser window Дубль lases from.
		static void BuildOptics(Fit fit)
		{
			var holder = fit.Holder.transform;
			var cmdHead = fit.Root.GetComponentsInChildren<CameraSlot>(true).FirstOrDefault(s => s.name == "commander head");
			fit.TvOptic = fit.TvGo.GetComponent<UsableOptic>();
			fit.ThermalOptic = CloneOptic(fit.Donor, holder);
			fit.DublTv = CloneOptic(fit.Donor, holder);
			fit.DublThermal = CloneOptic(fit.Donor, holder);
			// Onto the head while inactive. ThermalSight adds a post volume beside the optic's post, and left in the holder it
			// dies with it and EnablePost throws on it.
			foreach (var optic in new[] { fit.TvOptic, fit.ThermalOptic, fit.DublTv, fit.DublThermal })
			{
				optic.gameObject.SetActive(false);
				optic.transform.SetParent(fit.Head, false);
				optic.transform.localPosition = Vector3.zero;
				optic.transform.localRotation = Quaternion.identity;
			}
			var gunnerWeapons = new[] { fit.Weapons.Weapons[0].Weapon, fit.Weapons.Weapons[1].Weapon };
			var mainRounds = GuidanceBeam.DayOptic(fit.MainFcs)?.RegisteredAmmoTypes;
			var kord = new[] { fit.Gun.Weapon };
			var kordRounds = new[] { fit.Gun.B32 };
			var problems = fit.Problems;
			SetupTv(fit.TvOptic, "PK PAN TV channel", fit.Fcs, fit.Head, fit.Frame, kord, kordRounds, cmdHead, true, problems);
			SetupTv(
				fit.DublTv,
				"PK PAN Dubl TV",
				fit.MainFcs,
				fit.Head,
				fit.Frame,
				gunnerWeapons,
				mainRounds,
				cmdHead,
				false,
				problems);
			SetupThermal(
				fit.TvOptic,
				fit.ThermalOptic,
				"PK PAN thermal channel",
				fit.Fcs,
				fit.Head,
				kord,
				kordRounds,
				cmdHead,
				problems);
			SetupThermal(
				fit.DublTv,
				fit.DublThermal,
				"PK PAN Dubl thermal",
				fit.MainFcs,
				fit.Head,
				gunnerWeapons,
				mainRounds,
				cmdHead,
				problems);
			fit.DublTv.gameObject.AddComponent<ThermalSight.GateFollowsSight>().Init(fit.MainFcs, fit.DublTv.slot);
			// In Дубль the gun's laser leaves from the panorama's window, else the spot sits off its centre close in.
			var panLaser = new GameObject("T90M PK PAN laser window (Dubl)").transform;
			panLaser.SetParent(fit.Head, false);
			panLaser.localPosition = Vector3.zero;
			panLaser.localRotation = Quaternion.identity;
			GunnerFcs.LaserAim.FromWhile(fit.MainFcs, fit.DublTv.slot, panLaser, PanFcs.LaserMax);
			GunnerFcs.LaserAim.FromWhile(fit.MainFcs, fit.DublThermal.slot, panLaser, PanFcs.LaserMax);
		}

		// The Kord and its fire control placed and switched on, the setup holder gone.
		static void WakeParts(Fit fit)
		{
			fit.Gun.Go.transform.SetParent(fit.CradleT, false);
			fit.Gun.Go.transform.SetPositionAndRotation(fit.World(Muzzle), fit.CradleT.rotation);
			fit.Gun.Go.SetActive(true);
			fit.FcsGo.transform.SetParent(fit.Scripts.transform, false);
			fit.FcsGo.SetActive(true);
			// Night channels first, a day slot marks its linked night sight when it wakes.
			foreach (var optic in new[] { fit.ThermalOptic, fit.DublThermal, fit.TvOptic, fit.DublTv })
			{
				if (optic.postAdditional != null
					&& optic.postAdditional.Any(v => v == null || v.transform.IsChildOf(fit.Holder.transform)))
				{
					fit.Problems.Add($"'{optic.name}': a post volume was left in the setup holder; dropped");
					optic.postAdditional = optic.postAdditional
						.Where(v => v != null && !v.transform.IsChildOf(fit.Holder.transform))
						.ToArray();
				}
				optic.gameObject.SetActive(true);
			}
			Object.Destroy(fit.Holder);
			fit.Gun.Weapon.SetCycleTime(fit.Gun.Cycle);
		}

		// The ammunition belt and the spent cases; nothing when the bundle has no belt part or the belt fails.
		static void FitBelt(Fit fit)
		{
			KordBelt.Belt belt = null;
			if (fit.VisOf.TryGetValue("RWS_BOX", out var boxVis))
				try { belt = KordBelt.Fit(boxVis, fit.CradleT, BeltSettings); }
				catch (System.Exception exception) { MelonLogger.Error($"{Tag} Kord belt failed: " + exception); }
			else fit.Problems.Add("no ammunition belt part (RWS_BOX): the belt stays still");
			if (belt == null) return;
			fit.Belt = belt;
			var gun = fit.Gun;
			belt.Attach(gun.Weapon, gun.Cycle);
			if (KordCases)
				fit.Brass = KordBrass.Attach(
					fit.Vehicle,
					gun.Weapon,
					gun.Cycle,
					fit.CradleT,
					fit.CradleT.InverseTransformPoint(fit.World(CaseChute)),
					CaseChuteDir,
					belt,
					new[] { fit.Frame, fit.Turret.Find("GUN"), fit.Turret, fit.Turret.parent });
		}

		// The station component, the weapon system entry and the Link that keeps the panorama and the gunner in step.
		static void Register(Fit fit)
		{
			var gun = fit.Gun;
			var info = new WeaponSystemInfo
			{
				Name = Name,
				Role = WeaponSystemRole.RoofGun,
				Weapon = gun.Weapon,
				FCS = fit.Fcs,
				PreAimWeapon = WeaponSystemRole.MainGun,
				PreAimAngleThreshold = 3f
			};
			var station = fit.Scripts.AddComponent<Station>();
			station.Info = info;
			station.Drum = fit.Drum;
			station.Weapon = gun.Weapon;
			station.Fcs = fit.Fcs;
			station.MainFcs = fit.MainFcs;
			station.Tv = fit.TvOptic;
			station.Thermal = fit.ThermalOptic;
			station.DublTv = fit.DublTv;
			station.DublThermal = fit.DublThermal;
			station.Turret = fit.Turret;
			RemoteMg.Register(
				fit.Vehicle,
				fit.Weapons,
				info,
				new[] { fit.Drum, fit.Cradle },
				new Component[]
				{
					fit.Drum,
					fit.Cradle,
					gun.Weapon,
					gun.Feed,
					fit.Fcs,
					fit.TvOptic.slot,
					fit.ThermalOptic.slot,
					fit.DublTv.slot,
					fit.DublThermal.slot,
				},
				station.Sights);
			ByWeapon[gun.Weapon] = station;
			fit.Scripts.AddComponent<Link>().Init(station);
			UnityKeys.Prune(ByWeapon);
			DevFitted(station, fit);
		}

		// The panorama's optics are clones of the donor's commander optic. The counts of what a clone dropped aren't used.
		internal static UsableOptic CloneOptic(Donor donor, Transform holder)
		{
			int removed = 0, scrubbed = 0;
			return RemoteMg.CloneOptic(donor, donor.CommanderOptic, holder, ref removed, ref scrubbed);
		}

		// The Kord's muzzle on War Thunder's model, levelled about the trunnion with the cradle's parts.
		static readonly Vector3 WtMuzzle = new Vector3(0.2604f, 2.8311f, -0.920f);
		static Vector3 Muzzle => Levelled(WtMuzzle);
		static Vector3 Levelled(Vector3 wt)
		{
			var cradleMount = T90MMod.Rig?.Mounts?.FirstOrDefault(m => m.Group == "RWS_CRADLE");
			if (cradleMount == null) return wt;
			float pitch = T90MMod.Rig.HasAxes ? T90MMod.Rig.PitchDeg : 0f;
			return cradleMount.Pivot + Quaternion.AngleAxis(-pitch, Vector3.right) * (wt - cradleMount.Pivot);
		}

		static readonly Vector3 WtCaseChute = new Vector3(0.300f, 2.800f, -1.455f);
		static Vector3 CaseChute => Levelled(WtCaseChute);
		static readonly Vector3 CaseChuteDir = new Vector3(0.10f, -0.08f, 1f);
		static bool KordCases => T90MMod.KordCases == null || T90MMod.KordCases.Value;
		static readonly KordBelt.Settings BeltSettings = new KordBelt.Settings
		{
			Tag = "T-90M Kord",
			Prefix = "T90M_",
			MeshName = "T90M Kord",
			BeltMaterial = "t90m_mg_6p49_kord_ammo",
			// My own number, taken from the T-90A (its elevation reaches +70 deg); this one reaches +45 and needs
			// fewer.
			SpareRounds = 24,
			MaxCases = BeltRounds * Belts,
		};

		internal static void LateSetup(Vehicle vehicle)
		{
			var station = vehicle.GetComponentInChildren<Station>(true);
			if (station == null) return;
			var weapons = vehicle.GetComponent<WeaponsManager>();
			var gunner = new[] { weapons.Weapons[0].Weapon, weapons.Weapons[1].Weapon };
			var day = GuidanceBeam.DayOptic(station.MainFcs);
			var slots = new[] { day?.slot, day?.slot.LinkedNightSight, station.Pdt?.slot }.Where(s => s != null).ToArray();
			foreach (var slot in slots) slot.ExclusiveWeapons = gunner;
			// The T-64B's NSVT starts unstabilised, and BoresightStabilized only follows the aim with the stabiliser on.
			station.Fcs.SetStabsActive(true);
			RemoteMg.LateSetup(vehicle);
		}

		// Dev hooks: optional methods the author's own test harness fills in. They compile to nothing in a normal build.
		static partial void DevFitting(Vehicle vehicle);

		static partial void DevFitted(Station station, Fit fit);

		static partial void DevHandOver(ref bool held);
	}
}
