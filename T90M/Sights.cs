using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
	internal static class Sights
	{
		static readonly PortReticles Reticles = new PortReticles(typeof(Sights).Assembly, T90MMod.FriendlyName);
		static readonly FieldInfo Smr = Reflect.Field(typeof(ReticleMesh), "SMR");

		const float DayWide = 12f, DayNarrow = 4f;

		static readonly GunnerFcs.Settings Fcs = new GunnerFcs.Settings
		{
			LaserMin = 300f,
			LaserMax = 10000f,
			LineOfSightLimits = new Vector2(-10f, 30f),
			LineOfSightAzimuth = 7.5f,
			// Lead kept inside the head's 7.5 deg travel. Unclamped, the gun ran off ahead after a long lase and dragged the view with it.
			LeadLimit = 7.5f,
			RangeStep = 100,
			DisplayRangeIncrement = 10,
			FireGateAngle = 0.1f,
			FireWhenLaid = () => T90MMod.FireWhenLaid == null || T90MMod.FireWhenLaid.Value,
			GateStaysOnGun = true,
			ManualMode = false,
		};

		const float ThermalWide = 6.75f, ThermalNarrow = 2.25f, ThermalZoom = 1.125f;
		const int ThermalHeight = 576;
		// The render fills the 16:9 screen, 1024 across gives 768 over the 4:3 picture in the middle.
		const int ThermalWidth = 1024;
		const float LaserRecharge = 2f;

		static readonly Vector3 LaserWindowPoint = new Vector3(-0.602f, 2.199f, -0.576f);
		const string LaserWindowName = "T90M Sosna-U laser window";

		internal static void Apply(Vehicle vehicle)
		{
			var fcs = vehicle.GetComponentInChildren<FireControlSystem>();
			var day = GuidanceBeam.DayOptic(fcs);
			if (day == null) { MelonLogger.Warning($"{T90MMod.FriendlyName}: no gunner day sight found; the T-72M1's sights kept."); return; }
			if (day.GetComponent<Marker>() != null) return;
			day.gameObject.AddComponent<Marker>();
			var problems = new List<string>();
			ApplyDay(day, problems);
			GunnerFcs.Apply(day, Fcs);
			GunnerFcs.LaserAim.Register(fcs, day, Fcs);
			var laser = fcs.LaserOrigin != null && fcs.LaserOrigin.name == LaserWindowName ? fcs.LaserOrigin : null;
			GunnerFcs.HeadView(T90MMod.FriendlyName, laser, day.slot.transform, day.slot.LinkedNightSight != null ? day.slot.LinkedNightSight.transform : null, fcs.ReferenceTransform);
			GunnerFcs.Parallax.Register(fcs);
			ApplyThermal(vehicle, day, problems);
			foreach (var p in problems) MelonLogger.Warning($"{T90MMod.FriendlyName} sight: {p}");
		}

		internal static void PlaceLaser(Vehicle vehicle)
		{
			var root = vehicle.transform;
			var fcs = vehicle.GetComponentInChildren<FireControlSystem>();
			var t = GunnerFcs.LaserWindow(root, fcs, root.Find("---MESH---/HULL/TURRET"), LaserWindowPoint, LaserWindowName);
			if (t == null) { MelonLogger.Warning($"{T90MMod.FriendlyName}: no fire control or turret; the laser stays at the T-72M1's sight."); }
		}

		static readonly Vector3 PeriscopePoint = new Vector3(-0.392f, 2.200f, -0.770f);

		internal static void PlacePeriscope(Vehicle vehicle)
		{
			var root = vehicle.transform;
			var slot = root.GetComponentsInChildren<CameraSlot>(true).FirstOrDefault(s => s.name == "Gunner's Periscope Camera");
			if (slot == null) { MelonLogger.Warning($"{T90MMod.FriendlyName}: gunner's periscope slot not found; left as it is."); return; }
			slot.transform.position = root.TransformPoint(PeriscopePoint);
			slot.DirectAttachOnly = true;   // Unit skips these in Cycle View, the slot stays for anything attached to it
		}

		internal class Marker : MonoBehaviour
		{
			internal ReticleBuild.Overlay DayReadout;
		}

		static void ApplyDay(UsableOptic day, List<string> problems)
		{
			const string file = "sosna_u_day.jsonc";
			var spec = Reticles.Spec(file);
			var host = day.reticleMesh.reticleSO;
			day.reticleMesh.reticleSO = Reticles.Build(file, host, host, null);
			Smr.SetValue(day.reticleMesh, null);
			day.reticleMesh.Load();
			day.slot.DefaultFov = DayWide;
			day.slot.OtherFovs = new[] { DayNarrow };
			day.slot.AllowFreeZoom = false;
			day.slot.fovAspect = true;
			// The T-72M1's sight has no zoom so no zoom sound either, these are the T-80B's.
			day.slot.ZoomInAudioEvent = "event:/Effects/Optic/Optic_Zoom_In";
			day.slot.ZoomOutAudioEvent = "event:/Effects/Optic/Optic_Zoom_Out";
			SightMotion.Stabilised(day.slot);
			var overlay = ReticleBuild.BuildOverlay(spec, day.transform, (float)spec.Fov, problems);
			var marker = day.GetComponent<Marker>();
			if (marker != null) marker.DayReadout = overlay;
			string mono = Mono(spec, "Mono"), digits = Mono(spec, "MonoDigits");
			SightReadout.Attach(day, overlay, new SightReadout.Settings
			{
				Range = "range", RangePrefix = $"<mspace={digits}em>", RangeSuffix = "</mspace>",
				Ready = "ready",
				Ammo = "ammo", AmmoText = a => $"<mspace={mono}em>{DayCode(a)}</mspace>",
				LaserLamp = "laser", LaserRecharge = LaserRecharge,
			}, problems);
			string lacks = ReticleBuild.Missing(overlay.Readouts.TryGetValue("ammo", out var t) ? t.font : null, "APHFGMК");
			if (lacks.Length > 0) problems.Add($"day readout font lacks '{lacks}'");
		}

		static void ApplyThermal(Vehicle vehicle, UsableOptic day, List<string> problems)
		{
			var slot = day.slot.LinkedNightSight;
			var night = slot == null ? null : slot.PairedOptic ?? vehicle.GetComponentsInChildren<UsableOptic>(true).FirstOrDefault(o => o.slot == slot);
			if (night == null) { problems.Add("no gunner night sight on the base vehicle; no thermal channel"); return; }
			bool ok = ThermalSight.Apply(day, night, new ThermalSight.Settings
			{
				Tag = T90MMod.FriendlyName,
				Name = "Sosna-U thermal",
				WideFov = ThermalWide,
				OtherFovs = new[] { ThermalNarrow, ThermalZoom },
				ElectronicFov = ThermalZoom,
				Width = ThermalWidth,
				Height = ThermalHeight,
				ElevationLimits = Fcs.LineOfSightLimits,
				Reticle = o => ThermalMarks(o, problems),
				Monitor = true,
				Crt = true,
			});
			if (!ok) return;
			ThermalSight.SetUnitNightVision(vehicle, slot);
		}

		static string ThermalMarks(UsableOptic o, List<string> problems)
		{
			// GHPC won't draw a dark reticle over a thermal picture, so every mark is a screen item.
			if (o.reticleMesh != null) o.reticleMesh.gameObject.SetActive(false);
			var wideSpec = Reticles.Spec("sosna_u_thermal_wide.jsonc");
			var narrowSpec = Reticles.Spec("sosna_u_thermal_narrow.jsonc");
			var zoomSpec = Reticles.Spec("sosna_u_thermal_zoom.jsonc");
			var rowSpec = Reticles.Spec("sosna_u_thermal_row.jsonc");
			var wide = ReticleBuild.BuildOverlay(wideSpec, o.transform, (float)wideSpec.Fov, problems);
			var narrow = ReticleBuild.BuildOverlay(narrowSpec, o.transform, (float)narrowSpec.Fov, problems);
			var zoom = ReticleBuild.BuildOverlay(zoomSpec, o.transform, (float)zoomSpec.Fov, problems);
			var row = ReticleBuild.BuildOverlay(rowSpec, o.transform, (float)rowSpec.Fov, problems);
			string mono = Mono(rowSpec, "Mono");
			string bound = SightReadout.Attach(o, row, new SightReadout.Settings
			{
				Range = "range", RangePrefix = $"<b><mspace={mono}em>", RangeSuffix = "</mspace></b>",
				Ready = "ready",
				Ammo = "ammo", AmmoText = a => $"<b>{ThermalLetter(a)}</b>",
			}, problems);
			string lacks = ReticleBuild.Missing(row.Readouts.TryGetValue("ammo", out var t) ? t.font : null, "БОКУП");
			if (lacks.Length > 0) problems.Add($"thermal readout font lacks '{lacks}'");
			ZoomReticles.Attach(o, null, new[] { new[] { wide.Root }, new[] { narrow.Root, row.Root }, new[] { zoom.Root, row.Root } });
			return $"marks on the screen ({wideSpec.Screen.Count} / {narrowSpec.Screen.Count} / {zoomSpec.Screen.Count} items, row {rowSpec.Screen.Count}), readout {bound}";
		}

		static string Mono(ReticleSpec spec, string name) => spec.Num(name).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

		internal static string DayCode(AmmoType a) =>
			a.Caliber < 20f ? "MG"
			: a.Guidance != AmmoType.GuidanceType.Unguided ? "GA"
			: a.Category == AmmoType.AmmoCategory.Penetrator ? "AP"
			: a.Category == AmmoType.AmmoCategory.ShapedCharge ? "К"
			: "HF";

		internal static string ThermalLetter(AmmoType a) =>
			a.Caliber < 20f ? "П"
			: a.Guidance != AmmoType.GuidanceType.Unguided ? "У"
			: a.Category == AmmoType.AmmoCategory.Penetrator ? "Б"
			: a.Category == AmmoType.AmmoCategory.ShapedCharge ? "К"
			: "О";
	}
}
