namespace T90M
{
	// The settings' descriptions in MelonPreferences.cfg, in the order Mod.CreateSettings creates them. Static fields, not
	// consts: tools\check-settings.ps1 reads every const as a possible default. AfterChange stays on top: static
	// fields start in the order they are written, so a text that ends in it needs it set first.
	internal static class SettingText
	{
		static readonly string AfterChange = " For T-90Ms spawned after the change.";

		internal static readonly string Enabled =
			"Adds the T-90M as its own unit (Instant Action, campaign roster, ATAC key \"T90M\").";

		internal static readonly string Apfsds =
			"Default APFSDS where the game does not choose one (the mission customizer's ammo dropdown and the"
			+ " campaign's ammo supply override it): 3BM60 Svinets-2 (default; the round the T-90M's autoloader was"
			+ " made for) or 3BM42 Mango.";

		internal static readonly string FireWhenLaid =
			"true (default): after a lase the trigger does nothing until the gun has climbed to the elevation the"
			+ " computer set for that range, as the real firing-permission circuit has it (the ready lamp comes on when"
			+ " it is there); from the gunner's sight and the commander's panorama alike. false = the gun fires at any"
			+ " time, as the game's own tanks do; a shot while it is still climbing after a lase falls short."
			+ AfterChange;

		internal static readonly string TrackerOn =
			"true (default): the automatic target tracker of the gunner's sight and the commander's panorama. Hold"
			+ " the cross on a target in the narrow thermal field (the panorama: also its narrow TV field) until the"
			+ " ready frame shows, then lase (APFSDS, HEAT or missile; in the panorama any) or press the tracker key:"
			+ " the sight stays on the target; small aiming inputs move the cross within the frame. The key again, a"
			+ " shot from the gun, a field change or aiming away past the frame drops it. false = no tracker. Takes"
			+ " effect at once.";

		internal static readonly string TrackerKey =
			"The tracker's capture / release key, a Unity key name: Mouse2 (default) is the middle mouse button; e.g."
			+ " Y, H, U, Mouse3, Mouse4, or None for lasing only. Pick a key the game doesn't use (T is the night"
			+ " sight, G smoke).";

		internal static readonly string TrackerDayLock =
			"false (default): as on the real sight, a target is picked and locked only in the narrow thermal field."
			+ " true = also in the day sight (a convenience: the real tracker works on the thermal picture)."
			+ AfterChange;

		internal static readonly string ScreensOn =
			"The TShU-1-2M screen system (default): laser warning heads all round, the warning on the commander's"
			+ " screen with a tone, and automatic 3D17 aerosol screens toward a laser from the front. false = none of"
			+ " it: the smoke launchers fire the game's ordinary smoke salvo."
			+ AfterChange;

		internal static readonly string ScreensAutomatic =
			"true (default): a laser from the front fires an aerosol screen toward it by itself (automatic mode)."
			+ " false = semi-automatic mode: the warning and the page only, the screen is fired by hand (vehicle menu: TShU-1-2M, or"
			+ " the smoke key)."
			+ AfterChange;

		internal static readonly string ScreensTone =
			"true (default): the laser warning sounds a tone with the warning page. false = the page only, no sound."
			+ AfterChange;

		internal static readonly string ScreensPage =
			"true (default): the laser warning shows the commander's screen page (the four sensor sectors, the"
			+ " direction, what the system did) at the bottom right. false = no page, the tone only."
			+ AfterChange;

		internal static readonly string ScreensPageAlways =
			"false (default): the screen page comes up on a laser warning and goes away a few seconds after. true ="
			+ " it stays on screen the whole time you're in the T-90M, showing the grenades left and the mode between"
			+ " warnings. Either way, in the thermal sight, the PDT and the panorama it sits larger in the black bar"
			+ " right of the picture (on a widescreen), and in the day sight smaller, under the picture."
			+ AfterChange;

		internal static readonly string KordCases =
			"true (default): the Kord throws its spent cases out of the chute, and they lie on the tank"
			+ " until it tilts or brakes hard. false = no cases (the belt still feeds)."
			+ AfterChange;

		internal static readonly string EraEffects =
			"true (default): a set-off ERA container is gone with a blast, the plate round it sooted, nets over it"
			+ " cut. false = a set-off ERA container looks as before (it still works as armour, once)."
			+ AfterChange;

		internal static readonly string ArmourPreset =
			"game-scale (default: matched to GHPC's other tanks; turret 690/820, glacis 550/750 without ERA) or"
			+ " source-only (open-source figures; turret 575/750, glacis 500/650)."
			+ AfterChange;
	}
}
