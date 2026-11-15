using System.Collections.Generic;
using System.Linq;
using GHPC.Effects;
using GHPC.Vehicle;
using HarmonyLib;
using UnityEngine;

namespace T90M.Common
{
	// An ammo box sealed off from the crew, with lids that blow out.
	// GHPC's cook-off would otherwise take the whole tank with it.
	internal static class BlowoutRack
	{
		internal sealed class Lid
		{
			public string Name;
			public string ExitName;
			public float ExitRadius = 0.4f;
			public FlammableSourceType Source = FlammableSourceType.SideLarge;
			public float MassKg = 50f;
			public float FlameHeightTrigger = 1f;
			public float ForceForFlameTrigger = 700f;
		}

		// Fields a tank never sets keep their defaults here: another tank sets them (CS0649 is off for this declaration only).
#pragma warning disable CS0649
		internal sealed class Settings
		{
			public string Tag = "Port";
			public string Prefix = "";
			public string RackPath;
			public string RackName;
			public float RetrievalDelay = 4f, StorageDelay = 2f;
			public string CompartmentName;
			public string VolumeName;
			public string UpReference;
			public Lid[] Lids = new Lid[0];
			public float ResidualTnt = 1f,
				ResidualFlame = 2f,
				ResidualIgniteMin = 160f,
				ResidualIgniteMax = 170f,
				ResidualBurn = 180f,
				ResidualTemp = 600f;
			public float FireScale = 1f;
			public string ReplaceCompartment;
		}
#pragma warning restore CS0649

		// GHPC kills chassis power on a burst in any compartment. The box is sealed off from the crew and engine, so the tank keeps running.
		internal sealed class Isolated : MonoBehaviour
		{
			public GHPC.Unit Unit;
			public float FireScale = 1f;
		}

		static readonly AccessTools.FieldRef<CompartmentExit, Compartment> ExitCompartment =
			Reflect.FieldRefAccess<CompartmentExit, Compartment>("_compartment");

		[HarmonyPatch(typeof(CompartmentExit), nameof(CompartmentExit.SetFireRatioByFlameHeight))]
		static class FireScalePatch
		{
			// The drawn flames only, the height that throws the lids and heats the box stays GHPC's.
			static void Prefix(CompartmentExit __instance, ref float __0)
			{
				ref float height = ref __0;
				var compartment = __instance != null ? ExitCompartment(__instance) : null;
				var iso = compartment != null ? compartment.GetComponent<Isolated>() : null;
				if (iso != null) height *= iso.FireScale;
			}
		}

		// GHPC's cook-off blast is a sphere that hits its own tank through the floor, it took the engine out 1.4 m below.
		// Own parts hidden while the box updates, so it goes up and out like the panels are meant to send it.
		[HarmonyPatch(typeof(Compartment), nameof(Compartment.DoUpdate))]
		static class VentPatch
		{
			static readonly Stack<List<GHPC.Equipment.DestructibleComponent>> Hidden =
				new Stack<List<GHPC.Equipment.DestructibleComponent>>();

			static void Prefix(Compartment __instance)
			{
				var iso = __instance != null ? __instance.GetComponent<Isolated>() : null;
				var items = iso?.Unit?.InfoBroker?.BlastDamageRouter?.BlastableItems;
				var mine = items != null ? new List<GHPC.Equipment.DestructibleComponent>() : null;
				Hidden.Push(mine);
				if (items == null) return;
				foreach (var vulnerability in items)
					if (vulnerability.Component != null && !vulnerability.Component.Hidden)
					{
						vulnerability.Component.Hidden = true;
						mine.Add(vulnerability.Component);
					}
			}

			static void Finalizer()
			{
				var mine = Hidden.Count > 0 ? Hidden.Pop() : null;
				if (mine != null) foreach (var component in mine) if (component != null) component.Hidden = false;
			}
		}

		static readonly AccessTools.FieldRef<Compartment, FlammablesManager> CompartmentManager =
			Reflect.FieldRefAccess<Compartment, FlammablesManager>("_manager");
		static readonly System.Reflection.FieldInfo DidBurstField = Reflect.Field(typeof(FlammablesManager), "DidBurst");

		[HarmonyPatch(typeof(Compartment), "DoBurst")]
		static class BurstPatch
		{
			static bool Prefix(Compartment __instance, float __0)
			{
				float ratio = __0;
				if (__instance == null || __instance.Parent != null || __instance.GetComponent<Isolated>() == null) return true;
				foreach (var exit in __instance.Exits) exit.MakeFireBurst(ratio);
				var manager = CompartmentManager(__instance);
				if (manager != null) (DidBurstField?.GetValue(manager) as System.Action<float>)?.Invoke(ratio);
				return false;
			}
		}

