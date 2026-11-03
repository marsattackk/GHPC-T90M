using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GHPC;
using GHPC.Camera;
using GHPC.Crew;
using GHPC.Equipment.Optics;
using GHPC.PhysicsHelpers;
using GHPC.Player;
using GHPC.Utility;
using GHPC.Vehicle;
using GHPC.Weaponry;
using GHPC.Weapons;
using HarmonyLib;
using MelonLoader;
using T90M.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.PostProcessing;

namespace T90M
{
	using Donor = T90M.Common.RemoteMg.Donor;

	internal static class Station
	{
		static string Tag => T90MMod.FriendlyName;

		internal const string KordName = "12.7mm Machine Gun 6P49MT Kord";
		internal const int BeltRounds = 300;
		internal const int Belts = 1;
		internal const float RateHigh = 200f, RateLow = 100f;
		const int B32PerTracer = 3;

		static readonly Vector2 Elevation = new Vector2(-10f, 45f);
		internal const float FireSector = 316f;
		internal const float BlindCentre = -45f;
		const float DrumSpeed = 45f, CradleSpeed = 10f;

		const float TvWide = 5.6f, TvNarrow = 1.9f;
		const float ThermalWide = 6.75f, ThermalNarrow = 2.25f, ThermalZoom = 1.125f;
		const int ThermalWidth = 1024, ThermalHeight = 576;
		static readonly GunnerFcs.Settings PanFcs = new GunnerFcs.Settings
		{
			LaserMin = 300f,
			LaserMax = 7500f,
			LineOfSightLimits = new Vector2(-15f, 45f),
			RangeStep = 100,
			DisplayRangeIncrement = 5,
			FireGateAngle = -1f,
			FireWhenLaid = () => T90MMod.FireWhenLaid == null || T90MMod.FireWhenLaid.Value,
			ManualMode = false,
		};
		static readonly Vector3 Eye = new Vector3(0.499f, 2.597f, -1.606f);

		static readonly FieldInfo SlotReparent = Reflect.Field(typeof(CameraSlot), "_reparentTarget");
		static readonly PortReticles Reticles = new PortReticles(typeof(Station).Assembly, T90MMod.FriendlyName);

		internal class Fitted : RemoteMg.Station
		{
			internal AimablePlatform Drum;
			internal WeaponSystem Weapon;
			internal FireControlSystem Fcs, MainFcs;
			internal UsableOptic Tv, Thermal, DublTv, DublThermal, Pdt;
			internal Transform Turret;
			internal bool LowRate;
			internal float Rate => LowRate ? RateLow : RateHigh;
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

		static readonly Dictionary<WeaponSystem, Fitted> ByWeapon = new Dictionary<WeaponSystem, Fitted>();
		static bool _logged;

