using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GHPC.State;
using GHPC.Vehicle;
using MelonLoader;
using MelonLoader.Utils;
using T90M.Common;
using UnityEngine;

[assembly: MelonInfo(typeof(T90M.T90MMod), "T-90M", "0.1.3", "marsattackk")]
[assembly: MelonGame("Radian Simulations LLC", "GHPC")]

namespace T90M
{
	public class T90MMod : MelonMod
	{
		internal const string UnitKey = "T90M";
		internal const string FriendlyName = "T-90M";
		const string BaseUnit = "T72M1";
		const string ArmyFrom = "T80B";

		static readonly (string group, string path)[] Bones = { ("HULL", "---MESH---/HULL"), ("TURRET", "---MESH---/HULL/TURRET"), ("GUN", "---MESH---/HULL/TURRET/GUN"),
			("MANTLET_COVER", "---MESH---/HULL/TURRET/GUN") };

		internal static GameObject Visual, Armor;
		internal static DmArmour ArmourValues;
		internal static VanillaEdits Edits;
		static readonly string[] BaseOff = { "Fire Wall", "Rear Fuel tank" };
		static readonly (string piece, Matrix4x4 m)[] BaseMoved = { ("Cannon", Matrix4x4.Translate(new Vector3(0f, 0.045f, -0.185f))) };
		internal static RigMap Rig;
		internal static MaterialRoles Roles;
		internal static Vector3[] SmokePoints;
		internal static MelonPreferences_Entry<bool> Enabled;
		internal static CampaignConfig Campaign;
		internal static MelonPreferences_Entry<string> Apfsds;
		internal static MelonPreferences_Entry<bool> WeatheredPaint;
		internal static MelonPreferences_Entry<string> ArmourPreset;
		internal static MelonPreferences_Entry<bool> ScreensOn, ScreensAutomatic, ScreensTone, ScreensPage, ScreensPageAlways;
		internal static MelonPreferences_Entry<bool> KordCases;
		internal static MelonPreferences_Entry<bool> EraEffects;

		const string Repo = "marsattackk/GHPC-T90M";
		static readonly SettingsMigration[] Migrations = { };

		static readonly string[] MenuScenes = { "MainMenu2_Scene", "MainMenu2-1_Scene", "LOADER_MENU", "LOADER_INITIAL", "t64_menu", "leo2_menu" };