		static readonly AccessTools.FieldRef<GHPC.Weapons.AmmoRack, Compartment> RackCompartment =
			Reflect.FieldRefAccess<GHPC.Weapons.AmmoRack, Compartment>("_compartment");
		static readonly AccessTools.FieldRef<GHPC.Weapons.AmmoRack, float> RackRetrieval =
			Reflect.FieldRefAccess<GHPC.Weapons.AmmoRack, float>("_retrievalDelaySeconds");
		static readonly AccessTools.FieldRef<GHPC.Weapons.AmmoRack, float> RackStorage =
			Reflect.FieldRefAccess<GHPC.Weapons.AmmoRack, float>("_storageDelaySeconds");
		static readonly AccessTools.FieldRef<FlammablesCluster, Transform> ClusterMarker =
			Reflect.FieldRefAccess<FlammablesCluster, Transform>("_fireMarker");

		internal static string Apply(Vehicle vehicle, Settings s, Mesh volume, Transform bone, List<string> problems)
		{
			var root = vehicle.transform;
			var rackT = InteriorMap.Find(root, s.RackPath);
			var rack = rackT != null ? rackT.GetComponent<GHPC.Weapons.AmmoRack>() : null;
			var manager = vehicle.GetComponent<FlammablesManager>();
			if (rack == null)
			{
				problems.Add($"rack '{s.RackPath}' not found");
				return null;
			}
			if (manager == null || manager.Compartments == null || manager.Compartments.Length == 0)
			{
				problems.Add("no FlammablesManager compartments");
				return null;
			}
			Compartment replaced = null;
			if (!string.IsNullOrEmpty(s.ReplaceCompartment))
			{
				if (manager.Compartments.Any(c => c != null
					&& c.Name == s.CompartmentName
					&& c.GetComponent<Isolated>() != null))
					return null;
				replaced = manager.Compartments.FirstOrDefault(c => c != null
					&& c.Name == s.ReplaceCompartment);
				var rmc = replaced != null ? replaced.GetComponent<MeshCollider>() : null;
				if (rmc == null || rmc.sharedMesh == null)
				{
					problems.Add($"compartment '{s.ReplaceCompartment}' to replace not found");
					return null;
				}
				volume = rmc.sharedMesh;
			}
			if (volume == null || (bone == null && replaced == null))
			{
				problems.Add($"no volume mesh '{s.VolumeName}' in the bundle");
				return null;
			}
			var sibling = replaced != null
				? replaced.GetComponent<Collider>()
				: manager.Compartments.Select(c => c != null ? c.GetComponent<Collider>() : null)
					.FirstOrDefault(c => c != null
						&& c.GetComponentsInParent<GHPC.Utility.LateFollow>(true).Any(f => f.FollowTarget == bone));
			if (sibling == null)
			{
				problems.Add($"no compartment volume following '{bone.name}' to put the box beside");
				return null;
			}

			// Built inactive, Compartment.Awake has to see every field set.
			var volumeObject = new GameObject(s.VolumeName);
			volumeObject.SetActive(false);
			if (replaced != null)
			{
				volumeObject.transform.SetParent(replaced.transform.parent, false);
				volumeObject.transform.localPosition = replaced.transform.localPosition;
				volumeObject.transform.localRotation = replaced.transform.localRotation;
				volumeObject.transform.localScale = replaced.transform.localScale;
				if (replaced.GetComponent<DmArmourApply.ImportedCompartment>() != null)
					volumeObject.AddComponent<DmArmourApply.ImportedCompartment>();
				manager.Compartments = manager.Compartments.Where(c => c != replaced).ToArray();
				foreach (var cluster in replaced.Clusters ?? new List<FlammablesCluster>())
					if (cluster != null && cluster != rack.Flammables)
						problems.Add($"compartment '{replaced.Name}' held cluster '{cluster.Name}': dropped with it");
			}
			else Followers.PlaceOnBone(volumeObject.transform, bone, sibling.transform.parent);
			volumeObject.layer = sibling.gameObject.layer;
			volumeObject.tag = sibling.gameObject.tag;
			if (replaced != null) Object.DestroyImmediate(replaced.gameObject);
			var meshCollider = volumeObject.AddComponent<MeshCollider>();
			meshCollider.sharedMesh = volume;
			meshCollider.convex = false;

			var fireSource = new GameObject("residual burning");
			fireSource.SetActive(false);
			fireSource.transform.SetParent(volumeObject.transform, false);
			fireSource.transform.localPosition = volume.bounds.center;
			fireSource.layer = volumeObject.layer;
			var item = fireSource.AddComponent<FlammableItem>();
			Traverse.Create(item).Field("_explosive").SetValue(false);
			Traverse.Create(item).Field("_tntEquivalent").SetValue(s.ResidualTnt);
			Traverse.Create(item).Field("_selfOxidizing").SetValue(false);
			Traverse.Create(item).Field("_flameHeightMeters").SetValue(s.ResidualFlame);
			Traverse.Create(item).Field("_ignitionTempMin").SetValue(s.ResidualIgniteMin);
			Traverse.Create(item).Field("_ignitionTempMax").SetValue(s.ResidualIgniteMax);
			Traverse.Create(item).Field("_shortestBurnTime").SetValue(s.ResidualBurn);
			Traverse.Create(item).Field("_burnTemperature").SetValue(s.ResidualTemp);
			item.HeatTransferRatio = 1f;
			fireSource.SetActive(true);
			var residual = new FlammablesCluster { Name = "residual burn", MaxTempC = 1000f };
			residual.Items.Add(item);
			ClusterMarker(residual) = fireSource.transform;

			var old = RackCompartment(rack);
			if (old != null)
			{
				old.Clusters.Remove(rack.Flammables);
				old.Parent?.Clusters.Remove(rack.Flammables);   // a child compartment's clusters are listed in its parent too
			}

			var upBone = InteriorMap.Find(root, s.UpReference) ?? bone;
			var body = vehicle.GetComponent<Rigidbody>();
			var exits = new List<CompartmentExit>();
			var detach = new List<DetachableParent>();
			var lids = new List<string>();
			var all = root.GetComponentsInChildren<Transform>(true);
			foreach (var lidSetting in s.Lids)
			{
				var lid = all.FirstOrDefault(c => c.name == s.Prefix + lidSetting.Name)
					?? all.FirstOrDefault(c => c.name == lidSetting.Name);
				var meshFilter = lid != null
					? lid.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh != null)
					: null;
				if (lid == null || meshFilter == null)
				{
					problems.Add($"lid '{lidSetting.Name}' not found; the box vents without it");
					continue;
				}
				var bounds = meshFilter.sharedMesh.bounds;
				var colGo = new GameObject("blow-off collider");
				colGo.SetActive(false);
				colGo.layer = 14;
				colGo.transform.SetParent(meshFilter.transform, false);
				var box = colGo.AddComponent<BoxCollider>();
				box.center = bounds.center;
				box.size = bounds.size;
				var dpGo = new GameObject(lidSetting.ExitName + " blow-off");
				dpGo.SetActive(false);
				dpGo.transform.SetParent(volumeObject.transform, false);
				var detachable = dpGo.AddComponent<DetachableParent>();
				detachable.Transform = lid;
				detachable.UpReferenceTransform = upBone;
				detachable.MassKg = lidSetting.MassKg;
				detachable.OverpressureTrigger = 0f;
				detachable.OverpressureToForceFactor = 1f;
				detachable.FlameHeightTrigger = lidSetting.FlameHeightTrigger;
				detachable.ForceForFlameTrigger = lidSetting.ForceForFlameTrigger;
				detachable.MassDecreaseItem = body;
				detachable.MaxTorque = 500f;
				detachable.MinLateralForce = 300f;
				detachable.MaxLateralForce = 600f;
				detachable.DownforceMultiplier = 0f;
				detachable.LocalShiftOnDetach = new Vector3(0f, 0.07f, 0f);
				detachable.ColliderObjects = new[] { colGo };
				detachable.ObjectsToDeactivate = new GameObject[0];
				detachable.TransformsToLeaveBehind = new Transform[0];
				detachable.AarHideItemsParents = new Transform[0];
				detachable.OtherParents = new Transform[0];
				dpGo.SetActive(true);
				var marker = new GameObject(lidSetting.ExitName + " marker").transform;
				marker.SetParent(volumeObject.transform, false);
				marker.position = meshFilter.transform.TransformPoint(bounds.center);
				marker.rotation = Quaternion.LookRotation(upBone.up, -upBone.forward);
				exits.Add(new CompartmentExit
				{
					Name = lidSetting.ExitName,
					Radius = lidSetting.ExitRadius,
					FlameHeightThreshold = 0f,
					SourceType = lidSetting.Source,
					PositionMarker = marker,
					AnimatedParts = new AnimatedPart[0],
					DetachTrigger = detachable,
					KillFireWhenDetached = false,
					Closed = true,
					PressureToOpenBar = 0f,
				});
				detach.Add(detachable);
				lids.Add($"{lid.name} ({lidSetting.MassKg:0} kg, flame {lidSetting.FlameHeightTrigger:0.0} m)");
			}