		internal static void Apply(Vehicle vehicle)
		{
			var root = vehicle.transform;
			var rig = T90MMod.Rig;
			RigMap.Mount M(string g) => rig?.Mounts?.FirstOrDefault(x => x.Group == g);
			var drumM = M("RWS"); var cradleM = M("RWS_CRADLE");
			if (drumM == null || cradleM == null) { MelonLogger.Warning($"{Tag}: the bundle has no remote-station mounts (old bundle?); the station stays fixed and unarmed."); return; }
			RemoteMg.SetLogTag($"{Tag} commander's station: ");
			var d = Donor.Get(Tag);
			if (d == null || d.CommanderOptic == null) { MelonLogger.Warning($"{Tag}: the T-64B station donor {(d == null ? "was not found" : "has no Commander's Optic")}; the station stays fixed and unarmed."); return; }
			var turret = root.Find("---MESH---/HULL/TURRET");
			var wm = vehicle.GetComponent<WeaponsManager>();
			if (turret == null || wm?.Weapons == null || wm.Weapons.Length < 2) { MelonLogger.Warning($"{Tag}: no turret bone or main gun / coax rows; the station stays fixed."); return; }
			var mainFcs = wm.Weapons[0].FCS;
			Vector3 W(Vector3 prefab) => root.TransformPoint(prefab);
			var problems = new List<string>();

			var axisT = new GameObject("T90M RWS axis").transform;
			axisT.SetParent(turret, false);
			axisT.SetPositionAndRotation(W(drumM.Pivot), root.rotation * Quaternion.FromToRotation(Vector3.up, drumM.Axis.normalized));
			var drumT = new GameObject("T90M RWS drum").transform;
			drumT.SetParent(axisT, false);
			var frame = new GameObject("T90M RWS").transform;
			frame.SetParent(drumT, false);
			frame.SetPositionAndRotation(W(drumM.Pivot), root.rotation);
			var cradleT = new GameObject("T90M RWS cradle").transform;
			cradleT.SetParent(frame, false);
			cradleT.SetPositionAndRotation(W(cradleM.Pivot), root.rotation * Quaternion.FromToRotation(Vector3.right, cradleM.Axis.normalized));
			Transform Carrier(string g) => g == drumM.Group ? frame : g == cradleM.Group ? cradleT : null;

			// The model has the Kord pitched 1.17 deg nose down with the turret, GHPC lays the cradle level on the aim.
			float pitch = rig.HasAxes ? rig.PitchDeg : 0f;
			void Level(Transform t) => t.RotateAround(cradleT.position, cradleT.right, -pitch);
			var visOf = new Dictionary<string, Transform>();
			foreach (var m in rig.Mounts)
			{
				var vis = turret.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "T90M_" + m.Group)
					?? turret.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == m.Group);
				var to = m.Axis != Vector3.zero ? Carrier(m.Group) : Carrier(m.Rides);
				if (vis == null || to == null) { problems.Add($"part '{m.Group}' {(vis == null ? "not in the bundle" : "has no drive")}"); continue; }
				vis.name = "T90M_" + m.Group;
				visOf[m.Group] = vis;
				vis.SetParent(to, true);
				if (to == cradleT) Level(vis);
			}

			Transform Group(string name, Transform follow)
			{
				var g = new GameObject(name) { layer = 8 };
				g.SetActive(false);
				g.transform.SetParent(root, false);
				g.transform.SetPositionAndRotation(follow.position, follow.rotation);
				var f = g.AddComponent<LateFollow>();
				f.FollowTarget = follow;
				f.ForceToRoot = true;
				foreach (var c in new[] { "ARMOR", "AAR" }) { var t = new GameObject(c) { layer = 8 }.transform; t.SetParent(g.transform, false); }
				g.SetActive(true);
				return g.transform;
			}
			var drumGroup = Group("T90M RWS COLLIDERS", frame);
			var cradleGroup = Group("T90M RWS_CRADLE COLLIDERS", cradleT);
			if (T90MMod.Armor != null && T90MMod.ArmourValues != null)
			{
				string PathOf(Transform t) { var s = t.name; for (var p = t.parent; p != null && p != root; p = p.parent) s = p.name + "/" + s; return s; }
				var left = new List<string>();
				DmArmourApply.Apply(vehicle, T90MMod.Armor, T90MMod.ArmourValues, Tag, left, null,
					new[] { (drumM.Group, PathOf(frame), "RWS COLLIDERS"), (cradleM.Group, PathOf(cradleT), "RWS_CRADLE COLLIDERS") });
				foreach (Transform piece in cradleGroup.Find("ARMOR")) Level(piece);
				RaycastColliderGroup.AddNewColliderSetToParentGroup(drumGroup);
				RaycastColliderGroup.AddNewColliderSetToParentGroup(cradleGroup);
				if (left.Count > 0) problems.Add("armour: " + string.Join("; ", left));
			}

			var holder = new GameObject("T90M RWS (setup)");
			holder.SetActive(false);
			holder.transform.SetParent(root, false);
			var gun = RemoteMg.CloneGun(d, holder.transform, new RemoteMg.GunSettings { Name = KordName, DamageName = "6P49MT Kord", BeltRounds = BeltRounds, Belts = Belts, RoundsPerMinute = RateHigh, B32PerTracer = B32PerTracer }, Tag);
			if (gun == null) { Object.Destroy(holder); return; }
			int removed = gun.Removed, scrubbed = gun.Scrubbed;
			// Ends up last under the root, GetComponentInChildren<FireControlSystem>() elsewhere has to find the gunner's.
			var (fcsGo, fcs, tvGo, r2, s2) = RemoteMg.CloneFcs(d, holder.transform, "T90M Kord fire control");
			removed += r2; scrubbed += s2;
			var head = new GameObject("T90M PK PAN head").transform;
			head.SetParent(frame, false);
			head.SetPositionAndRotation(W(Eye), root.rotation);
			var reference = new GameObject("T90M Kord aim reference").transform;
			reference.SetParent(frame, false);
			reference.SetPositionAndRotation(W(Eye), root.rotation);
			fcs.ReferenceTransform = reference;
			fcs.LaserOrigin = null;
			fcs.LinkedWeaponSystems = new[] { gun.Weapon };
			var scripts = new GameObject("---T90M RWS SCRIPTS---");
			scripts.transform.SetParent(root, false);
			var drum = RemoteMg.Platform(scripts.transform, "RWS drive", d.CupolaDrive, drumT, EnumHelpers.Axis3.Y, Vector2.zero, DrumSpeed, null);
			var cradle = RemoteMg.Platform(scripts.transform, "RWS elevation drive", d.ElevationDrive, cradleT, EnumHelpers.Axis3.X, Elevation, CradleSpeed, null);
			// GHPC ignores a goal more than 90 deg off, the drum froze on a quick slew behind.
			drum.ProtectFromBadProjection = false;
			cradle.ProtectFromBadProjection = false;
			drum.Stabilized = cradle.Stabilized = true;
			fcs.Mounts = new[] { drum, cradle };

			var cmdHead = root.GetComponentsInChildren<CameraSlot>(true).FirstOrDefault(s => s.name == "commander head");
			var tv = tvGo.GetComponent<UsableOptic>();
			var th = RemoteMg.CloneOptic(d, d.CommanderOptic, holder.transform, ref removed, ref scrubbed);
			var dtv = RemoteMg.CloneOptic(d, d.CommanderOptic, holder.transform, ref removed, ref scrubbed);
			var dth = RemoteMg.CloneOptic(d, d.CommanderOptic, holder.transform, ref removed, ref scrubbed);
			// Onto the head while inactive. ThermalSight adds a post volume beside the optic's post, and left in the holder it
			// dies with it and EnablePost throws on it.
			foreach (var o in new[] { tv, th, dtv, dth })
			{
				o.gameObject.SetActive(false);
				o.transform.SetParent(head, false);
				o.transform.localPosition = Vector3.zero;
				o.transform.localRotation = Quaternion.identity;
			}
			var gunnerWeapons = new[] { wm.Weapons[0].Weapon, wm.Weapons[1].Weapon };
			var mainRounds = GuidanceBeam.DayOptic(mainFcs)?.RegisteredAmmoTypes;
			SetupTv(tv, "PK PAN TV channel", fcs, head, frame, new[] { gun.Weapon }, new[] { gun.B32 }, cmdHead, true, problems);
			SetupTv(dtv, "PK PAN Dubl TV", mainFcs, head, frame, gunnerWeapons, mainRounds, cmdHead, false, problems);
			SetupThermal(tv, th, "PK PAN thermal channel", fcs, head, new[] { gun.Weapon }, new[] { gun.B32 }, cmdHead, problems);
			SetupThermal(dtv, dth, "PK PAN Dubl thermal", mainFcs, head, gunnerWeapons, mainRounds, cmdHead, problems);
			dtv.gameObject.AddComponent<ThermalSight.GateFollowsSight>().Init(mainFcs, dtv.slot);
			// In Дубль the gun's laser leaves from the panorama's window, else the spot sits off its centre close in.
			var panLaser = new GameObject("T90M PK PAN laser window (Dubl)").transform;
			panLaser.SetParent(head, false);
			panLaser.localPosition = Vector3.zero;
			panLaser.localRotation = Quaternion.identity;
			GunnerFcs.LaserAim.FromWhile(mainFcs, dtv.slot, panLaser, PanFcs.LaserMax);
			GunnerFcs.LaserAim.FromWhile(mainFcs, dth.slot, panLaser, PanFcs.LaserMax);

			gun.Go.transform.SetParent(cradleT, false);
			gun.Go.transform.SetPositionAndRotation(W(MuzzlePoint), cradleT.rotation);
			gun.Go.SetActive(true);
			fcsGo.transform.SetParent(scripts.transform, false);
			fcsGo.SetActive(true);
			// Night channels first, a day slot marks its linked night sight when it wakes.
			foreach (var o in new[] { th, dth, tv, dtv })
			{
				if (o.postAdditional != null && o.postAdditional.Any(v => v == null || v.transform.IsChildOf(holder.transform)))
				{
					problems.Add($"'{o.name}': a post volume was left in the setup holder; dropped");
					o.postAdditional = o.postAdditional.Where(v => v != null && !v.transform.IsChildOf(holder.transform)).ToArray();
				}
				o.gameObject.SetActive(true);
			}
			Object.Destroy(holder);
			gun.Weapon.SetCycleTime(gun.Cycle);

			KordBelt.Belt belt = null;
			if (visOf.TryGetValue("RWS_BOX", out var boxVis))
				try { belt = KordBelt.Fit(boxVis, cradleT, BeltSettings); }
				catch (System.Exception e) { MelonLogger.Error($"{Tag} Kord belt failed: " + e); }
			else problems.Add("no ammunition belt part (RWS_BOX): the belt stays still");
			if (belt != null)
			{
				belt.Attach(gun.Weapon, gun.Cycle);
				if (KordCases) KordBrass.Attach(vehicle, gun.Weapon, gun.Cycle, cradleT, cradleT.InverseTransformPoint(W(CaseChute)), CaseChuteDir, belt,
					new[] { frame, turret.Find("GUN"), turret, turret.parent });
			}

			var info = new WeaponSystemInfo { Name = KordName, Role = WeaponSystemRole.RoofGun, Weapon = gun.Weapon, FCS = fcs, PreAimWeapon = WeaponSystemRole.MainGun, PreAimAngleThreshold = 3f };
			var st = scripts.AddComponent<Fitted>();
			st.Info = info;
			st.Drum = drum; st.Weapon = gun.Weapon; st.Fcs = fcs; st.MainFcs = mainFcs;
			st.Tv = tv; st.Thermal = th; st.DublTv = dtv; st.DublThermal = dth;
			st.Turret = turret;
			RemoteMg.Register(vehicle, wm, info, new[] { drum, cradle }, new Component[] { drum, cradle, gun.Weapon, gun.Feed, fcs, tv.slot, th.slot, dtv.slot, dth.slot }, st.Sights);
			ByWeapon[gun.Weapon] = st;
			scripts.AddComponent<Link>().Init(st);
			foreach (var dead in ByWeapon.Keys.Where(k => k == null).ToList()) ByWeapon.Remove(dead);

			foreach (var p in problems) MelonLogger.Warning($"{Tag} commander's station: {p}");
			if (_logged) return;
			_logged = true;
		}