		public override void OnInitializeMelon()
		{
			BuildStamp.Log(Info.Name);
			UnityErrors.Install(typeof(T90MMod).Namespace);
			var cfg = MelonPreferences.CreateCategory("T-90M");
			Enabled = cfg.CreateEntry("Enabled", true, description: "Adds the T-90M as its own unit (Instant Action, campaign roster, ATAC key \"T90M\").");
			Campaign = CampaignConfig.Create(cfg, FriendlyName, 0.25f, 2);
			Apfsds = cfg.CreateEntry("APFSDS", "3BM60", description: "Default APFSDS where the game does not choose one (the mission customizer's ammo dropdown and the campaign's ammo supply override it): 3BM60 Svinets-2 (default; the round the T-90M's autoloader was made for) or 3BM42 Mango.");
			WeatheredPaint = cfg.CreateEntry("Weathered paint", true, description: "Dirt, wear and rust on the paint (default); false = clean factory paint. For T-90Ms spawned after the change.");
			ArmourPreset = cfg.CreateEntry("Armour preset", "game-scale", description: "game-scale (default: matched to GHPC's other tanks; turret 690/820, glacis 550/750 without ERA) or source-only (open-source figures; turret 575/750, glacis 500/650). For T-90Ms spawned after the change.");
			ScreensOn = cfg.CreateEntry("TShU-1-2M", true, description: "The TShU-1-2M screen system (default): laser warning heads all round, the warning on the commander's screen with a tone, and automatic 3D17 aerosol screens toward a laser from the front. false = none of it: the smoke launchers fire the game's ordinary smoke salvo. For T-90Ms spawned after the change.");
			ScreensAutomatic = cfg.CreateEntry("TShU-1-2M automatic screens", true, description: "true (default): a laser from the front fires an aerosol screen toward it by itself (automatic mode). false = semi-automatic mode: the warning only, the screen is fired by hand (vehicle menu: TShU-1-2M, or the smoke key). For T-90Ms spawned after the change.");
			ScreensTone = cfg.CreateEntry("TShU-1-2M alarm tone", true, description: "true (default): the laser warning sounds a tone. false = the screen page only, no sound. For T-90Ms spawned after the change.");
			ScreensPage = cfg.CreateEntry("TShU-1-2M warning page", true, description: "true (default): the laser warning shows the commander's screen page (the four sensor sectors, the direction, what the system did) at the bottom right. false = no page, the tone only. For T-90Ms spawned after the change.");
			ScreensPageAlways = cfg.CreateEntry("TShU-1-2M page always on screen", false, description: "false (default): the screen page comes up on a laser warning and goes away a few seconds after. true = it stays on screen the whole time you're in the T-90M, showing the grenades left and the mode between warnings. Either way, in the thermal sight, the PDT and the panorama it sits larger in the black bar right of the picture (on a widescreen), and in the day sight smaller, under the picture. For T-90Ms spawned after the change.");
			KordCases = cfg.CreateEntry("Kord spent cases", true, description: "true (default): the station's Kord MT throws its spent cases out of the chute, and they lie on the tank until it tilts or brakes hard. false = no cases (the belt still feeds). For T-90Ms spawned after the change.");
			EraEffects = cfg.CreateEntry("ERA destruction effects", true, description: "true (default): a set-off ERA container is gone with a blast, the plate round it sooted, nets over it cut. false = it looks as before (it still works as armour, once). For T-90Ms spawned after the change.");
			UpdateCheck.Config(cfg, FriendlyName, Info.Version, Repo);
			PortSettings.Finish(cfg, Info.Version, Migrations);
			if (!Enabled.Value) return;
			PortAmmo.S = Firepower.Ammo();
			AirBurst.S = Firepower.Fuse;
			NetScreens.Apply(HarmonyInstance);
			var bundle = BundleAssets.Open(Path.Combine(MelonEnvironment.ModsDirectory, "T90M", "t90m"), FriendlyName);
			Visual = BundleAssets.Prefab(bundle, "T90M_visual", FriendlyName, required: true);
			Armor = BundleAssets.Prefab(bundle, "T90M_armor", FriendlyName, required: false, ifMissing: "armour will not be changed.");
			var values = bundle != null ? bundle.LoadAsset<TextAsset>(DmArmour.AssetName) : null;
			if (values != null) ArmourValues = DmArmour.Parse(values.text);
			var edits = bundle != null ? bundle.LoadAsset<TextAsset>(VanillaEdits.AssetName) : null;
			if (edits != null) Edits = VanillaEdits.Parse(edits.text);
			EraBricks.LoadLinks(bundle != null ? bundle.LoadAsset<TextAsset>(EraBricks.LinksAsset) : null);
			EraBricks.LoadGone(bundle != null ? bundle.LoadAsset<TextAsset>(EraBricks.GoneAsset) : null);
			EraBricks.Look = () => EraEffects.Value;
			EraBricks.Vanish = true;
			EraBricks.VanishRest = n => n.StartsWith("ex_era_turret_");
			if (Edits != null) Edits.Off = Edits.Off.Union(BaseRigT72M1.ImportReplaces).Union(BaseOff).ToArray();
			if (Edits != null) Edits.Transforms = Edits.Transforms.Where(x => !BaseMoved.Any(b => b.piece == x.piece)).Concat(BaseMoved.Where(b => !Edits.Reshape.Contains(b.piece))).ToArray();
			if (Armor != null && ArmourValues == null) MelonLogger.Warning($"{FriendlyName}: bundle has no armour values; the T-72's armour stays.");
			if (Visual != null) MelonLogger.Msg($"Loaded {FriendlyName} visuals" + (Armor != null && ArmourValues != null ? " and armour." : "."));
			var rig = bundle != null ? bundle.LoadAsset<TextAsset>(RigMap.AssetName) : null;
			if (rig != null) Rig = RigMap.Parse(rig.text);
			var roles = bundle != null ? bundle.LoadAsset<TextAsset>(MaterialRoles.AssetName) : null;
			if (roles != null) Roles = MaterialRoles.Parse(roles.text);
			SmokePoints = SmokeLaunchers.Load(bundle);
			if (bundle != null && SmokePoints == null) MelonLogger.Warning($"{FriendlyName}: bundle has no smoke launch points; the smoke grenades leave from the T-72's positions.");

			var unit = new UnitDef
			{
				Key = UnitKey,
				FriendlyName = () => FriendlyName,
				BaseUnit = () => BaseUnit,
				ArmyFrom = () => ArmyFrom,
				Enabled = () => Enabled.Value,
				OnSpawn = v => Convert(v, atSpawn: true),
			};
			Campaign.Use(unit);
			UnitRegistry.Add(unit);
			UnitRegistry.TryRegister();
			ShowcaseSpawner.Add(new ShowcaseSpawner.Entry { Key = UnitKey, Name = () => FriendlyName, Anchors = new[] { BaseUnit, "T80B", "T72M1" } });
		}

