// Fits an add-on kit (a second model's parts, its armour and modules) to a unit that is the port already.
using System;
using System.Collections.Generic;
using System.Linq;
using GHPC;
using GHPC.Equipment;
using GHPC.PhysicsHelpers;
using GHPC.Vehicle;
using MelonLoader;
using UnityEngine;

namespace T90M.Common
{
	internal static partial class PortKits
	{
		internal sealed class Context
		{
			public string Tag = "Port";
			public string Prefix = "";
			public GameObject ArmourPrefab;
			public Func<DmArmour> ArmourValues;
			public PortModules Modules;
			public Func<string, Vector3> BoneMoved;
		}

		internal sealed class Fitted : MonoBehaviour
		{
			internal readonly List<string> Kits = new List<string>();
			internal readonly List<GameObject> Armour = new List<GameObject>();
			internal readonly List<GameObject> Dropped = new List<GameObject>();
		}

		internal static bool IsFitted(Vehicle vehicle, string kit)
		{
			var fitted = vehicle != null ? vehicle.GetComponent<Fitted>() : null;
			return fitted != null && fitted.Kits.Contains(kit.ToLowerInvariant());
		}

		internal static List<Kit> Load(AssetBundle bundle, string tag)
		{
			var text = PortText.Load(bundle, "kits");
			if (text == null) return null;
			var problems = new List<string>();
			var kits = Parse(text, problems);
			if (problems.Count > 0) MelonLogger.Warning($"{tag} kits: {string.Join("; ", problems)}; those lines are left out.");
			return kits;
		}

		internal static string Apply(Vehicle vehicle, Kit kit, Context context, List<string> problems)
		{
			var fitted = vehicle.GetComponent<Fitted>() ?? vehicle.gameObject.AddComponent<Fitted>();
			string key = kit.Name.ToLowerInvariant();
			if (fitted.Kits.Contains(key)) return $"the {kit.Name} kit is fitted already";
			fitted.Kits.Add(key);
			var root = vehicle.transform;
			var everything = root.GetComponentsInChildren<Transform>(true);
			Transform GroupObject(string group) => everything.FirstOrDefault(p => p.name == context.Prefix + group);
			var log = new List<string>();

			int rootsOn = 0;
			foreach (var (group, rootName) in kit.Roots)
			{
				var groupObject = GroupObject(group);
				var kitRoot = groupObject != null ? groupObject.Find(rootName) : null;
				if (kitRoot == null)
				{
					problems.Add($"{group}/{rootName} not in the unit");
					continue;
				}
				kitRoot.gameObject.SetActive(true);
				rootsOn++;
			}
			log.Add($"{rootsOn} of {kit.Roots.Count} kit roots on");

			var hidden = new List<Renderer>();
			int hides = 0;
			foreach (var (group, path) in kit.Hides)
			{
				var groupObject = GroupObject(group);
				var target = Locate(everything, groupObject, path, context.Prefix);
				if (target == null)
				{
					problems.Add($"hidden part {group}/{path} not found");
					continue;
				}
				hidden.AddRange(target.GetComponentsInChildren<Renderer>(true));
				foreach (var brick in target.GetComponentsInChildren<MeshFilter>(true)) EraBricks.Retire(brick.transform);
				target.gameObject.SetActive(false);
				hides++;
			}
			foreach (var renderer in hidden) renderer.enabled = false;
			if (hidden.Count > 0) KeepHidden.Add(root, hidden.ToArray(), null, $"{kit.Name} kit hidden part");
			log.Add($"{hides} of {kit.Hides.Count} normal parts hidden ({hidden.Count} renderers kept off)");

			if (kit.RigLines.Count > 0) log.Add(FitRig(root, kit, context));

			log.Add(FitArmour(vehicle, kit, context, fitted, problems));

			if (context.Modules != null)
			{
				if (kit.ModuleHides.Count > 0) log.Add(context.Modules.HideModules(vehicle, kit.ModuleHides));
				context.Modules.BuildKit(vehicle, kit.Name);
			}

			if (kit.SmokePoints.Count > 0) log.Add(MoveSmokePoints(vehicle, kit, context, problems));
			if (kit.SmokeCaps.Count > 0)
			{
				var plainCaps = root.GetComponentsInChildren<MeshRenderer>(true)
					.Where(r => r.name.StartsWith("smoke_cap_") && !SmokeCaps.UnderKitRoot(r.transform))
					.Cast<Renderer>()
					.ToArray();
				foreach (var cap in plainCaps) cap.enabled = false;
				if (plainCaps.Length > 0) KeepHidden.Add(root, plainCaps, null, $"{kit.Name} kit hidden smoke cap");
				log.Add($"{plainCaps.Length} normal smoke caps hidden");
				log.Add("smoke caps: " + SmokeCaps.Apply(root, kit: true));
			}

			ThermalHeat.Apply(root, ThermalHeat.PortParts(root, context.Prefix), context.Tag);
			log.Add("thermal heat applied again");
			string line = $"{kit.Name} kit fitted: " + string.Join("; ", log);
			return line;
		}