		static readonly Vector3 MuzzleWt = new Vector3(0.2604f, 2.8311f, -0.920f);
		static Vector3 MuzzlePoint => Levelled(MuzzleWt);
		static Vector3 Levelled(Vector3 wt)
		{
			var m = T90MMod.Rig?.Mounts?.FirstOrDefault(x => x.Group == "RWS_CRADLE");
			if (m == null) return wt;
			float pitch = T90MMod.Rig.HasAxes ? T90MMod.Rig.PitchDeg : 0f;
			return m.Pivot + Quaternion.AngleAxis(-pitch, Vector3.right) * (wt - m.Pivot);
		}

		static readonly Vector3 CaseChuteWt = new Vector3(0.300f, 2.800f, -1.455f);
		static Vector3 CaseChute => Levelled(CaseChuteWt);
		static readonly Vector3 CaseChuteDir = new Vector3(0.10f, -0.08f, 1f);
		static bool KordCases => T90MMod.KordCases == null || T90MMod.KordCases.Value;
		static readonly KordBelt.Settings BeltSettings = new KordBelt.Settings
		{
Prefix = "T90M_", MeshName = "T90M Kord", BeltMaterial = "t90m_mg_6p49_kord_ammo",
			SpareRounds = 24,
			MaxCases = BeltRounds * Belts,
		};