		public override void OnSceneWasInitialized(int buildIndex, string sceneName)
		{
			if (Enabled.Value) UnitRegistry.TryRegister();
		}

		public override void OnSceneWasLoaded(int buildIndex, string sceneName)
		{
			if (!Enabled.Value) return;
			UpdateCheck.OnSceneLoaded(MenuScenes.Contains(sceneName));
			UnitRegistry.StartCampaignRoster();
			RepairKitCompat.TryPatch(HarmonyInstance);
			ShowcaseSpawner.OnSceneLoaded(sceneName);
			if (MenuScenes.Contains(sceneName))
				DonorParts.WarmAtMenu(FriendlyName,
					() => CrewVoices.Donor(FriendlyName),
					() => RemoteMg.Donor.Get(FriendlyName),
					() => ThermalSight.WarmDonor(FriendlyName),
					() => VanillaMaterials.Warm(Roles),
					() => ShtoraScreen.WarmDonors(ScreenSystem.Defaults));
			if (!MenuScenes.Contains(sceneName))
				StateController.RunOrDefer(GameState.PlayerReady, ConvertAll, GameStatePriority.Low);
		}

		static IEnumerator ConvertAll(GameState _)
		{
			foreach (var v in Object.FindObjectsByType<Vehicle>(FindObjectsSortMode.None))
				if (v.UniqueName == UnitKey) Convert(v);
			yield break;
		}

		class Converted : MonoBehaviour { }

		const string PaintedPrefix = "t90m_t_90m_";

		static byte[] _trimmedSkin;
		static bool _trimmedSkinRead;

		// The T-72M1's running gear is one skinned mesh with the skirts in it, so I ship a copy with them cut out.
		static byte[] TrimmedSkin()
		{
			if (_trimmedSkinRead) return _trimmedSkin;
			_trimmedSkinRead = true;
			using (var s = typeof(T90MMod).Assembly.GetManifestResourceStream("T72M1_skin_hull_trimmed.bin"))
			{
				if (s == null) { MelonLogger.Warning($"{FriendlyName}: no trimmed T-72M1 skin in the DLL; the T-72 side skirts may show through the hull."); return null; }
				var ms = new MemoryStream(); s.CopyTo(ms); _trimmedSkin = ms.ToArray();
			}
			return _trimmedSkin;
		}

