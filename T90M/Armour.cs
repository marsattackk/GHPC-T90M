using System;
using System.Collections.Generic;
using System.Linq;
using GHPC.Vehicle;
using MelonLoader;
using T90M.Common;
using UnityEngine;

namespace T90M
{
	// Loads the armour pieces and their numbers, switches off the T-72M1 pieces War Thunder's model replaces, and scales
	// the cheeks and glacis filling down for source-only.
	internal static partial class Armour
	{
		internal static GameObject Prefab;
		internal static DmArmour Values;
		internal static MelonPreferences_Entry<string> Preset;

		static readonly string[] BaseOff = { "Fire Wall", "Rear Fuel tank" };
		// The T-72M1's barrel tube moved onto the T-90M's bore and muzzle (4.5 cm up, 18.5 cm back), for a bundle that
		// doesn't reshape the barrel itself.
		static readonly (string piece, Matrix4x4 m)[] BaseMoved = { ("Cannon", Matrix4x4.Translate(new Vector3(0f, 0.045f, -0.185f))) };

		internal static void Config(MelonPreferences_Category cfg)
		{
			Preset = ArmourPresets.Create(
				cfg,
				SettingText.ArmourPreset);
		}

		internal static void Load(AssetBundle bundle)
		{
			Prefab = BundleAssets.Prefab(
				bundle,
				"T90M_armor",
				T90MMod.FriendlyName,
				required: false,
				ifMissing: "armour will not be changed.");
			var values = PortText.Load(bundle, DmArmour.AssetName);
			if (values != null) Values = DmArmour.Parse(values);
			var edits = PortText.Load(bundle, VanillaEdits.AssetName);
			if (edits != null) T90MMod.Edits = VanillaEdits.Parse(edits);
			if (T90MMod.Edits != null) T90MMod.Edits.Off = T90MMod.Edits.Off.Union(BaseRigT72M1.ImportReplaces).Union(BaseOff).ToArray();
			if (T90MMod.Edits != null)
				T90MMod.Edits.Transforms = T90MMod.Edits.Transforms
					.Where(x => !BaseMoved.Any(m => m.piece == x.piece))
					.Concat(BaseMoved.Where(m => !T90MMod.Edits.Reshape.Contains(m.piece)))
					.ToArray();
			if (Prefab != null && Values == null)
				MelonLogger.Warning($"{T90MMod.FriendlyName}: no armour values; the T-72's armour stays.");
			if (Prefab != null && T90MMod.Edits == null)
				MelonLogger.Warning($"{T90MMod.FriendlyName}: no vanilla_edits; the T-72M1's armour stays under the T-90M's.");
		}

		static bool _warned;
		static bool _netsChecked;

		internal static Dictionary<string, (Mesh mesh, Transform bone)> Apply(Vehicle vehicle)
		{
			var volumes = new Dictionary<string, (Mesh mesh, Transform bone)>();
			if (Prefab == null || Values == null) return volumes;
			var problems = new List<string>();
			string preset = ArmourPresets.Pick(Preset);
			var values = Presets.For(Values, preset);
			DmArmourApply.Install(vehicle, Prefab, values, T90MMod.Edits, T90MMod.FriendlyName, problems, volumes: volumes);
			if (!_warned && problems.Count > 0)
			{
				_warned = true;
				MelonLogger.Warning($"{T90MMod.FriendlyName} armour: {problems.Count} piece(s) not found or left out "
					+ $"({string.Join("; ", problems)}); the armour may not match the model (game update?).");
			}
			if (!_netsChecked)
			{
				_netsChecked = true;
				if (!Values.Entries.Any(e => e.Slat
					&& e.Name != null
					&& e.Name.StartsWith(NetScreens.NamePrefix, StringComparison.OrdinalIgnoreCase)))
					MelonLogger.Warning($"{T90MMod.FriendlyName} armour: no slat piece named \"{NetScreens.NamePrefix}...\"; "
						+ "the net screens act as plain slat armour.");
			}
			DevApplied(vehicle, values, preset, volumes);
			return volumes;
		}

		static readonly string[] GlacisFilling = { "composite_armor_hull_01_dm", "turret_05_front_dm", "turret_04_front_dm" };
		// Source-only against game-scale, per filling, the ratio of the two solved figures. Cheek packages
		// for the open-source 575 / 750, glacis middle layers for 500 / 650.
		const float PackageKe = 0.7967f / 1.1652f;
		const float PackageCe = 1.4182f / 1.6508f;
		const float GlacisKe = 1.0414f / 1.3170f;
		const float GlacisCe = 1.9487f / 2.4995f;
		static readonly ArmourPresets.Pair Presets = new ArmourPresets.Pair(ArmourPresets.GameScale, entry =>
			entry.Variable && entry.Piece.Contains("_pkg") ? (PackageKe, PackageCe)
			: entry.Variable && Array.IndexOf(GlacisFilling, entry.Piece) >= 0 ? (GlacisKe, GlacisCe)
			: ((float, float)?)null);

		// Dev hooks: optional methods the author's own test harness fills in. They compile to nothing in a normal build.
		static partial void DevApplied(
			Vehicle vehicle,
			DmArmour values,
			string preset,
			Dictionary<string, (Mesh mesh, Transform bone)> volumes);
	}
}