		static void SetupTv(UsableOptic o, string name, FireControlSystem fcs, Transform head, Transform level, WeaponSystem[] weapons, AmmoCodexScriptable[] rounds,
			CameraSlot onDestroy, bool ownFcs, List<string> problems)
		{
			o.name = name;
			var s = o.slot;
			if (s.gameObject != o.gameObject) s.gameObject.name = name;
			SlotReparent.SetValue(s, head);
			o.FCS = fcs;
			o.RegisterAtStart = ownFcs;
			o.Mounts = new AimablePlatform[0];
			o.LevelTransform = level;
			if (rounds != null) o.RegisteredAmmoTypes = rounds;
			s.ExclusiveWeapons = weapons;
			s.SlotToUseOnDestroy = onDestroy;
			s.IsExterior = false;
			s.DefaultFov = TvWide;
			s.OtherFovs = new[] { TvNarrow };
			s.AllowFreeZoom = false;
			s.fovAspect = false;
			s.VisionType = NightVisionType.None;
			s.NightSightAtNightOnly = false;
			s.ZoomInAudioEvent = "event:/Effects/Optic/Optic_Zoom_In";
			s.ZoomOutAudioEvent = "event:/Effects/Optic/Optic_Zoom_Out";
			SightMotion.Stabilised(s);
			if (ownFcs) GunnerFcs.Apply(o, PanFcs);
			else
			{
				o.Alignment = OpticAlignment.BoresightStabilized;
				o.RotateElevation = true;
				o.RotateAzimuth = true;
				o.CantCorrect = true;
				o.ForceHorizontalReticleAlign = true;
				o.LocalElevationLimits = PanFcs.LineOfSightLimits;
				GunnerFcs.AimClamp.Register(fcs, o);
			}
			if (o.reticleMesh != null) o.reticleMesh.gameObject.SetActive(false);
			MonoPicture(o, name);
			o.gameObject.AddComponent<ThermalSight.NoScopeMask>().Init(s);
			var wide = ReticleBuild.BuildOverlay(Reticles.Spec("pk_pan_tv_wide.jsonc"), o.transform, TvWide, problems);
			var narrow = ReticleBuild.BuildOverlay(Reticles.Spec("pk_pan_tv_narrow.jsonc"), o.transform, TvNarrow, problems);
			var row = Row(o, problems);
			ZoomReticles.Attach(o, null, new[] { new[] { wide.Root }, new[] { narrow.Root, row.Root } });
		}

