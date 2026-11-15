using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GHPC.Vehicle;
using MelonLoader;
using T90M.Common;
using UnityEngine;

namespace T90M
{
	// Turns the T-72M1 into the T-90M: the steps that run at spawn and the ones a moment later.
	internal static partial class Conversion
	{
		static readonly (string group, string path)[] Bones =
			{ ("HULL", "---MESH---/HULL"), ("TURRET", "---MESH---/HULL/TURRET"), ("GUN", "---MESH---/HULL/TURRET/GUN"),
				("MANTLET_COVER", "---MESH---/HULL/TURRET/GUN") };

		const string PaintedPrefix = "t90m_t_90m_";

		sealed class Parts
		{
			internal Vehicle Vehicle;
			internal Transform Root;
			internal Transform Turret;
			internal Transform Gun;
			internal Dictionary<string, (Mesh mesh, Transform bone)> Volumes = new Dictionary<string, (Mesh mesh, Transform bone)>();
		}

		static readonly ConversionStep<Parts>[] Steps =
		{
			new ConversionStep<Parts> { What = "base visuals", Run = HideBase },
			new ConversionStep<Parts> { What = "visuals", Run = AttachVisuals },
			new ConversionStep<Parts> { What = "paint", Run = Paint },
			new ConversionStep<Parts> { What = "hatch undersides", Otherwise = "left plain", Run = PaintHatchUndersides },
			new ConversionStep<Parts> { What = "tactical numbers", Run = PlaceNumbers },
			new ConversionStep<Parts> { What = "leftovers", Run = HideLeftovers },
			// Before PortAxes moves the pivots, the pieces are placed in prefab space with the rig at rest.
			new ConversionStep<Parts> { What = "armour", Run = p => p.Volumes = Armour.Apply(p.Vehicle) },
			new ConversionStep<Parts>
			{
				What = "interior",
				Otherwise = "left as the T-72M1's",
				Run = p => Interior.Apply(p.Vehicle),
			},
			new ConversionStep<Parts>
			{
				What = "bustle box",
				Run = p => Firepower.FitBustle(p.Vehicle, p.Volumes),
				SpawnOnly = true,
				Late = LateText("bustle box", "no bustle box"),
			},
			new ConversionStep<Parts>
			{
				What = "ammo setup",
				Run = p => PortAmmo.ApplyAtSpawn(p.Vehicle),
				SpawnOnly = true,
				Late = LateText("ammo setup", "it keeps the T-72M1's ammunition"),
			},
			new ConversionStep<Parts>
			{
				What = "3OF82 fuse setup",
				Run = p => AirBurst.Attach(p.Vehicle),
				SpawnOnly = true,
				Late = LateText("3OF82 fuse setup", "the 3OF82 has no air-burst fuse"),
			},
			new ConversionStep<Parts>
			{
				What = "coax setup",
				Run = p => PortAmmo.ApplyCoaxAtSpawn(p.Vehicle),
				SpawnOnly = true,
				Late = LateText("coax setup", "the coax keeps the T-72M1's belt"),
			},
			new ConversionStep<Parts> { What = "gun setup", Run = p => GunSetup.Apply(p.Vehicle, Firepower.Gun) },
			new ConversionStep<Parts> { What = "mobility setup", Otherwise = "left as the T-72M1's", Run = SetMobility },
			new ConversionStep<Parts> { What = "smoke launchers", Run = SeatSmoke },
			new ConversionStep<Parts> { What = "hatches", Run = MoveHatches },
			new ConversionStep<Parts> { What = "turret axis", Run = MoveAxes },
			new ConversionStep<Parts>
			{
				What = "commander's station",
				Otherwise = "the station stays fixed",
				Run = p => Commander.Apply(p.Vehicle),
				SpawnOnly = true,
				Late = LateText("commander's station", "no commander's station"),
			},
			new ConversionStep<Parts>
			{
				What = "modules",
				Otherwise = "the T-72M1's kept",
				Run = p => Modules.Build(p.Vehicle),
				SpawnOnly = true,
				Late = LateText("modules", "no modules"),
			},
			// Left at the T-72M1's sight the laser starts inside the turret front and ranges the tank's own ERA.
			new ConversionStep<Parts>
			{
				What = "laser placement",
				Otherwise = "the T-72M1's kept",
				Run = p => Sights.PlaceLaser(p.Vehicle),
				SpawnOnly = true,
				Late = LateText("laser placement", "the laser stays at the T-72M1's place"),
			},
			new ConversionStep<Parts>
			{
				What = "periscope placement",
				Otherwise = "the T-72M1's place kept",
				Run = p => Sights.PlacePeriscope(p.Vehicle),
				SpawnOnly = true,
				Late = LateText("periscope placement", "the periscope stays at the T-72M1's place"),
			},
			new ConversionStep<Parts>
			{
				What = "TShU-1-2M",
				Run = p => Countermeasures.Apply(p.Vehicle),
				SpawnOnly = true,
				Late = LateText("TShU-1-2M", "no TShU-1-2M"),
			},
			new ConversionStep<Parts> { What = "LOD materials", Run = p => VisualLods.Sync(p.Root, T90MMod.FriendlyName) },
		};

