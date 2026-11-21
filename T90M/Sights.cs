using System.Collections.Generic;
using System.Linq;
using GHPC.Camera;
using GHPC.Equipment.Optics;
using GHPC.Vehicle;
using GHPC.Weapons;
using MelonLoader;
using T90M.Common;
using UnityEngine;

namespace T90M
{
	// The gunner's Sosna-U, day and thermal.
	internal static class Sights
	{
		// The «Сосна-У»'s figures here are its 2014 manual's, for the sight in the T-72B3.
		// The day channel's fields: 4 deg at 12x and 12 deg at 4x.
		const float DayWide = 12f, DayNarrow = 4f;

		static readonly GunnerFcs.Settings Settings = new GunnerFcs.Settings
		{
			Tag = T90MMod.FriendlyName,
			// The laser's range, 300 to 10000 m.
			LaserMin = 300f,
			LaserMax = 10000f,
			// The line of sight's elevation, -10 to +30 deg.
			LineOfSightLimits = new Vector2(-10f, 30f),
			// The line of sight's travel in traverse, 7.5 deg, read as either side (my reading).
			LineOfSightAzimuth = 7.5f,
			// Lead kept inside the head's 7.5 deg travel. Unclamped, the gun ran off ahead after a long lase and dragged the view with it.
			LeadLimit = 7.5f,
			// The step of a hand-entered range isn't published: the T-90A's is used (my reading).
			RangeStep = 100,
			// The range display counts in 10 m.
			DisplayRangeIncrement = 10,
			FireGateAngle = 0.1f,   // the Kalina's isn't published: GHPC's T-80B (1A33) value, as on the T-90A
			FireWhenLaid = () => T90MMod.FireWhenLaid == null || T90MMod.FireWhenLaid.Value,
			GateStaysOnGun = true,
			ManualMode = false,
		};

		// The thermal fields' heights: 9 x 6.75°, 3 x 2.25° and 1.5 x 1.12° with the electronic zoom (the «Сосна-У»
		// manual, and Thales gives the Catherine-XP's own 9 x 6.75° and 3 x 2.25° with a 2 x 2 zoom).
		internal const float ThermalWide = 6.75f;
		internal const float ThermalNarrow = 2.25f;
		internal const float ThermalZoom = 1.125f;
		// The render height, 576 as in the TV standard (the Catherine-XP's own picture is 768 x 575, which
		// the tracker counts in lines).
		internal const int ThermalHeight = 576;
		// The render fills the 16:9 screen. 1024 across gives 768 over the 4:3 picture in the middle.
		internal const int ThermalWidth = 1024;
		// The interval between two laser measurements, 2 s at most.
		const float LaserRecharge = 2f;

		// The laser window on the model: my own placement.
		static readonly Vector3 LaserWindowPoint = new Vector3(-0.602f, 2.199f, -0.576f);
		const string LaserWindowName = "T90M Sosna-U laser window";

		internal static void Apply(Vehicle vehicle)
		{
			var fcs = vehicle.GetComponentInChildren<FireControlSystem>();
			var day = GuidanceBeam.DayOptic(fcs);
			if (day == null)
			{
				MelonLogger.Warning($"{T90MMod.FriendlyName}: no gunner day sight found; the T-72M1's sights kept.");
				return;
			}
			if (day.GetComponent<Marker>() != null) return;
			day.gameObject.AddComponent<Marker>();
			var problems = new List<string>();
			ApplyDay(day, problems);
			GunnerFcs.Apply(day, Settings);
			GunnerFcs.LaserAim.Register(fcs, day, Settings);
			var laser = fcs.LaserOrigin != null && fcs.LaserOrigin.name == LaserWindowName ? fcs.LaserOrigin : null;
			GunnerFcs.HeadView(
				T90MMod.FriendlyName,
				laser,
				day.slot.transform,
				day.slot.LinkedNightSight != null ? day.slot.LinkedNightSight.transform : null,
				fcs.ReferenceTransform);
			GunnerFcs.Parallax.Register(fcs);
			ApplyThermal(vehicle, day, problems);
			foreach (var problem in problems) MelonLogger.Warning($"{T90MMod.FriendlyName} sight: {problem}");
		}

		internal static void PlaceLaser(Vehicle vehicle)
		{
			var root = vehicle.transform;
			var fcs = vehicle.GetComponentInChildren<FireControlSystem>();
			var window = GunnerFcs.LaserWindow(root, fcs, root.Find("---MESH---/HULL/TURRET"), LaserWindowPoint, LaserWindowName);
			if (window == null)
			{
				MelonLogger.Warning($"{T90MMod.FriendlyName}: no fire control or turret; the laser stays at the T-72M1's sight.");
			}
		}

		// The gunner's periscope on the model: my own placement.
		static readonly Vector3 PeriscopePoint = new Vector3(-0.392f, 2.200f, -0.770f);

