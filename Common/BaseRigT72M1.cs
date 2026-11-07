using System.Linq;
using UnityEngine;

namespace T90M.Common
{
	public static class BaseRigT72M1
	{
		public const string HullPath = "---MESH---/HULL", TurretPath = "---MESH---/HULL/TURRET", GunPath = "---MESH---/HULL/TURRET/GUN";

		public static readonly string[] HiddenRenderers =
		{
			"T72M1_hull", "T72M1_turret", "T72M1_skin_turret", "lp_turret_cables001", "equipment",
			"t72m hull net", "t72m turret net", "t72m gun net",
			"roundels_72M", "roundels_72M1", "bustle_number_3digit", "bustle_number_4digit",
			"nsv_mount", "lp_R123",
		};

		// "Gunner Sight" and "Night Sight" stand inside the new left cheek. "Fuel tanks" is the right fender's tank row.
		public static readonly string[] ImportReplaces =
		{
			"Upper Front Plate", "Upper Front Plate Extra Armor", "Textolite.001", "Backing Plate Armor.004", "Driver Extra Armor",
			"Lower Front Plate", "Hull Side", "Hull Side.001", "Hull Floor", "Hull Roof 20mm", "Hull Roof 30mm ", "Hull rear",
			"Hull rear.001", "Turret Ring", "lp_driverhatch",
			"Mud Guard", "Mudguard Rubber", "Sponson", "Sponson Fuel Tank", "Fuel tanks", "Sponson Storage Container", "Log", "Plow",
			"Turret.002", "Composite Armor Array", "Turret Storage box", "Sight Shielding", "Loaders Hatch.002", "Ejector Door",
			"Gun Mantlet",
			"Commanders Cupola", "Commanders Hatch", "Commanders Spotlight", "Commanders Light Cover",
			"Luna", "Luna Cover",
			"Gunsight Housing", "NightSight Housing", "Night Sight Cover", "Gunsight Glass", "Night Sight Glass", "Gunner Sight", "Night Sight",
		};

		// Collapsed, they leave the skirt vertices blended with HULL half-pulled, slivers through the new hull sides.
		// Hence the trimmed skin, the collapse stays as the fallback.
		public static bool IsHullDetailBone(string n) =>
			n == "fender_L" || n == "fender_R" || n == "flaps" || n.StartsWith("gill_") || n.StartsWith("skirt_");

		public static void HideBaseVisuals(Transform root, byte[] trimmedSkin = null)
		{
			foreach (var r in root.GetComponentsInChildren<Renderer>(true))
				if (HiddenRenderers.Contains(r.gameObject.name) || (r.transform.parent != null && HiddenRenderers.Contains(r.transform.parent.name)))
					r.enabled = false;
			foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.name == "T72M1_skin_hull"))
			{
				foreach (var bone in skin.bones)
					if (bone != null && IsHullDetailBone(bone.name)) bone.localScale = Vector3.zero;
				if (trimmedSkin != null) TrimSkin(skin, trimmedSkin);
			}

			var smoke = root.Find("---MESH---/HULL/TURRET/smoke rack");
			if (smoke != null) smoke.localScale = Vector3.zero;
		}

		static readonly TrimmedSkin Trim = new TrimmedSkin("skirts / flaps / fenders / gills removed");

		public static string TrimFailure => Trim.Failure;

		public static void TrimSkin(SkinnedMeshRenderer skin, byte[] data) => Trim.Swap(skin, data, "bones collapsed only");

		// The T-72 interior pokes through the new turret. The IR searchlight's light stays, the night sight needs it.
		public static readonly string[] LeftoverMaterials = { "T72_interior", "GST emission", "IR searchlight glow" };
		public static readonly string[] LeftoverNames = { "Luna lens glow", "Luna visible glow" };
		public const string LeftoverLightPrefix = "GST light";

		public static void HideLeftovers(Transform root, string ownPrefix)
		{
			bool UnderOurs(Transform t) { for (var x = t; x != null && x != root; x = x.parent) if (x.name.StartsWith(ownPrefix)) return true; return false; }
			string Clean(Material m) => m == null ? "" : m.name.Replace(" (Instance)", "");
			var rs = root.GetComponentsInChildren<Renderer>(true)
				.Where(r => !UnderOurs(r.transform) && !(r is ParticleSystemRenderer)
					&& (LeftoverNames.Contains(r.name) || r.sharedMaterials.Any(m => LeftoverMaterials.Contains(Clean(m)))))
				.ToArray();
			var ls = root.GetComponentsInChildren<Light>(true).Where(l => l.name.StartsWith(LeftoverLightPrefix)).ToArray();
			foreach (var r in rs) r.enabled = false;
			foreach (var l in ls) l.enabled = false;
			KeepHidden.Add(root, rs, ls);
		}

		public static readonly Vector3 TurretPivot = new Vector3(0f, 1.369f, -0.527f), GunPivot = new Vector3(0f, 1.651f, 0.372f);

		public static readonly Vector3 EjectionPortCentre = new Vector3(0.003f, 1.974f, -1.214f);

		public static void MoveEjectionMarkers(Transform root, Vector3 worldShift)
		{
			foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(x => x.name == "ejection path" || x.name == "ejection port fire marker").ToList())
			{
				t.position += worldShift;
			}
		}

		public static Material FindTemplate(Transform root)
		{
			foreach (var r in root.GetComponentsInChildren<Renderer>(true))
				if (r.name == "T72M1_hull" || r.name == "T72M1_turret")
				{
					var m = r.sharedMaterials.FirstOrDefault(x => x != null && x.shader.name == "GHPC/VehicleShader");
					if (m != null) return m;
				}
			return null;
		}
	}
}