		internal static void Convert(Vehicle vehicle, bool atSpawn = false)
		{
			if (T90MMod.Visual == null || !ConvertSweep.Mark(vehicle)) return;
			var root = vehicle.transform;
			var parts = new Parts
			{
				Vehicle = vehicle,
				Root = root,
				Turret = root.Find("---MESH---/HULL/TURRET"),
				Gun = root.Find("---MESH---/HULL/TURRET/GUN"),
			};
			Func<ConversionStep<Parts>, bool> keeps = null;
			DevKeeps(ref keeps);
			ConversionSteps.Run(T90MMod.FriendlyName, Steps, parts, vehicle.name, atSpawn, keeps);
			MelonCoroutines.Start(LateSetup(vehicle, atSpawn));
		}

		// What a step that only runs at spawn leaves undone, said by ConversionSteps.Run when a vehicle is converted late.
		static string LateText(string step, string note) =>
			$"{T90MMod.FriendlyName} {step}: {{0}} was converted after it spawned; {note}.";

		static void HideBase(Parts parts)
		{
			BaseRigT72M1.HideBaseVisuals(parts.Root, EmbeddedFile.T72M1Skin(T90MMod.FriendlyName));
			if (BaseRigT72M1.TrimFailure != null)
				MelonLogger.Warning($"{T90MMod.FriendlyName}: {BaseRigT72M1.TrimFailure}; the T-72 side skirts may show through the hull.");
		}

		static void AttachVisuals(Parts parts)
		{
			VisualParts.Attach(parts.Root, T90MMod.Visual, "T90M_", Bones);
			var cover = parts.Gun != null ? parts.Gun.Find("T90M_MANTLET_COVER") : null;
			if (cover == null)
			{
				MelonLogger.Warning($"{T90MMod.FriendlyName}: bundle has no MANTLET_COVER; the mantlet cover stays on the turret.");
				return;
			}
			MantletCover.Skin(parts.Root, parts.Turret, parts.Gun, cover);
		}

		static void Paint(Parts parts)
		{
			PaintFinish.Apply(T90MMod.WeatheredPaint);
			var template = BaseRigT72M1.FindTemplate(parts.Root);
			var flir = Shader.Find("Standard (FLIR)");
			foreach (var part in parts.Root.GetComponentsInChildren<Transform>(true).Where(p => p.name.StartsWith("T90M_")).ToArray())
				if (part.parent == null || !part.parent.name.StartsWith("T90M_"))
					VehiclePaint.ApplyPaint(part.gameObject, template, flir, PaintedPrefix);
			// Moving parts keep their bare group names until Commander renames them, so match on any T90M_ parent.
			VanillaMaterials.Apply(parts.Root, T90MMod.Roles);
			DevPaint(parts);
		}

		static void PaintHatchUndersides(Parts parts)
		{
			if (HatchUndersides.Plain(parts.Root, "t90m_hatch_inner") == 0)
				MelonLogger.Warning($"{T90MMod.FriendlyName}: no hatch underside material found (old bundle?).");
			HatchUndersides.Paint(
				parts.Root,
				"t90m_hatch_inner",
				"t90m_t_90m_turret",
				new RectInt(1312, 3856, 230, 110),   // x 1312-1542, y 3856-3966 of the turret atlas: a smooth plate, flat in the normal map
				new Vector2Int(4096, 4096));
		}

		static void PlaceNumbers(Parts parts)
		{
			if (parts.Turret == null) return;
			TacticalNumbers.Apply(parts.Root, parts.Turret, Numbers);
		}