		static void SetupThermal(UsableOptic day, UsableOptic o, string name, FireControlSystem fcs, Transform head, WeaponSystem[] weapons, AmmoCodexScriptable[] rounds,
			CameraSlot onDestroy, List<string> problems)
		{
			o.name = name;
			var s = o.slot;
			if (s.gameObject != o.gameObject) s.gameObject.name = name;
			SlotReparent.SetValue(s, head);
			o.FCS = fcs;
			o.RegisterAtStart = false;
			o.Mounts = new AimablePlatform[0];
			if (rounds != null) o.RegisteredAmmoTypes = rounds;
			s.ExclusiveWeapons = weapons;
			s.SlotToUseOnDestroy = onDestroy;
			s.IsExterior = false;
			s.LinkedNightSight = null;
			day.slot.LinkedNightSight = s;
			string bound = "";
			ThermalSight.Apply(day, o, new ThermalSight.Settings
			{
				Tag = Tag,
				Name = name,
				WideFov = ThermalWide,
				OtherFovs = new[] { ThermalNarrow, ThermalZoom },
				ElectronicFov = ThermalZoom,
				Width = ThermalWidth,
				Height = ThermalHeight,
				ElevationLimits = PanFcs.LineOfSightLimits,
				Reticle = x => bound = ThermalMarks(x, problems),
				Monitor = true,
				Crt = false,
			});
		}

