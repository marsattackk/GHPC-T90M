using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using T90M.Common;
using UnityEngine;

[assembly: MelonInfo(typeof(T90M.T90MMod), "T-90M", "0.3.1", "marsattackk")]
[assembly: MelonGame("Radian Simulations LLC", "GHPC")]

namespace T90M
{
	// Loads the bundle, registers the unit on the T-72M1 prefab and hangs the War Thunder model's HULL / TURRET / GUN on the base
	// rig's bones (the base's visuals off, paint, real axes). The armour goes on before the axes move; the commander's station,
	// the bustle box, the modules and TShU-1-2M are fitted at spawn, the sights, ПДТ and tracker once the unit has started
	// (Conversion.Convert).
	public partial class T90MMod : MelonMod
	{
		internal const string UnitKey = "T90M";

		internal static GameObject Visual;   // the War Thunder model, the bundle's T90M_visual prefab
		internal static VanillaEdits Edits;   // the T-72M1's pieces switched off or changed (Resources\vanilla_edits.txt)
		internal static MaterialRoles Roles;   // material roles: the vanilla glass at spawn (Resources\material_roles.txt)
		internal static RigMap Rig;   // moving parts: hatches, the turret and gun axes (Resources\rig_map.txt)
		internal static Vector3[] SmokePoints;   // the smoke tubes' launch points (Resources\smoke_points.txt)

		internal const string FriendlyName = "T-90M";
		const string BaseUnit = "T72M1";   // registry key of the vanilla prefab "T72M1" (crew, interior, damage model, running gear)
		// The T-72M1 is NVA in GHPC, so the army comes off the T-80B.
		const string ArmyFrom = "T80B";
		internal const int CoaxRounds = 2000;   // the T-90M's own table («Описание танка Т-90М»)
		internal const string NumberPosition = "rear lamp";

		internal static MelonPreferences_Entry<bool> Enabled;
		internal static CampaignConfig Campaign;
		internal static MelonPreferences_Entry<string> Apfsds;
		internal static MelonPreferences_Entry<bool> ForceApfsds;
		internal static MelonPreferences_Entry<bool> WeatheredPaint;
		internal static MelonPreferences_Entry<bool> ScreensOn;
		internal static MelonPreferences_Entry<bool> ScreensAutomatic;
		internal static MelonPreferences_Entry<bool> ScreensTone;
		internal static MelonPreferences_Entry<bool> ScreensPage;
		internal static MelonPreferences_Entry<bool> ScreensPageAlways;
		internal static MelonPreferences_Entry<bool> KordCases;
		internal static MelonPreferences_Entry<bool> EraEffects;
		internal static MelonPreferences_Entry<bool> FireWhenLaid;
		internal static MelonPreferences_Entry<bool> TrackerOn;
		internal static MelonPreferences_Entry<string> TrackerKey;
		internal static MelonPreferences_Entry<bool> TrackerDayLock;

		const string Repo = "marsattackk/GHPC-T90M";
		// A changed default needs a rule here. MelonLoader keeps the saved one. None yet.
		static readonly SettingsMigration[] Migrations = { };

		public override void OnInitializeMelon()
		{
			BuildStamp.Log(Info.Name);
			UnityErrors.Install(typeof(T90MMod).Namespace);
			CreateSettings();
			if (!Enabled.Value) return;
			PortAmmo.S = Firepower.Settings();
			AirBurst.S = Firepower.Fuse;
			NetScreens.Apply(HarmonyInstance);
			LoadData();
			RegisterUnit();
		}

