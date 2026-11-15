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
	// Sosna-U's channels and the key, handed to TargetTracker.
	internal static class Tracker
	{
		internal const KeyCode DefaultKey = KeyCode.Mouse2;

		static bool _badKeyWarned;
		static KeyCode Key()
		{
			var keyName = T90MMod.TrackerKey?.Value;
			if (string.IsNullOrWhiteSpace(keyName)) return KeyCode.None;
			if (Enum.TryParse(keyName.Trim(), true, out KeyCode key)) return key;
			if (!_badKeyWarned)
			{
				_badKeyWarned = true;
				MelonLogger.Warning($"{T90MMod.FriendlyName}: \"Target tracker key\" '{keyName}' is not a Unity key name; "
					+ "the tracker captures by lasing only.");
			}
			return KeyCode.None;
		}

		static bool GunnerLase(AmmoType a) => a != null && a.Caliber >= 20f
			&& (a.Guidance != AmmoType.GuidanceType.Unguided
				|| a.Category == AmmoType.AmmoCategory.Penetrator
				|| a.Category == AmmoType.AmmoCategory.ShapedCharge);
		static bool AnyLase(AmmoType a) => a != null;

		// 575 is the Catherine-XP's picture, 768 x 575; 576 is the render height and the TV standard.
		const int ThermalLines = 575;
		const int TvLines = 576;
		const float ReadyPixels = 8f;   // made up (no published figure). A T-72 to about 4 km in the narrow thermal

		static string Off() => T90MMod.TrackerOn != null && !T90MMod.TrackerOn.Value ? "target tracker off in the settings" : null;

		internal static void Apply(Vehicle vehicle)
		{
			var fcs = vehicle.GetComponentInChildren<FireControlSystem>();
			// The first FireControlSystem under the root is the gunner's. The Kord station adds its own after it.
			if (fcs == null || TargetTracker.For(fcs) != null) return;
			var day = GuidanceBeam.DayOptic(fcs);
			var nightSlot = day?.slot.LinkedNightSight;
			var night = nightSlot == null
				? null
				: nightSlot.PairedOptic
					?? vehicle.GetComponentsInChildren<UsableOptic>(true).FirstOrDefault(o => o.slot == nightSlot);
			var weapons = vehicle.GetComponent<WeaponsManager>();
			var station = vehicle.GetComponentInChildren<Commander.Station>(true);
			if (day == null || night == null || weapons?.Weapons == null || weapons.Weapons.Length == 0)
			{
				MelonLogger.Warning($"{T90MMod.FriendlyName}: no «Сосна-У» day / thermal channel; no target tracker.");
				return;
			}
			var problems = new List<string>();
			var marks = Reticles.Spec("asc_marks.jsonc");
			RegisterGunner(fcs, weapons, day, night, station, marks, problems);
			RegisterPanorama(station, marks, problems);
			foreach (var problem in problems) MelonLogger.Warning($"{T90MMod.FriendlyName} target tracker: {problem}");
		}

		// The gunner's tracker: the thermal and day channels, and Дубль's two when the commander's station is fitted.
		static void RegisterGunner(
			FireControlSystem fcs,
			WeaponsManager weapons,
			UsableOptic day,
			UsableOptic night,
			Commander.Station station,
			ReticleSpec marks,
			List<string> problems)
		{
			var readout = day.GetComponent<Sights.Marker>()?.DayReadout;
			bool dayLock = T90MMod.TrackerDayLock != null && T90MMod.TrackerDayLock.Value;
			var gunner = new List<TargetTracker.Channel>
			{
				new TargetTracker.Channel
				{
					Name = "Sosna-U thermal",
					Optic = night,
					Slot = day.slot.LinkedNightSight,
					CaptureSteps = new[] { 1, 2 },
					Lines = ThermalLines,
					Thermal = true,
					LaseCaptures = GunnerLase,
					ZoomResets = true,
				},
				// The real tracker only works off the thermal picture, so the day sight
				// judges a target by the narrow thermal field's lines.
				new TargetTracker.Channel
				{
					Name = "Sosna-U day",
					Optic = day,
					Slot = day.slot,
					CaptureSteps = dayLock ? new[] { 0, 1 } : new int[0],
					KeyCaptures = dayLock,
					LaseCaptures = dayLock ? GunnerLase : (Func<AmmoType, bool>)null,
					Lines = ThermalLines,
					PixelFov = Sights.ThermalNarrow,
					Thermal = true,
					SymbolLeft = readout != null && readout.Readouts.TryGetValue("lock left", out var left) ? left : null,
					SymbolRight = readout != null && readout.Readouts.TryGetValue("lock right", out var right) ? right : null,
				},
			};
			if (station != null)
			{
				gunner.Add(new TargetTracker.Channel
				{
					Name = "Dubl TV",
					Optic = station.DublTv,
					Slot = station.DublTv?.slot,
					CaptureSteps = new[] { 1 },
					Lines = TvLines,
					Group = "dubl",
					KeyCaptures = false,
					KeyResets = false,
					LaseCaptures = AnyLase,
					ZoomResets = true,
					AmmoResets = true,
				});
				gunner.Add(new TargetTracker.Channel
				{
					Name = "Dubl thermal",
					Optic = station.DublThermal,
					Slot = station.DublThermal?.slot,
					CaptureSteps = new[] { 1, 2 },
					Lines = ThermalLines,
					Thermal = true,
					Group = "dubl",
					KeyCaptures = false,
					KeyResets = false,
					LaseCaptures = AnyLase,
					ZoomResets = true,
					AmmoResets = true,
				});
			}
			TargetTracker.Register(fcs, new TargetTracker.Settings
			{
				Tag = T90MMod.FriendlyName + " Sosna-U",
				// The sight's line of sight slews at up to 5 deg/s.
				SlewRate = 5f,
				ReadyPixels = ReadyPixels,
				Key = Key,
				ResetOnFire = new[] { weapons.Weapons[0].Weapon },
				ResetWhen = () => Off()
					?? (station != null && station.PanoramaView && Commander.Link.HandOverHeld()
						? "target designation from the commander"
						: null),
			}, gunner.ToArray(), marks, problems);
		}

		// The commander's tracker on the panorama's TV and thermal channels.
		static void RegisterPanorama(Commander.Station station, ReticleSpec marks, List<string> problems)
		{
			if (station?.Fcs == null || station.Tv == null || station.Thermal == null) return;
			TargetTracker.Register(station.Fcs, new TargetTracker.Settings
			{
				Tag = T90MMod.FriendlyName + " PK PAN",
				// The panorama lays its line of sight at 10 deg/s at least in elevation; the tracker's own rate isn't
				// published, so that is taken (my reading).
				SlewRate = 10f,
				ReadyPixels = ReadyPixels,
				Key = Key,
				ResetWhen = () => Off() ?? (station.DublView ? "Дубль: the panorama lays the main gun" : null),
			}, new[]
			{
				new TargetTracker.Channel
				{
					Name = "PK PAN TV",
					Optic = station.Tv,
					Slot = station.Tv.slot,
					CaptureSteps = new[] { 1 },
					Lines = TvLines,
					Group = "pan",
					LaseCaptures = AnyLase,
					ZoomResets = true
				},
				new TargetTracker.Channel
				{
					Name = "PK PAN thermal",
					Optic = station.Thermal,
					Slot = station.Thermal.slot,
					CaptureSteps = new[] { 1, 2 },
					Lines = ThermalLines,
					Thermal = true,
					Group = "pan",
					LaseCaptures = AnyLase,
					ZoomResets = true
				},
			}, marks, problems);
		}
	}
}