		static void HideLeftovers(Parts parts)
		{
			BaseRigT72M1.HideLeftovers(parts.Root, "T90M_");
			if (parts.Turret == null) return;
			BaseRigT72M1.MoveEjectionMarkers(
				parts.Root,
				// The T-90M's own ejection hatch (hatch_03), against the T-72M1 port's centre.
				parts.Turret.TransformVector(new Vector3(-0.033f, 2.114f, -1.518f) - BaseRigT72M1.EjectionPortCentre));
		}

		static void MoveHatches(Parts parts)
		{
			HatchRig.Apply(parts.Root, T90MMod.Rig, "T90M_", T90MMod.FriendlyName);
		}

		static void SetMobility(Parts parts)
		{
			Mobility.Apply(parts.Vehicle, Drive.Settings);
		}

		static void SeatSmoke(Parts parts)
		{
			SmokeLaunchers.Seat(parts.Vehicle, T90MMod.SmokePoints);
			SmokeCaps.Apply(parts.Root);
		}

		static void MoveAxes(Parts parts)
		{
			var axes = PortAxes.FromRig(T90MMod.Rig, BaseRigT72M1.TurretPivot, BaseRigT72M1.GunPivot, T90MMod.FriendlyName);
			if (axes != null) PortAxes.Apply(parts.Vehicle, axes);
			else MelonLogger.Warning($"{T90MMod.FriendlyName}: the rig map has no axes; the turret turns on the T-72M1's pivots.");
		}

		static readonly InteriorMap.Embedded Interior = new InteriorMap.Embedded(
			typeof(T90MMod).Assembly,
			T90MMod.FriendlyName,
			T90MMod.UnitKey,
			"T-72M1");
		static readonly PortModules Modules = new PortModules(
			typeof(T90MMod).Assembly,
			T90MMod.FriendlyName,
			"T-72M1",
			T90MMod.UnitKey);

		static readonly TacticalNumbers.Settings Numbers = new TacticalNumbers.Settings
		{
			Mode = T90MMod.NumberPosition,
			// The lamp on the model's axes, moved to the T-72's turret ring centre.
			LampFace = new Vector3(-0.475f, 2.419f - BaseRigT72M1.TurretPivot.y, -1.872f - BaseRigT72M1.TurretPivot.z),
			LampNormal = new Vector3(0f, 0f, -1f),
			DigitScale = 1.18f,
			Tag = T90MMod.FriendlyName,
			DigitSpacing = 0.059f,   // two windows on the lamp, the T-72's 7.3 cm put each digit off its window
		};

		static readonly ConversionStep<Vehicle>[] LateSteps =
		{
			new ConversionStep<Vehicle> { What = "voices", Run = v => CrewVoices.Apply(v, T90MMod.FriendlyName) },
			new ConversionStep<Vehicle>
			{
				What = "sight setup",
				Otherwise = "the T-72M1's sights kept",
				Run = v => Sights.Apply(v),
			},
			new ConversionStep<Vehicle> { What = "ПДТ", Run = v => Pdt.Apply(v) },
			new ConversionStep<Vehicle> { What = "commander's station late setup", Run = v => Commander.LateSetup(v) },
			new ConversionStep<Vehicle> { What = "target tracker", Run = v => Tracker.Apply(v) },
			new ConversionStep<Vehicle> { What = "module wiring", SpawnOnly = true, Run = v => Modules.Wire(v) },
			new ConversionStep<Vehicle>
			{
				What = "thermal heat",
				Run = v => ThermalHeat.Apply(v.transform, ThermalHeat.PortParts(v.transform, "T90M_"), T90MMod.FriendlyName),
			},
			new ConversionStep<Vehicle> { What = "ammunition self-check", SpawnOnly = true, Run = v => PortAmmo.SelfCheck(v) },
		};

		static IEnumerator LateSetup(Vehicle vehicle, bool atSpawn)
		{
			// A spawned unit's smoke launcher cached the crew before it existed and never fires without a refresh.
			yield return SmokeLaunchers.RefreshCrewLater(vehicle, T90MMod.FriendlyName);
			if (vehicle == null) yield break;
			ConversionSteps.Run(T90MMod.FriendlyName, LateSteps, vehicle, vehicle.name, atSpawn);
			DevAfter(vehicle, atSpawn);
		}

		// Dev hooks: optional methods the author's own test harness fills in. They compile to nothing in a normal build.
		static partial void DevKeeps(ref Func<ConversionStep<Parts>, bool> keep);

		static partial void DevPaint(Parts parts);

		static partial void DevAfter(Vehicle vehicle, bool atSpawn);
	}
}