		// The settings in MelonPreferences.cfg, then the file's version and its migrations.
		void CreateSettings()
		{
			var cfg = MelonPreferences.CreateCategory("T-90M");
			Enabled = cfg.CreateEntry(
				"Enabled",
				true,
				description: SettingText.Enabled);
			// The T-90M is the newer and rarer tank, so it shows up half as often as the T-90A (0.25 against 0.5).
			Campaign = CampaignConfig.Create(cfg, FriendlyName, 0.25f, 2);
			Apfsds = cfg.CreateEntry(
				"APFSDS",
				"3BM60",
				description: SettingText.Apfsds);
			ForceApfsds = cfg.CreateEntry(
				"Force APFSDS",
				false,
				description: SettingText.ForceApfsds);
			FireWhenLaid = cfg.CreateEntry(
				"Fire only when the gun is laid",
				true,
				description: SettingText.FireWhenLaid);
			TrackerOn = cfg.CreateEntry(
				"Target tracker",
				true,
				description: SettingText.TrackerOn);
			TrackerKey = cfg.CreateEntry(
				"Target tracker key",
				Tracker.DefaultKey.ToString(),
				description: SettingText.TrackerKey);
			TrackerDayLock = cfg.CreateEntry(
				"Target tracker from the day sight",
				false,
				description: SettingText.TrackerDayLock);
			WeatheredPaint = PaintFinish.Create(cfg, "T-90M");
			ScreensOn = cfg.CreateEntry(
				"TShU-1-2M",
				true,
				description: SettingText.ScreensOn);
			ScreensAutomatic = cfg.CreateEntry(
				"TShU-1-2M automatic screens",
				true,
				description: SettingText.ScreensAutomatic);
			ScreensTone = cfg.CreateEntry(
				"TShU-1-2M alarm tone",
				true,
				description: SettingText.ScreensTone);
			ScreensPage = cfg.CreateEntry(
				"TShU-1-2M warning page",
				true,
				description: SettingText.ScreensPage);
			ScreensPageAlways = cfg.CreateEntry(
				"TShU-1-2M page always on screen",
				false,
				description: SettingText.ScreensPageAlways);
			KordCases = cfg.CreateEntry(
				"Kord spent cases",
				true,
				description: SettingText.KordCases);
			// On by default on the T-90M; off by default on the T-90A, as it has always shipped.
			EraEffects = cfg.CreateEntry(
				"ERA destruction effects",
				true,
				description: SettingText.EraEffects);
			Armour.Config(cfg);
			UpdateCheck.Config(cfg, FriendlyName, Info.Version, Repo);
			DevInit(cfg);
			PortSettings.Finish(cfg, Info.Version, Migrations);
		}

		// The bundle's prefabs and the text data in Resources\ (armour, base edits, rig, glass, ERA, smoke points).
		static void LoadData()
		{
			var bundle = BundleAssets.Open(Path.Combine(MelonEnvironment.ModsDirectory, "T90M", "t90m"), FriendlyName);
			Visual = BundleAssets.Prefab(bundle, "T90M_visual", FriendlyName, required: true);
			Armour.Load(bundle);
			EraBricks.LoadLinks(bundle);
			EraBricks.LoadGone(bundle);
			EraBricks.Look = () => EraEffects.Value;
			EraBricks.Vanish = true;
			EraBricks.VanishRest = n => n.StartsWith("ex_era_turret_");
			if (Visual != null)
				MelonLogger.Msg($"Loaded {FriendlyName} visuals" + (Armour.Prefab != null && Armour.Values != null ? " and armour." : "."));
			var rig = PortText.Load(bundle, RigMap.AssetName);
			if (rig != null) Rig = RigMap.Parse(rig);
			else MelonLogger.Warning($"{FriendlyName}: no rig map; the hatches stay closed.");
			var roles = PortText.Load(bundle, MaterialRoles.AssetName);
			if (roles != null) Roles = MaterialRoles.Parse(roles);
			else MelonLogger.Warning($"{FriendlyName}: no material roles; glass keeps the bundle's own materials.");
			SmokePoints = SmokeLaunchers.Load(bundle);
			if (bundle != null && SmokePoints == null)
				MelonLogger.Warning($"{FriendlyName}: no smoke launch points; the smoke grenades leave from the T-72's positions.");
			DevLoadData(bundle);
		}

		// The T-90M as a unit of its own: the game's unit lists, the campaign roster and the Showcase.
		static void RegisterUnit()
		{
			var unit = new UnitDef
			{
				Key = UnitKey,
				FriendlyName = () => FriendlyName,
				BaseUnit = () => BaseUnit,
				ArmyFrom = () => ArmyFrom,
				Enabled = () => Enabled.Value,
				OnSpawn = v => Conversion.Convert(v, atSpawn: true),
			};
			Campaign.Use(unit);
			UnitRegistry.Add(unit);
			UnitRegistry.TryRegister();
			ShowcaseSpawner.Add(new ShowcaseSpawner.Entry
			{
				Key = UnitKey,
				Name = () => FriendlyName,
				Anchors = new[] { BaseUnit, "T80B" }
			});
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
					() => ShtoraScreen.WarmDonors(Countermeasures.Screens));
			DevSceneLoaded(sceneName, MenuScenes.Contains(sceneName), HarmonyInstance);
			if (!MenuScenes.Contains(sceneName))
				ConvertSweep.AtPlayerReady(UnitKey, v => Conversion.Convert(v));
		}

		// Dev hooks: optional methods the author's own test harness fills in. They compile to nothing in a normal build.
		static partial void DevInit(MelonPreferences_Category cfg);

		static partial void DevLoadData(AssetBundle bundle);

		static partial void DevSceneLoaded(string sceneName, bool menu, HarmonyLib.Harmony harmony);
	}
}