		static Transform Locate(Transform[] everything, Transform groupObject, string path, string prefix)
		{
			var found = groupObject != null ? groupObject.Find(path) : null;
			if (found != null) return found;
			string last = path.Substring(path.LastIndexOf('/') + 1);
			string tail = "/" + path;
			var named = everything.Where(p => p.name == last).ToList();
			var byName = named.FirstOrDefault(p => PathOf(p).EndsWith(tail, StringComparison.Ordinal))
				?? (named.Count == 1 ? named[0] : null);
			if (byName != null || string.IsNullOrEmpty(prefix)) return byName;
			var renamed = everything.Where(p => p.name == prefix + last).ToList();
			return renamed.Count == 1 ? renamed[0] : null;
		}

		static string PathOf(Transform part)
		{
			string path = part.name;
			for (var parent = part.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
			return path;
		}

		static string FitRig(Transform root, Kit kit, Context context)
		{
			var map = RigMap.Parse(string.Join("\n", kit.RigLines));
			var rigLog = HatchRig.Apply(root, map, context.Prefix, context.Tag, placed: true);
			return $"{kit.RigLines.Count} rig parts: " + string.Join("; ", rigLog);
		}

		static string FitArmour(Vehicle vehicle, Kit kit, Context context, Fitted fitted, List<string> problems)
		{
			if (kit.ArmourGroups.Count == 0 && kit.ArmourHides.Count == 0) return "no armour in the kit";
			if (context.ArmourPrefab == null || context.ArmourValues == null)
			{
				problems.Add("no armour prefab or values: the kit's armour is not fitted and the normal armour stays");
				return "armour not fitted";
			}
			var values = context.ArmourValues();
			if (values == null)
			{
				problems.Add("no armour values: the kit's armour is not fitted and the normal armour stays");
				return "armour not fitted";
			}
			var rigGroups = DmArmourApply.GroupsFor(vehicle.transform);
			var mapped = new List<(string group, string bone, string colliders)>();
			var offsets = new Dictionary<string, Vector3>();
			foreach (var (armourGroup, rigGroup) in kit.ArmourGroups)
			{
				var rig = rigGroups.FirstOrDefault(c => c.group == rigGroup);
				if (rig.group == null)
				{
					problems.Add($"armour group {armourGroup}: the unit's rig has no {rigGroup}");
					continue;
				}
				mapped.Add((armourGroup, rig.bone, rig.colliders));
				offsets[armourGroup] = context.BoneMoved != null ? -context.BoneMoved(rigGroup) : Vector3.zero;
			}
			var roots = rigGroups
				.Select(r => DmArmourApply.ArmorRoot(vehicle, r.colliders))
				.Where(a => a != null)
				.Distinct()
				.ToList();
			var normal = roots.SelectMany(a => a.Cast<Transform>()).ToList();
			int made = mapped.Count == 0
				? 0
				: DmArmourApply.Apply(
					vehicle,
					context.ArmourPrefab,
					values,
					context.Tag,
					problems,
					null,
					mapped.ToArray(),
					g => offsets.TryGetValue(g, out var offset) ? offset : Vector3.zero);
			var before = new HashSet<Transform>(normal);
			int added = 0;
			foreach (var armourRoot in roots)
				foreach (var piece in armourRoot.Cast<Transform>().ToList())
				{
					if (before.Contains(piece)) continue;
					fitted.Armour.Add(piece.gameObject);
					RaycastColliderGroup.AddNewColliderSetToParentGroup(piece);
					added++;
				}
			EraBricks.AddLinks(kit.EraLinks);
			EraBricks.Relink(vehicle.transform);
			int dropped = 0;
			foreach (string name in kit.ArmourHides)
			{
				var pieces = normal.Where(p => p != null && p.name == name).ToList();
				if (pieces.Count == 0)
				{
					problems.Add($"armour piece '{name}' to drop not found");
					continue;
				}
				foreach (var piece in pieces)
				{
					foreach (var armour in piece.GetComponents<UniformArmor>()) EraBricks.Unregister(armour);
					piece.gameObject.SetActive(false);
					fitted.Dropped.Add(piece.gameObject);
					dropped++;
				}
			}
			return $"armour: {made} pieces made ({added} placed), {dropped} of {kit.ArmourHides.Count} normal pieces dropped";
		}

		static string MoveSmokePoints(Vehicle vehicle, Kit kit, Context context, List<string> problems)
		{
			var manager = vehicle.GetComponentInChildren<VehicleSmokeManager>(true);
			var turret = RigBones.Turret(vehicle.transform);
			if (manager == null || turret == null) return "smoke points: no smoke manager or turret";
			var moved = context.BoneMoved != null ? context.BoneMoved("TURRET") : Vector3.zero;
			int count = 0;
			for (int i = 0; i < kit.SmokePoints.Count; i++)
			{
				var slot = SmokeLaunchers.SlotOfTube(manager, i + 1);
				if (slot == null || slot.SpawnLocation == null) continue;
				var point = new Vector3(kit.SmokePoints[i][0], kit.SmokePoints[i][1], kit.SmokePoints[i][2]);
				slot.SpawnLocation.position = turret.TransformPoint(point - moved);
				count++;
			}
			if (count < kit.SmokePoints.Count)
				problems.Add($"smoke points: {kit.SmokePoints.Count - count} tube(s) have no grenade slot to move");
			return $"smoke points: {count} of {kit.SmokePoints.Count} tubes moved";
		}
	}
}