		static string ThermalMarks(UsableOptic o, List<string> problems)
		{
			if (o.reticleMesh != null) o.reticleMesh.gameObject.SetActive(false);
			var wide = ReticleBuild.BuildOverlay(Reticles.Spec("sosna_u_thermal_wide.jsonc"), o.transform, ThermalWide, problems);
			var narrow = ReticleBuild.BuildOverlay(Reticles.Spec("sosna_u_thermal_narrow.jsonc"), o.transform, ThermalNarrow, problems);
			var zoom = ReticleBuild.BuildOverlay(Reticles.Spec("sosna_u_thermal_zoom.jsonc"), o.transform, ThermalZoom, problems);
			var row = Row(o, problems);
			ZoomReticles.Attach(o, null, new[] { new[] { wide.Root }, new[] { narrow.Root, row.Root }, new[] { zoom.Root, row.Root } });
			return "the «Сосна-У» thermal marks";
		}

		static ReticleBuild.Overlay Row(UsableOptic o, List<string> problems)
		{
			var spec = Reticles.Spec("sosna_u_thermal_row.jsonc");
			var row = ReticleBuild.BuildOverlay(spec, o.transform, (float)spec.Fov, problems);
			string mono = spec.Num("Mono").ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
			SightReadout.Attach(o, row, new SightReadout.Settings
			{
				Range = "range", RangePrefix = $"<b><mspace={mono}em>", RangeSuffix = "</mspace></b>", RangeQuantize = 5,
				Ready = "ready",
				Ammo = "ammo", AmmoText = a => $"<b>{Sights.ThermalLetter(a)}</b>",
			}, problems);
			return row;
		}

		static PostProcessProfile _mono;
		internal static void MonoPicture(UsableOptic o, string name)
		{
			if (_mono == null)
			{
				_mono = ScriptableObject.CreateInstance<PostProcessProfile>();
				_mono.name = $"{Tag} black-and-white TV";
				var cg = _mono.AddSettings<ColorGrading>();
				cg.enabled.Override(true);
				cg.saturation.Override(-100f);
				Object.DontDestroyOnLoad(_mono);
			}
			var go = new GameObject(name + " post (black-and-white)") { layer = o.post != null ? o.post.gameObject.layer : 16 };
			go.transform.SetParent(o.transform, false);
			var v = go.AddComponent<PostProcessVolume>();
			v.isGlobal = true;
			v.weight = 1f;
			v.priority = 100f;
			v.sharedProfile = _mono;
			v.enabled = false;
			o.postAdditional = (o.postAdditional ?? new PostProcessVolume[0]).Where(x => x != null).Append(v).ToArray();
		}

		internal static void LateSetup(Vehicle vehicle)
		{
			var st = vehicle.GetComponentInChildren<Fitted>(true);
			if (st == null) return;
			var wm = vehicle.GetComponent<WeaponsManager>();
			var gunner = new[] { wm.Weapons[0].Weapon, wm.Weapons[1].Weapon };
			var day = GuidanceBeam.DayOptic(st.MainFcs);
			var slots = new[] { day?.slot, day?.slot.LinkedNightSight, st.Pdt?.slot }.Where(s => s != null).ToArray();
			foreach (var s in slots) s.ExclusiveWeapons = gunner;
			// The T-64B's NSVT starts unstabilised, and BoresightStabilized only follows the aim with the stabiliser on.
			st.Fcs.SetStabsActive(true);
			RemoteMg.LateSetup(vehicle);
		}

		[DefaultExecutionOrder(-40)]
		internal class Link : MonoBehaviour
		{
			Fitted _st;
			Vector3 _lasedLine;
			internal void Init(Fitted st)
			{
				_st = st;
				if (st.Fcs != null) st.Fcs.LaserFired += () => _lasedLine = st.Fcs.AimWorldVector;
			}

			internal static bool HandOverHeld()
			{
				try { return InputUtil.MainPlayer != null && InputUtil.MainPlayer.GetButton("Aim Override"); }
				catch { return false; }
			}

