using System;
using System.Collections.Generic;
using System.Linq;
using GHPC.Equipment.Optics;
using GHPC.Vehicle;
using GHPC.Weapons;
using MelonLoader;
using T90M.Common;
using UnityEngine;

namespace T90M
{
	internal static class Tracker
	{
		internal const KeyCode DefaultKey = KeyCode.Mouse2;
		static readonly PortReticles Reticles = new PortReticles(typeof(Tracker).Assembly, T90MMod.FriendlyName);

		static bool _badKeyWarned;
		static KeyCode Key()
		{
			var s = T90MMod.TrackerKey?.Value;
			if (string.IsNullOrWhiteSpace(s)) return KeyCode.None;
			if (Enum.TryParse(s.Trim(), true, out KeyCode k)) return k;
			if (!_badKeyWarned) { _badKeyWarned = true; MelonLogger.Warning($"{T90MMod.FriendlyName}: \"Target tracker key\" '{s}' is not a Unity key name; the tracker captures by lasing only."); }
			return KeyCode.None;
		}

		static bool GunnerLase(AmmoType a) => a != null && a.Caliber >= 20f
			&& (a.Guidance != AmmoType.GuidanceType.Unguided || a.Category == AmmoType.AmmoCategory.Penetrator || a.Category == AmmoType.AmmoCategory.ShapedCharge);
		static bool AnyLase(AmmoType a) => a != null;

		const int ThermalLines = 575, TvLines = 576;
		const float ThermalNarrowFov = 2.25f;
		const float ReadyPixels = 8f;   // made up, no figure published. A T-72 to about 4 km in the narrow thermal

		static string Off() => T90MMod.TrackerOn != null && !T90MMod.TrackerOn.Value ? "target tracker off in the settings" : null;

		internal static void Apply(Vehicle vehicle)
		{
			var fcs = vehicle.GetComponentInChildren<FireControlSystem>();
			// First FireControlSystem under the root is the gunner's, the Kord station's is added after it.
			if (fcs == null || TargetTracker.For(fcs) != null) return;
			var day = GuidanceBeam.DayOptic(fcs);
			var nightSlot = day?.slot.LinkedNightSight;
			var night = nightSlot == null ? null : nightSlot.PairedOptic ?? vehicle.GetComponentsInChildren<UsableOptic>(true).FirstOrDefault(o => o.slot == nightSlot);
			var wm = vehicle.GetComponent<WeaponsManager>();
			var st = vehicle.GetComponentInChildren<Station.Fitted>(true);
			if (day == null || night == null || wm?.Weapons == null || wm.Weapons.Length == 0) { MelonLogger.Warning($"{T90MMod.FriendlyName}: no «Сосна-У» day / thermal channel; no target tracker."); return; }
			var problems = new List<string>();
			var marks = Reticles.Spec("asc_marks.jsonc");
			var readout = day.GetComponent<Sights.Marker>()?.DayReadout;
			bool dayLock = T90MMod.TrackerDayLock != null && T90MMod.TrackerDayLock.Value;

			var gunner = new List<TargetTracker.Channel>
			{
				new TargetTracker.Channel
				{
					Name = "Sosna-U thermal", Optic = night, Slot = nightSlot, CaptureSteps = new[] { 1, 2 }, Lines = ThermalLines, Thermal = true,
					LaseCaptures = GunnerLase, ZoomResets = true,
				},
				// The real tracker only works off the thermal picture, so the day sight judges a target by the narrow thermal field's lines.
				new TargetTracker.Channel
				{
					Name = "Sosna-U day", Optic = day, Slot = day.slot,
					CaptureSteps = dayLock ? new[] { 0, 1 } : new int[0], KeyCaptures = dayLock, LaseCaptures = dayLock ? GunnerLase : (Func<AmmoType, bool>)null,
					Lines = ThermalLines, PixelFov = ThermalNarrowFov, Thermal = true,
					SymbolLeft = readout != null && readout.Readouts.TryGetValue("lock left", out var l) ? l : null,
					SymbolRight = readout != null && readout.Readouts.TryGetValue("lock right", out var r) ? r : null,
				},
			};
			if (st != null)
			{
				gunner.Add(new TargetTracker.Channel
				{
					Name = "Dubl TV", Optic = st.DublTv, Slot = st.DublTv?.slot, CaptureSteps = new[] { 1 }, Lines = TvLines, Group = "dubl",
					KeyCaptures = false, KeyResets = false, LaseCaptures = AnyLase, ZoomResets = true, AmmoResets = true,
				});
				gunner.Add(new TargetTracker.Channel
				{
					Name = "Dubl thermal", Optic = st.DublThermal, Slot = st.DublThermal?.slot, CaptureSteps = new[] { 1, 2 }, Lines = ThermalLines, Thermal = true, Group = "dubl",
					KeyCaptures = false, KeyResets = false, LaseCaptures = AnyLase, ZoomResets = true, AmmoResets = true,
				});
			}
			TargetTracker.Register(fcs, new TargetTracker.Settings
			{
				SlewRate = 5f,
				ReadyPixels = ReadyPixels,
				Key = Key,
				ResetOnFire = new[] { wm.Weapons[0].Weapon },
				ResetWhen = () => Off() ?? (st != null && st.PanoramaView && Station.Link.HandOverHeld() ? "target designation from the commander" : null),
			}, gunner.ToArray(), marks, problems);
			if (st?.Fcs != null && st.Tv != null && st.Thermal != null)
				TargetTracker.Register(st.Fcs, new TargetTracker.Settings
				{
					SlewRate = 10f,
					ReadyPixels = ReadyPixels,
					Key = Key,
					ResetWhen = () => Off() ?? (st.DublView ? "Дубль: the panorama lays the main gun" : null),
				}, new[]
				{
					new TargetTracker.Channel { Name = "PK PAN TV", Optic = st.Tv, Slot = st.Tv.slot, CaptureSteps = new[] { 1 }, Lines = TvLines, Group = "pan", LaseCaptures = AnyLase, ZoomResets = true },
					new TargetTracker.Channel { Name = "PK PAN thermal", Optic = st.Thermal, Slot = st.Thermal.slot, CaptureSteps = new[] { 1, 2 }, Lines = ThermalLines, Thermal = true, Group = "pan", LaseCaptures = AnyLase, ZoomResets = true },
				}, marks, problems);

			foreach (var p in problems) MelonLogger.Warning($"{T90MMod.FriendlyName} target tracker: {p}");
		}
	}
}
