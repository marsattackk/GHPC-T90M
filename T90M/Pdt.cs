using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GHPC;
using GHPC.Camera;
using GHPC.Equipment.Optics;
using GHPC.Vehicle;
using GHPC.Weapons;
using MelonLoader;
using Reticle;
using T90M.Common;
using UnityEngine;

namespace T90M
{
	internal static class Pdt
	{
		static string Tag => T90MMod.FriendlyName;
		internal const string SightName = "PDT back-up sight";
		const float Fov = 3.0f;
		static readonly Vector2 LineOfSight = new Vector2(-9f, 17f);
		static readonly Vector3 Eye = new Vector3(-0.448f, 2.083f, -0.194f);

		static readonly FieldInfo SlotReparent = Reflect.Field(typeof(CameraSlot), "_reparentTarget");
		static readonly FieldInfo Smr = Reflect.Field(typeof(ReticleMesh), "SMR");
		static readonly PortReticles Reticles = new PortReticles(typeof(Pdt).Assembly, T90MMod.FriendlyName);

		internal static void Apply(Vehicle vehicle)
		{
			var root = vehicle.transform;
			var turret = root.Find("---MESH---/HULL/TURRET");
			var wm = vehicle.GetComponent<WeaponsManager>();
			var d = RemoteMg.Donor.Get(Tag);
			if (turret == null || wm?.Weapons == null || wm.Weapons.Length < 2 || d?.CommanderOptic == null) { MelonLogger.Warning($"{Tag}: no ПДТ (turret, gun rows or the T-64B optic donor missing)."); return; }
			var fcs = wm.Weapons[0].FCS;
			var day = GuidanceBeam.DayOptic(fcs);
			var problems = new List<string>();

			var head = new GameObject("T90M PDT head").transform;
			head.SetParent(turret, false);
			head.SetPositionAndRotation(root.TransformPoint(Eye), root.rotation);

			var holder = new GameObject("T90M PDT (setup)");
			holder.SetActive(false);
			holder.transform.SetParent(root, false);
			int removed = 0, scrubbed = 0;
			var o = RemoteMg.CloneOptic(d, d.CommanderOptic, holder.transform, ref removed, ref scrubbed);
			o.gameObject.SetActive(false);
			o.transform.SetParent(head, false);
			o.transform.localPosition = Vector3.zero;
			o.transform.localRotation = Quaternion.identity;
			o.name = SightName;
			var s = o.slot;
			if (s.gameObject != o.gameObject) s.gameObject.name = SightName;
			SlotReparent.SetValue(s, head);
			o.FCS = fcs;
			o.RegisterAtStart = false;
			o.Mounts = new AimablePlatform[0];
			o.Alignment = OpticAlignment.Boresight;
			o.RotateElevation = true;
			o.RotateAzimuth = true;
			o.CantCorrect = false;
			o.ForceHorizontalReticleAlign = false;
			o.LocalElevationLimits = LineOfSight;
			o.LevelTransform = turret;
			if (day != null) o.RegisteredAmmoTypes = day.RegisteredAmmoTypes;
			s.ExclusiveWeapons = new[] { wm.Weapons[0].Weapon, wm.Weapons[1].Weapon };
			s.SlotToUseOnDestroy = day != null ? day.slot : root.GetComponentsInChildren<CameraSlot>(true).FirstOrDefault(x => x.name == "commander head");
			s.IsExterior = false;
			s.DefaultFov = Fov;
			s.OtherFovs = new float[0];
			s.AllowFreeZoom = false;
			s.fovAspect = false;
			s.VisionType = NightVisionType.None;
			s.LinkedNightSight = null;
			SightMotion.Stabilised(s);
			// It looks along the bore, so the computer's lead swung the view ahead on a fast turn and snapped it back.
			GunnerFcs.LeadClamp.NoLeadWhile(fcs, s);

			const string file = "pdt.jsonc";
			var spec = Reticles.Spec(file);
			if (o.reticleMesh != null)
			{
				var host = o.reticleMesh.reticleSO;
				o.reticleMesh.reticleSO = Reticles.Build(file, host, host, null);
				Smr.SetValue(o.reticleMesh, null);
			}
			else problems.Add("the donor optic has no reticle mesh: no ПДТ marks");
			ReticleBuild.BuildOverlay(spec, o.transform, (float)spec.Fov, problems);
			Station.MonoPicture(o, SightName);
			o.gameObject.AddComponent<ThermalSight.NoScopeMask>().Init(s);
			o.gameObject.AddComponent<Mark>().Init(o, fcs);
			o.gameObject.AddComponent<ThermalSight.GateFollowsSight>().Init(fcs, s);

			o.gameObject.SetActive(true);
			if (o.reticleMesh != null) o.reticleMesh.Load();
			Object.Destroy(holder);

			var st = vehicle.GetComponentInChildren<Station.Fitted>(true);
			if (st != null) st.Pdt = o;
			foreach (var p in problems) MelonLogger.Warning($"{Tag} ПДТ: {p}");
		}

		// GHPC only lines up a reticle's Impact group on sights laid on the impact point, this one looks along the bore.
		[DefaultExecutionOrder(20)]
		internal class Mark : MonoBehaviour
		{
			UsableOptic _o;
			FireControlSystem _fcs;
			internal void Init(UsableOptic o, FireControlSystem fcs) { _o = o; _fcs = fcs; }

			void LateUpdate()
			{
				if (_o?.reticleMesh == null || _fcs == null || _o.slot == null || !_o.slot.IsActive) return;
				// At range 0 GHPC's impact point is the gun itself, the mark jumped 303 mrad. No range, mark on the bore.
				var p = _fcs.CurrentRange >= 1f ? _fcs.ImpactPointWorldPosition(false) : _o.slot.transform.position + _o.slot.transform.forward * 1000f;
				_o.reticleMesh.SetAlignment(ReticleTree.GroupBase.Alignment.Impact, p);
			}
		}
	}
}