		static void Convert(Vehicle vehicle, bool atSpawn = false)
		{
			if (Visual == null || vehicle == null || vehicle.GetComponent<Converted>() != null) return;
			vehicle.gameObject.AddComponent<Converted>();
			if (!atSpawn) MelonLogger.Warning($"{FriendlyName}: {vehicle.name} was converted after it spawned; it keeps the T-72M1's ammunition, and has no commander's station, modules or TShU-1-2M.");
			BaseRigT72M1.HideBaseVisuals(vehicle.transform, TrimmedSkin());
			if (BaseRigT72M1.TrimFailure != null) MelonLogger.Warning($"{FriendlyName}: {BaseRigT72M1.TrimFailure}; the T-72 side skirts may show through the hull.");
			var root = vehicle.transform;
			var turret = root.Find("---MESH---/HULL/TURRET");
			var gun = root.Find("---MESH---/HULL/TURRET/GUN");
			VisualParts.Attach(root, Visual, "T90M_", Bones);
			var cover = gun != null ? gun.Find("T90M_MANTLET_COVER") : null;
			if (cover != null) MantletCover.Skin(root, turret, gun, cover);
			else MelonLogger.Warning($"{FriendlyName}: bundle has no MANTLET_COVER; the mantlet cover stays on the turret.");
			VehiclePaint.Clean = !WeatheredPaint.Value;
			var template = BaseRigT72M1.FindTemplate(root);
			var flir = Shader.Find("Standard (FLIR)");
			foreach (var part in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("T90M_")).ToArray())
				if (part.parent == null || !part.parent.name.StartsWith("T90M_")) VehiclePaint.ApplyPaint(part.gameObject, template, flir, PaintedPrefix);
			// Moving parts keep their bare group names until Station renames them, so match on any T90M_ parent.
			VanillaMaterials.Apply(root, r => r.GetComponentsInParent<Transform>(true).Any(t => t.name.StartsWith("T90M_")), Roles);
			if (HatchUndersides.Plain(root, "t90m_hatch_inner") == 0) MelonLogger.Warning($"{FriendlyName}: no hatch underside material found (old bundle?).");
			try
			{
				HatchUndersides.Paint(root, "t90m_hatch_inner", "t90m_t_90m_turret", new RectInt(1312, 3856, 230, 110), new Vector2Int(4096, 4096));
			}
			catch (System.Exception e) { MelonLogger.Error($"{FriendlyName} hatch undersides failed (left plain): " + e); }
			try
			{
				if (turret != null) TacticalNumbers.Apply(root, turret, Numbers);
			}
			catch (System.Exception e) { MelonLogger.Error($"{FriendlyName} tactical numbers failed: " + e); }
			BaseRigT72M1.HideLeftovers(root, "T90M_");
			if (turret != null)
			{
				BaseRigT72M1.MoveEjectionMarkers(root, turret.TransformVector(new Vector3(-0.033f, 2.114f, -1.518f) - BaseRigT72M1.EjectionPortCentre));
			}
			// Before PortAxes moves the pivots, the pieces are placed in prefab space with the rig at rest.
			var volumes = Armour(vehicle);
			try { Interior.Apply(vehicle); }
			catch (System.Exception e) { MelonLogger.Error($"{FriendlyName} interior failed (left as the T-72M1's): " + e); }
			Firepower.Apply(vehicle, volumes, atSpawn);
			try
			{
				Mobility.Apply(vehicle, Drive.Settings);
			}
			catch (System.Exception e) { MelonLogger.Error($"{FriendlyName} mobility setup failed (left as the T-72M1's): " + e); }
			SmokeLaunchers.Seat(vehicle, SmokePoints);
			SmokeCaps.Apply(root);
			HatchRig.Apply(root, Rig, "T90M_", FriendlyName);
			var axes = PortAxes.FromRig(Rig, BaseRigT72M1.TurretPivot, BaseRigT72M1.GunPivot, FriendlyName);
			if (axes != null) PortAxes.Apply(vehicle, axes);
			else MelonLogger.Warning($"{FriendlyName}: bundle's rig map has no axes; the turret turns on the T-72M1's pivots.");
			// Spawn only: Unit builds its weapon and camera lists straight after, a late station never makes it in.
			if (atSpawn) try { Station.Apply(vehicle); }
			catch (System.Exception e) { MelonLogger.Error($"{FriendlyName} commander's station failed (the station stays fixed): " + e); }
			if (atSpawn) try { Modules.Build(vehicle); }
			catch (System.Exception e) { MelonLogger.Error($"{FriendlyName} modules failed (the T-72M1's kept): " + e); }
			// Left at the T-72M1's sight the laser starts inside the turret front and ranges our own ERA.
			if (atSpawn) try { Sights.PlaceLaser(vehicle); }
			catch (System.Exception e) { MelonLogger.Error($"{FriendlyName} laser placement failed (the T-72M1's kept): " + e); }
			if (atSpawn) try { Sights.PlacePeriscope(vehicle); }
			catch (System.Exception e) { MelonLogger.Error($"{FriendlyName} periscope placement failed (the T-72M1's place kept): " + e); }
			if (atSpawn) ScreenSystem.Apply(vehicle);
			try { VisualLods.Sync(root); }
			catch (System.Exception e) { MelonLogger.Error($"{FriendlyName} LOD materials failed: " + e); }
			MelonCoroutines.Start(LateSetup(vehicle, atSpawn));
		}

		static bool _armourWarned, _netsChecked;

		static Dictionary<string, (Mesh mesh, Transform bone)> Armour(Vehicle vehicle)
		{
			var volumes = new Dictionary<string, (Mesh, Transform)>();
			if (Armor == null || ArmourValues == null) return volumes;
			var root = vehicle.transform;
			var reshape = new Dictionary<string, (Mesh, Transform)>();
			foreach (var (group, bonePath, _) in DmArmourApply.T72Groups)
			{
				var g = Armor.transform.Find(group);
				var bone = root.Find(bonePath);
				if (g == null || bone == null) continue;
				foreach (Transform p in g)
				{
					if (p.name.StartsWith(VanillaEdits.ReshapeKind + "|")) reshape[p.name.Substring(p.name.IndexOf('|') + 1)] = (p.GetComponent<MeshFilter>().sharedMesh, bone);
					if (p.name.StartsWith(VanillaEdits.VolumeKind + "|")) volumes[p.name.Substring(p.name.IndexOf('|') + 1)] = (p.GetComponent<MeshFilter>().sharedMesh, bone);
				}
			}
			var missing = new List<string>();
			if (Edits != null) VanillaEditsApply.Apply(root, Edits, reshape, missing);
			var left = new List<string>();
			string preset = ArmourPreset.Value;
			var values = preset == "source-only" ? SourceOnlyValues() : ArmourValues;
			DmArmourApply.Apply(vehicle, Armor, values, FriendlyName, left, null);
			if (!_armourWarned && (missing.Count > 0 || left.Count > 0))
			{
				_armourWarned = true;
				MelonLogger.Warning($"{FriendlyName} armour: {missing.Count} T-72 armour piece(s) not found, {left.Count} piece(s) left out "
					+ $"({string.Join("; ", missing.Concat(left))}); the armour may not match the model (game update?).");
			}
			if (!_netsChecked)
			{
				_netsChecked = true;
				if (!ArmourValues.Entries.Any(e => e.Slat && e.Name != null && e.Name.StartsWith(NetScreens.NamePrefix, System.StringComparison.OrdinalIgnoreCase)))
					MelonLogger.Warning($"{FriendlyName} armour: no slat piece named \"{NetScreens.NamePrefix}...\"; the net screens act as plain slat armour.");
			}
			return volumes;
		}

