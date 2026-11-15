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
	// ПДТ, the gunner's back-up day sight. It looks along the bore, not at the impact point.
	internal static class Pdt
	{
		static string Tag => T90MMod.FriendlyName;
		internal const string SightName = "PDT back-up sight";
		const float Fov = 3.0f;
		static readonly Vector2 LineOfSight = new Vector2(-9f, 17f);
		static readonly Vector3 Eye = new Vector3(-0.448f, 2.083f, -0.194f);

		static readonly FieldInfo SlotReparent = Reflect.Field(typeof(CameraSlot), "_reparentTarget");

		internal static void Apply(Vehicle vehicle)
		{
			var root = vehicle.transform;
			var turret = root.Find("---MESH---/HULL/TURRET");
			var weapons = vehicle.GetComponent<WeaponsManager>();
			var donor = RemoteMg.Donor.Get(Tag);
			if (turret == null || weapons?.Weapons == null || weapons.Weapons.Length < 2 || donor?.CommanderOptic == null)
			{
				MelonLogger.Warning($"{Tag}: no ПДТ (turret, gun rows or the T-64B optic donor missing).");
				return;
			}
			var fcs = weapons.Weapons[0].FCS;
			var day = GuidanceBeam.DayOptic(fcs);
			var problems = new List<string>();

			var head = new GameObject("T90M PDT head").transform;
			head.SetParent(turret, false);
			head.SetPositionAndRotation(root.TransformPoint(Eye), root.rotation);

			var holder = new GameObject("T90M PDT (setup)");
			holder.SetActive(false);
			holder.transform.SetParent(root, false);
			var optic = Commander.CloneOptic(donor, holder.transform);
			optic.gameObject.SetActive(false);
			optic.transform.SetParent(head, false);
			optic.transform.localPosition = Vector3.zero;
			optic.transform.localRotation = Quaternion.identity;
			optic.name = SightName;
			var opticSlot = optic.slot;
			if (opticSlot.gameObject != optic.gameObject) opticSlot.gameObject.name = SightName;
			SlotReparent.SetValue(opticSlot, head);
			optic.FCS = fcs;
			optic.RegisterAtStart = false;
			optic.Mounts = new AimablePlatform[0];
			optic.Alignment = OpticAlignment.Boresight;
			optic.RotateElevation = true;
			optic.RotateAzimuth = true;
			optic.CantCorrect = false;
			optic.ForceHorizontalReticleAlign = false;
			optic.LocalElevationLimits = LineOfSight;
			optic.LevelTransform = turret;
			if (day != null) optic.RegisteredAmmoTypes = day.RegisteredAmmoTypes;
			opticSlot.ExclusiveWeapons = new[] { weapons.Weapons[0].Weapon, weapons.Weapons[1].Weapon };
			opticSlot.SlotToUseOnDestroy = day != null
				? day.slot
				: root.GetComponentsInChildren<CameraSlot>(true).FirstOrDefault(s => s.name == "commander head");
			opticSlot.IsExterior = false;
			opticSlot.DefaultFov = Fov;
			opticSlot.OtherFovs = new float[0];
			opticSlot.AllowFreeZoom = false;
			opticSlot.fovAspect = false;
			opticSlot.VisionType = NightVisionType.None;
			opticSlot.LinkedNightSight = null;
			SightMotion.Stabilised(opticSlot);
			// It looks along the bore, so the computer's lead swung the view ahead on a fast turn and snapped it back.
			GunnerFcs.LeadClamp.NoLeadWhile(fcs, opticSlot);

			const string file = "pdt.jsonc";
			var spec = Reticles.Spec(file);
			if (optic.reticleMesh != null)
			{
				var host = optic.reticleMesh.reticleSO;
				optic.reticleMesh.reticleSO = Reticles.Build(file, host, host, null);
				ReticleMeshes.ClearSmr(optic.reticleMesh);
			}
			else problems.Add("the donor optic has no reticle mesh: no ПДТ marks");
			ReticleBuild.BuildOverlay(spec, optic.transform, (float)spec.Fov, problems);
			Commander.MonoPicture(optic, SightName);
			optic.gameObject.AddComponent<ThermalSight.NoScopeMask>().Init(opticSlot);
			optic.gameObject.AddComponent<Mark>().Init(optic, fcs);
			optic.gameObject.AddComponent<ThermalSight.GateFollowsSight>().Init(fcs, opticSlot);

			optic.gameObject.SetActive(true);
			if (optic.reticleMesh != null) optic.reticleMesh.Load();
			Object.Destroy(holder);

			var station = vehicle.GetComponentInChildren<Commander.Station>(true);
			if (station != null) station.Pdt = optic;
			foreach (var problem in problems) MelonLogger.Warning($"{Tag} ПДТ: {problem}");
		}

		// GHPC only lines up a reticle's Impact group on sights laid on the impact point. This one looks along the bore.
		[DefaultExecutionOrder(20)]
		internal class Mark : MonoBehaviour
		{
			UsableOptic _o;
			FireControlSystem _fcs;
			internal void Init(UsableOptic o, FireControlSystem fcs)
			{
				_o = o;
				_fcs = fcs;
			}

			void LateUpdate()
			{
				if (_o?.reticleMesh == null || _fcs == null || _o.slot == null || !_o.slot.IsActive) return;
				// At range 0 GHPC's impact point is the gun itself and the mark jumped 303 mrad. No range, mark on the bore.
				var impact = _fcs.CurrentRange >= 1f
					? _fcs.ImpactPointWorldPosition(false)
					: _o.slot.transform.position + _o.slot.transform.forward * 1000f;
				_o.reticleMesh.SetAlignment(ReticleTree.GroupBase.Alignment.Impact, impact);
			}
		}
	}
}