			var iso = volumeObject.AddComponent<Isolated>();
			iso.Unit = vehicle;
			iso.FireScale = s.FireScale;
			var comp = volumeObject.AddComponent<Compartment>();
			comp.Name = s.CompartmentName;
			comp.Parent = null;
			comp.Volumes = new Collider[] { meshCollider };
			comp.TotalVolume = 1f;
			comp.Exits = exits;
			comp.Detachables = detach;
			comp.UnsecuredFiresHere = false;
			comp.Clusters = new List<FlammablesCluster> { residual, rack.Flammables };
			volumeObject.SetActive(true);
			comp.RegisterFlammablesManager(manager);
			manager.Compartments = manager.Compartments.Concat(new[] { comp }).ToArray();

			RackCompartment(rack) = comp;
			RackRetrieval(rack) = s.RetrievalDelay;
			RackStorage(rack) = s.StorageDelay;
			if (!string.IsNullOrEmpty(s.RackName)) rack.Name = s.RackName;
			return $"rack '{rack.Name}' ({rack.VisualSlots.Count} slots, {s.RetrievalDelay:0} s out / {s.StorageDelay:0} s in) in its own compartment '{s.CompartmentName}' "
				+ $"({volume.triangles.Length / 3} faces; out of '{(old != null ? old.Name : "none")}'); {lids.Count} of {s.Lids.Length} blow-off lids: {string.Join(", ", lids)}";
		}
	}
}
