using GHPC.Vehicle;
using T90M.Common;
using UnityEngine;

namespace T90M
{
	// TShU-1-2M laser warning and smoke screen, on ShtoraScreen's code.
	internal static partial class Countermeasures
	{
		internal static readonly ShtoraScreen.Settings Screens = new ShtoraScreen.Settings
		{
			Tag = T90MMod.FriendlyName,
			PreciseHalfAz = 45f,
			PreciseStep = 3.75f,
			HeadEl = new Vector2(-5f, 25f),
			Automatic = true,
			TubeHeight = 2.05f,
			Display = ShtoraScreen.Look.ScreenPage,
			PageTitle = "СПЗ ТШУ-1-2М",
			MenuName = "TShU-1-2M",
		};

		static readonly ConversionStep<Vehicle>[] Steps =
		{
			new ConversionStep<Vehicle> { What = "TShU-1-2M screens", Run = FitScreens },
		};

		internal static void Apply(Vehicle vehicle)
		{
			// Off just means no station. Every ShtoraScreen patch does nothing without one and the tubes fire GHPC's own salvo.
			bool on = T90MMod.ScreensOn.Value;
			DevPreview(ref on);
			if (!on) return;
			ConversionSteps.Run(T90MMod.FriendlyName, Steps, vehicle, vehicle.name, atSpawn: true);
		}

		static void FitScreens(Vehicle vehicle)
		{
			var settings = Screens.Copy();
			settings.Automatic = T90MMod.ScreensAutomatic.Value;
			settings.AlarmTone = T90MMod.ScreensTone.Value;
			bool page = T90MMod.ScreensPage.Value;
			bool pageAlways = T90MMod.ScreensPageAlways.Value;
			DevPreview(settings, ref page, ref pageAlways);
			settings.Display = page ? ShtoraScreen.Look.ScreenPage : ShtoraScreen.Look.ControlPanel;
			// The page plays its own tone and the Hud's only sounds when there's no page, so never both.
			settings.PageTone = settings.AlarmTone && page;
			settings.AlarmTone = settings.AlarmTone && !page;
			settings.PanelShown = false;
			settings.PanelAlways = page && pageAlways;
			ShtoraScreen.Apply(vehicle, settings);
		}

		// Dev hooks: optional methods the author's own test harness fills in. They compile to nothing in a normal build.
		static partial void DevPreview(ref bool on);

		static partial void DevPreview(ShtoraScreen.Settings screens, ref bool page, ref bool pageAlways);
	}
}