		static readonly string[] GlacisFilling = { "composite_armor_hull_01_dm", "turret_05_front_dm", "turret_04_front_dm" };
		const float PackageKe = 0.7967f / 1.1652f, PackageCe = 1.4182f / 1.6508f;
		const float GlacisKe = 1.0414f / 1.3170f, GlacisCe = 1.9487f / 2.4995f;
		static DmArmour _sourceOnly;

		static DmArmour SourceOnlyValues()
		{
			if (_sourceOnly != null) return _sourceOnly;
			var d = new DmArmour();
			foreach (var e in ArmourValues.Entries)
			{
				var c = new DmArmour.Entry
				{
					Piece = e.Piece, Mode = e.Mode, Name = e.Name, Material = e.Material, Xray = e.Xray, Source = e.Source,
					Ke = e.Ke, Ce = e.Ce, KeAfter = e.KeAfter, CeAfter = e.CeAfter, AngleMatters = e.AngleMatters, Slat = e.Slat,
				};
				if (c.Variable && c.Piece.Contains("_pkg")) { c.Ke *= PackageKe; c.Ce *= PackageCe; }
				else if (c.Variable && System.Array.IndexOf(GlacisFilling, c.Piece) >= 0) { c.Ke *= GlacisKe; c.Ce *= GlacisCe; }
				d.Add(c);
			}
			return _sourceOnly = d;
		}

		static readonly InteriorMap.Embedded Interior = new InteriorMap.Embedded(typeof(T90MMod).Assembly, FriendlyName, "T90M", "T-72M1");
		static readonly PortModules Modules = new PortModules(typeof(T90MMod).Assembly, FriendlyName, "T-72M1", "T90M");

		static readonly TacticalNumbers.Settings Numbers = new TacticalNumbers.Settings
		{
			Mode = "rear lamp",
			LampFace = new Vector3(-0.475f, 2.419f - 1.369f, -1.872f + 0.527f),
			LampNormal = new Vector3(0f, 0f, -1f),
			DigitScale = 1.18f,
			DigitSpacing = 0.059f,   // two windows on the lamp, the T-72's 7.3 cm put each digit off its window
		};

		static IEnumerator LateSetup(Vehicle vehicle, bool atSpawn)
		{
			// A spawned unit's smoke launcher cached the crew before it existed and never fires without a refresh.
			yield return SmokeLaunchers.RefreshCrewLater(vehicle, FriendlyName);
			if (vehicle == null) yield break;
			try { CrewVoices.Apply(vehicle, FriendlyName); }
			catch (System.Exception e) { MelonLogger.Error($"{FriendlyName} voices failed: " + e); }
			try { Sights.Apply(vehicle); }
			catch (System.Exception e) { MelonLogger.Error($"{FriendlyName} sight setup failed (the T-72M1's sights kept): " + e); }
			try { Pdt.Apply(vehicle); }
			catch (System.Exception e) { MelonLogger.Error($"{FriendlyName} ПДТ failed: " + e); }
			try { Station.LateSetup(vehicle); }
			catch (System.Exception e) { MelonLogger.Error($"{FriendlyName} commander's station late setup failed: " + e); }
			if (atSpawn) try { Modules.Wire(vehicle); }
			catch (System.Exception e) { MelonLogger.Error($"{FriendlyName} module wiring failed: " + e); }
			try { ThermalHeat.Apply(vehicle.transform, ThermalHeat.PortParts(vehicle.transform, "T90M_")); }
			catch (System.Exception e) { MelonLogger.Error($"{FriendlyName} thermal heat failed: " + e); }
			if (atSpawn) try { PortAmmo.SelfCheck(vehicle); }
			catch (System.Exception e) { MelonLogger.Error($"{FriendlyName} ammunition self-check failed: " + e); }
		}
	}
}