		internal static void PlacePeriscope(Vehicle vehicle)
		{
			var root = vehicle.transform;
			var slot = root
				.GetComponentsInChildren<CameraSlot>(true)
				.FirstOrDefault(c => c.name == "Gunner's Periscope Camera");
			if (slot == null)
			{
				MelonLogger.Warning($"{T90MMod.FriendlyName}: gunner's periscope slot not found; left as it is.");
				return;
			}
			slot.transform.position = root.TransformPoint(PeriscopePoint);
			slot.DirectAttachOnly = true;   // Unit skips these in Cycle View but the slot stays for anything attached to it
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
			ReticleMeshes.ClearSmr(day.reticleMesh);
			day.reticleMesh.Load();
			day.slot.DefaultFov = DayWide;
			day.slot.OtherFovs = new[] { DayNarrow };
			day.slot.AllowFreeZoom = false;
			day.slot.fovAspect = true;
			// The T-72M1's sight has no zoom and no zoom sound either. These are the T-80B's.
			day.slot.ZoomInAudioEvent = "event:/Effects/Optic/Optic_Zoom_In";
			day.slot.ZoomOutAudioEvent = "event:/Effects/Optic/Optic_Zoom_Out";
			SightMotion.Stabilised(day.slot);
			var overlay = ReticleBuild.BuildOverlay(spec, day.transform, (float)spec.Fov, problems);
			var marker = day.GetComponent<Marker>();
			if (marker != null) marker.DayReadout = overlay;
			string mono = Mono(spec, "Mono"), digits = Mono(spec, "MonoDigits");
			SightReadout.Attach(day, overlay, new SightReadout.Settings
			{
				Range = "range",
				RangePrefix = $"<mspace={digits}em>",
				RangeSuffix = "</mspace>",
				Ready = "ready",
				Ammo = "ammo",
				AmmoText = a => $"<mspace={mono}em>{DayCode(a)}</mspace>",
				LaserLamp = "laser",
				LaserRecharge = LaserRecharge,
			}, problems);
			string lacks = ReticleBuild.Missing(overlay.Readouts.TryGetValue("ammo", out var text) ? text.font : null, "APHFGMК");
			if (lacks.Length > 0) problems.Add($"day readout font lacks '{lacks}'");
		}

		static void ApplyThermal(Vehicle vehicle, UsableOptic day, List<string> problems)
		{
			var slot = day.slot.LinkedNightSight;
			var night = slot == null
				? null
				: slot.PairedOptic ?? vehicle.GetComponentsInChildren<UsableOptic>(true).FirstOrDefault(o => o.slot == slot);
			if (night == null)
			{
				problems.Add("no gunner night sight on the base vehicle; no thermal channel");
				return;
			}
			bool applied = ThermalSight.Apply(day, night, new ThermalSight.Settings
			{
				Tag = T90MMod.FriendlyName,
				Name = "Sosna-U thermal",
				WideFov = ThermalWide,
				OtherFovs = new[] { ThermalNarrow, ThermalZoom },
				ElectronicFov = ThermalZoom,
				Width = ThermalWidth,
				Height = ThermalHeight,
				ElevationLimits = Settings.LineOfSightLimits,
				Reticle = o => ThermalMarks(o, problems),
				Monitor = true,
				Crt = false,
			});
			if (!applied) return;
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
				Range = "range",
				RangePrefix = $"<mspace={mono}em>",
				RangeSuffix = "</mspace>",
				Ready = "ready",
				Ammo = "ammo",
				AmmoText = a => ThermalLetter(a),
			}, problems);
			string lacks = ReticleBuild.Missing(row.Readouts.TryGetValue("ammo", out var text) ? text.font : null, "БОКУП");
			if (lacks.Length > 0) problems.Add($"thermal readout font lacks '{lacks}'");
			ZoomReticles.Attach(o, null, new[] { new[] { wide.Root }, new[] { narrow.Root, row.Root }, new[] { zoom.Root, row.Root } });
			return $"marks on the screen ({wideSpec.Screen.Count} / {narrowSpec.Screen.Count} / {zoomSpec.Screen.Count} items, "
				+ $"row {rowSpec.Screen.Count}), readout {bound}";
		}

		static string Mono(ReticleSpec spec, string name)
			=> spec.Num(name).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

		internal static string DayCode(AmmoType a) =>
			a.Caliber < 20f ? "MG"
			: a.Guidance != AmmoType.GuidanceType.Unguided ? "GA"
			: a.Category == AmmoType.AmmoCategory.Penetrator ? "AP"
			: a.Category == AmmoType.AmmoCategory.ShapedCharge ? "К"
			: "HF";

		internal static string ThermalLetter(AmmoType a) => SightReadout.Cyrillic(a);
	}
}
