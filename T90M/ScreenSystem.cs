using System;
using GHPC.Vehicle;
using MelonLoader;
using T90M.Common;
using UnityEngine;

namespace T90M
{
	internal static class ScreenSystem
	{
		internal static readonly ShtoraScreen.Settings Defaults = new ShtoraScreen.Settings
		{
			Tag = T90MMod.FriendlyName,
			PreciseHalfAz = 45f, PreciseStep = 3.75f, HeadEl = new Vector2(-5f, 25f),
			Automatic = true,
			TubeHeight = 2.05f,
			Display = ShtoraScreen.Look.ScreenPage, PageTitle = "СПЗ ТШУ-1-2М",
			MenuName = "TShU-1-2M",
		};

		internal static void Apply(Vehicle vehicle)
		{
			// Off just means no station. Every ShtoraScreen patch does nothing without one and the tubes fire GHPC's own salvo.
			bool on = T90MMod.ScreensOn.Value;
			if (!on) return;
			var s = Defaults.Copy();
			s.Automatic = T90MMod.ScreensAutomatic.Value;
			s.AlarmTone = T90MMod.ScreensTone.Value;
			bool page = T90MMod.ScreensPage.Value;
			bool pageAlways = T90MMod.ScreensPageAlways.Value;
			s.Display = page ? ShtoraScreen.Look.ScreenPage : ShtoraScreen.Look.ControlPanel;
			// The page plays its own tone, the Hud's only sounds when there's no page, so never both.
			s.PageTone = s.AlarmTone && page;
			s.AlarmTone = s.AlarmTone && !page;
			s.PanelShown = false;
			s.PanelAlways = page && pageAlways;
			try { ShtoraScreen.Apply(vehicle, s); }
			catch (Exception e) { MelonLogger.Error($"{T90MMod.FriendlyName} TShU-1-2M screens failed: " + e); }
		}
	}
}