			void Update()
			{
				if (_st == null || _st.Fcs == null || _st.MainFcs == null) return;
				bool held = _st.PanoramaView && HandOverHeld() && IsPlayers(_st);
				var panTracker = TargetTracker.For(_st.Fcs);
				if (_heldLast && !held && panTracker != null && panTracker.State == TargetTracker.Mode.Auto)
					TargetTracker.For(_st.MainFcs)?.HandOver(panTracker.Target, panTracker.TrackedPoint, panTracker.ImageHalf);
				_heldLast = held;
				if (_st.DublView) _st.Fcs.SetAimVector(_st.MainFcs.AimWorldVector, true);
				else if (_st.PanoramaView && HandOverHeld() && IsPlayers(_st))
				{
					var from = _st.Fcs.ReferenceTransform.position;
					var aim = _st.Fcs.AimWorldVector;
					// A range lased on another line puts the gun on the wrong point, the sights stand 2 m apart.
					bool lasedHere = _st.Fcs.CurrentRange > 0f && !_st.Fcs.RangeInvalid && _lasedLine != Vector3.zero && Vector3.Angle(aim, _lasedLine) < 0.5f;
					if (lasedHere) _st.MainFcs.SetAimWorldPosition(from + aim.normalized * _st.Fcs.CurrentRange);
					else _st.MainFcs.SetAimVector(aim, true);
				}
				else if (!_st.PanoramaView && IsPlayers(_st) && _st.Turret != null && (panTracker == null || panTracker.State == TargetTracker.Mode.Manual))
				{
					var aim = _st.Fcs.AimWorldVector;
					if (!_follow || Vector3.Angle(aim, _setLast) > 0.01f) { _onTurret = _st.Turret.InverseTransformDirection(aim); _follow = true; }
					_setLast = _st.Turret.TransformDirection(_onTurret);
					_st.Fcs.SetAimVector(_setLast, true);
					return;
				}
				_follow = false;
			}

			bool _follow, _heldLast;
			Vector3 _onTurret;
			Vector3 _setLast;
		}

		static bool IsPlayers(Fitted st) => PlayerInput.Instance != null && (object)PlayerInput.Instance.CurrentPlayerUnit == st.GetComponentInParent<Vehicle>();

		// GHPC's Aim Override swings the active weapon at the AI's target. In the panorama that key hands the line to the gunner instead.
		[HarmonyPatch(typeof(CrewBrainWeaponsModule), nameof(CrewBrainWeaponsModule.SetAimVector))]
		static class AimOverridePatch
		{
			static bool Prefix(CrewBrainWeaponsModule __instance, WeaponSystemRole weaponSystemRole)
			{
				try
				{
					if (weaponSystemRole != WeaponSystemRole.RoofGun || !Link.HandOverHeld()) return true;
					var input = PlayerInput.Instance;
					if (input == null || (object)input.CurrentPlayerCrewBrain?.WeaponsModule != __instance) return true;
					var unit = input.CurrentPlayerUnit as Vehicle;
					var st = unit != null ? unit.GetComponentInChildren<Fitted>(true) : null;
					return st == null || !st.PanoramaView;
				}
				catch (System.Exception e) { PatchGuard.Failed("Kord MT aim override hand-over", e); return true; }
			}
		}

		[HarmonyPatch(typeof(WeaponSystem), nameof(WeaponSystem.AbleToFire), MethodType.Getter)]
		static class BlindSectorPatch
		{
			static void Postfix(WeaponSystem __instance, ref bool __result)
			{
				try
				{
					if (!__result || !ByWeapon.TryGetValue(__instance, out var st) || st == null || !st.InBlindSector) return;
					__result = false;
				}
				catch (System.Exception e) { PatchGuard.Failed("Kord MT blind sector", e); }
			}
		}

		[HarmonyPatch(typeof(PlayerInput), "AddSmokeButtons")]
		static class RateMenuPatch
		{
			static readonly MethodInfo Add = Reflect.Method(typeof(PlayerInput), "AddMenuButton", new[] { typeof(string), typeof(UnityAction) });

			static void Postfix(PlayerInput __instance)
			{
				try
				{
					var unit = __instance.CurrentPlayerUnit as Vehicle;
					var st = unit != null ? unit.GetComponentInChildren<Fitted>(true) : null;
					if (st == null || st.Weapon == null || Add == null) return;
					bool low = !st.LowRate;
					Add.Invoke(__instance, new object[] { $"Kord MT/Rate of fire: {(low ? $"low, {RateLow:0}" : $"high, {RateHigh:0}")} rds/min (now {st.Rate:0})",
						(UnityAction)(() => { st.SetLowRate(low); }) });
				}
				catch (System.Exception e) { PatchGuard.Failed("Kord MT rate menu", e); }
			}
		}
	}
}
