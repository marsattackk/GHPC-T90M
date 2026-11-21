# After a GHPC update

GHPC is a Unity Mono game with no obfuscation, so the mod patches its classes with Harmony and reads private members
through reflection. An update that renames or removes one of those breaks the feature that uses it and nothing else.
This page is how to find what broke and fix it.

## Start with the offline check

```
powershell -ExecutionPolicy Bypass -File tools\check-game.ps1 -GameDir "<GHPC folder>\Bin"
```

It builds the mod and looks every patch target and every `Reflect` member up in the new game's DLLs, without starting
the game. Anything gone shows as `MISSING` with the file it's used in. Names the code only knows at run time are in
`tools\check-game-list.txt`, and the script says `NOT COVERED` if the code gains one the list doesn't have.
`-OnlyProblems` prints the broken lines only. The RepairKit patch isn't checked, because it hooks another mod.

Then, in this order:

1. Build against the new `Assembly-CSharp.dll` and fix the compile errors. Those are public members and the compiler
   points at every one.
2. Fix whatever `check-game.ps1` lists.
3. Start the game and read `MelonLoader\Latest.log` from the top. Harmony errors and `Reflect` warnings come before the
   main menu.
4. Check the T-90M is in Instant Action, the mission customiser and the campaign.
5. Spawn one and read the log again. Conversion runs at spawn, so most warnings only show up here.
6. Go through the test list at the bottom.
7. Play a mission with vanilla T-72M1s and T-80Bs in it, mod on, because some patches touch every unit. Switched off,
   the mod patches nothing, so that run would tell you nothing.

## What it looks like in the log

- `Game field X.y not found: a game update may have changed it, ...` comes from `Reflect`. The word after "Game" can
  also be `method`, `property` or `property setter`. Once per session per member. `can't be read as bool (...)` means
  the field is still there but its type changed. `can't be looked up (...)` usually means a method gained an overload.
- `T90M.<class> could not be patched (a game update?), so that part of the mod is off`, then the exception: that
  class's patch target went. It's an error at startup, and the rest of the classes are patched as normal.
- `<feature> failed (the game carries on without it this time; 1 of 3 logged)` is `PatchGuard`. The patch ran and the
  code inside threw. The stack trace has the line, and it goes quiet after three.
- `Unity exception in our code: ...` is `UnityErrors`, an exception in one of the mod's behaviours or coroutines.
- `T-90M modules: ...`, `T-90M commander's station: ...`, `T-90M target tracker: ...` and the ammunition self-check
  list what they couldn't find. The ammo one ends by asking for the log. Ignore that bit.
- `donor prefab 'T80B' not in the unit registry`, `M60A3 TTS thermal donor not found` or a `vanilla material` line: a
  borrowed part couldn't be copied.
- `T-90M: bundle has no ...` isn't a game update. The bundle is old or wrong (see [bundle.md](bundle.md)).

A missing reflected member is looked up once and cached, and the feature code checks for null and skips, so a rename
shows up as a feature that's gone quiet rather than a crash. The exceptions are the `MethodDelegate`s: `TraverseLimits`
wraps `AimablePlatform.enforceEulerLimits` and `NetScreens` wraps `LiveRound.AddEvent`, and each loses its whole class
if that method goes.

What breaks most often, roughly in order: a private field or method renamed, a patched method's parameters changed, a
public member renamed (compile error), a prefab or material renamed, and last, nothing renamed but the game's behaviour
round the mod changed. That last one gives no log line at all. You only see it by playing.

## Fixing a renamed member

Open the game's `Assembly-CSharp.dll` (`GHPC_Data\Managed`) in a .NET decompiler, find the class from the warning and
look for the same kind of member with a similar type and name. Diffing the class against the previous game version is
quicker if you still have it. If a whole mechanism has gone (say `LiveRound.penCheck` is split up), the code using it
has to be rethought, not renamed.

## If GHPC moves to a new Unity version

The bundle was built with Unity 2022.3.62f2 and has to match the game's Unity version to load. If that changes, expect
`T-90M bundle could not be loaded: <path>` and no `Loaded T-90M visuals` line, and every T-90M is a plain T-72M1 under
another name. The fix is a new bundle built against [bundle.md](bundle.md). The code doesn't change.

## Where the mod hooks in

Files are in `Common\` unless they say `T90M\`. Each group ends with what you see in game when it breaks.

**Unit lists and spawning.** `UnitRegistry.cs` patches `MissionConfigMenuPanel.GetUnitKeysByClass`,
`UnitSpawner.GetMetadataByUniqueName` and `UnitSpawner.SpawnUnit`, and sets `Unit._uniqueName` and `_friendlyName`.
Broken: the T-90M is missing from a list, spawns as a plain T-72M1, or shows as T-72M1 in the HUD. `ConvertSweep.cs`
still catches a unit the spawn patch missed, but every step marked `SpawnOnly` in `T90M\Conversion.cs` is skipped and the
Arena-M kit never comes.

**Ammunition and the bustle box.** `PortAmmo.Menu.cs` patches the customiser's ammo dropdown and the campaign's
logistics, `PortAmmo.Spawn.cs` the restock check `LoadoutManager.RackHasRestockSpace`, and `PortAmmo.cs` the tandem ERA
rule on `LiveRound.penCheck`. `LoadoutManager.RegisterAllBallistics` is called after the ammo list is swapped.
`BlowoutRack.cs` patches `Compartment.DoUpdate`, `Compartment.DoBurst` and `CompartmentExit.SetFireRatioByFlameHeight`
and reads `FlammablesManager.DidBurst` and a few `AmmoRack` fields. Broken: the customiser shows the T-72M1's rounds,
rounds fly with no ballistics, the bustle box refills in a fight, or a fire in it stops the engine.

**The Telnik.** `AirBurst.cs` patches `LiveRound.DoUpdateImpl` and reads the round's ranged fuse fields to burst it in
the air. Its patches on `FireControlSystem` and `BallisticComputerRepository.GetSuperelevation` lift the aim for "over
the target". `FragmentCone.cs` sets the fragments' penetration again after `LiveRound.Init` resets it. The fuse menu,
like every vehicle menu entry here, hangs on `PlayerInput.AddSmokeButtons`. Broken: every Telnik goes off on impact,
bursts with no effect drawn, or "over" aims at the target itself.

**Gun, autoloader and voice.** `GunSetup.cs` rescales `AmmoFeed`'s reload stages and copies the gun's codex entry before
renaming it, because every T-72M1 shares it. `CrewVoices.cs` sets fields on the copied T-80B voice and lets one "AZ"
callout through per carousel turn. Broken: the T-72M1's reload time, every T-72M1 named after the T-90M's gun, a German
crew, or "AZ" several times per reload.

**Gunner's sight and fire control.** `GunnerFcs.cs` patches `FireControlSystem` (`AdjustAimByRatio`, `Update`,
`FiringBlocked`, `CompensatedWeaponVector`, `GetLeadWorldOffset`, `UpdateLaserAim`, `DoLase`). Its `Resync` rewrites the
fields the FCS caches in Awake and DeferredStart, and those names are in the list file. It also hooks in ahead of
`GHPC.Rendering.Instancing.InstancedMeshRenderer.LateUpdate`, found by name. `ThermalSight.cs` handles the night toggle
through `PlayerInput.Update` and the camera. Broken: the sight swings past its stops, the gun fires before it's laid,
reticles don't draw, grass and trees drop out of the sight while it moves, or the night key does nothing.

**Target tracker.** `TargetTracker.cs` patches `FireControlSystem.AdjustAimByRatio` for your nudges inside the frame,
`AmmoFeed.SetNextClipType` and `CameraSlot.ToggleFLIRPolarity`, and lays the aim before the FCS's own `LateUpdate`
reads it for the lead. The laying loop is `TrackServo`, which has its own tests. Broken: no ready frame, a lock that
won't hold or overshoots, or the lead lagging a frame behind the target.

**Commander's station and the Kord.** The station is cloned from a T-64B's. `RemoteMg.cs` patches `PlayerInput.Update`,
the fire and aim methods of `CrewBrainWeaponsModule` (picked in code) and the `CameraManager` slot methods, and writes
the unit's camera slots, platforms and statuses. `T90M\Commander.Link.cs` patches `CrewBrainWeaponsModule.SetAimVector` for the
Aim Override hand-over and the `WeaponSystem.AbleToFire` getter for the blind sector. Broken: free-look fires the Kord
at the cursor, the station has no view or no ammo, Aim Override swings the Kord at the AI's target instead of laying the
gun, or the Kord fires over the gunner's sight head.

**TShU-1-2M and smoke.** `ShtoraScreen.*.cs` patch `FireControlSystem.DoLase`, `UnitAI.SetTarget`, the vehicle menu and
`WeaponHud.Update`, and read `VehicleSmokeManager` fields to fire the 3D17s. `SmokeLaunchers.cs` patches
`VehicleSmokeManager.FireSmokes` to rebuild the crew lists a runtime spawn never got. Broken: no warning page, no
screen, or the launchers never fire.

**Arena-M kit.** Off by default, so a break here goes unnoticed unless you switch it on. `ActiveProtection.cs` patches
two methods by hand once the first kit tank sets up, not through `[HarmonyPatch]`: `LiveRoundBatchHandler.Update`, where
it reads `_liveRounds` to see every round in flight, and `PlayerInput.AddSmokeButtons`, where it adds the **Arena-M**
entries through `PlayerInput.AddMenuButton`. When it kills a threat it sets `LiveRound._armed` or `_failedToDetonate`
before `Detonate`, and calls `LiveRound.createExplosion` for the warhead's own fragments. The weapon info line rides
`WeaponHud.Update` through the screen system's patch. What goes on the tank is mod data: `Resources\kits.txt` for the
parts, the hidden objects and the armour groups (format in [bundle.md](bundle.md)), and the modules marked
`"kit": "arena"` in `modules_built.json`. Past those, a game update reaches it through the module and armour code the
plain tank uses as well, and the bustle box's path `T-72 TURRET COLLIDERS/SPARE AMMO RACK`, which the kit empties with
`BlowoutRack.Remove`. Broken: `APS: GHPC's live-round list not found` and the kit never fires, `APS watch failed` from
`PatchGuard` when a round field has gone, threats blowing up with no fragments, or no Arena-M entry in the vehicle menu
(no switching, no reload).

**Armour, ERA and nets.** `EraBricks.cs` patches `LiveRound.penCheck` and `UniformArmor.Detonate()` by hand, both or
neither, and logs `ERA bricks: patching failed` if it can't. `DmArmourApply.cs` patches `penCheck` too and sets
`UniformArmor._armorType`, `_isEra`, `_isSlatArmor` and friends. `NetScreens.cs` patches `penCheck` with a transpiler
that swaps GHPC's 0.65 slat chance for its own. `check-game.ps1` can't see that number, so if GHPC changes it the log
says `Net screens: GHPC's slat chance not found once`. Broken: ERA behaves as GHPC's own, every piece takes the game's
default armour material (the biggest effect of anything here), or the nets act as plain slats.

**Rig, modules and heat.** The moving parts use `LateFollow` shifts and `AimablePlatform._originalLocalRotation`, and
`TiltedRing.cs` patches `AimablePlatform.MoveTransformToMatchVirtualRotation` and `ShotInfo.PopulateUnitPoses`.
`ModuleBuilder.cs` sets `DestructibleComponent` and `UniformArmor` fields, `ThermalHeat.cs` writes `HeatSource.heat` and
`Mobility.cs` the top speeds. Broken: parts displaced or crooked, wrong AAR poses, modules with the wrong health, parts
black in white-hot, the T-72M1's speeds.

**Other mods.** `RepairKitCompat.cs` patches RepairKit's `Apply(Unit, NwhChassis)` when that mod is installed. A GHPC
update doesn't touch it.

## Names looked up as strings

`tools\check-game.ps1` reads the literal names in `Reflect.*`, `AccessTools.Method`, `Traverse.Create(x).Field`,
`Type.GetField` / `GetMethod` and the hit-zone table of `ModuleBuilder`. It can't check field enumerations (`GetFields`,
for instance the `AmmoType` copies in `PortAmmo` and `FragmentCone`, or `RemoteMg.Platform`) or a lookup whose name is a
variable: after a game update, read those against the class by hand. The script prints the full list of where they are.
The names below don't go through any of those calls, so a rename gives no warning of that shape either.

- **T-72M1 rig** (`BaseRigT72M1.cs`): the `---MESH---/HULL/TURRET/GUN` bones, the renderers it hides, and the hull
  skin, which is swapped for `Resources\T72M1_skin_hull_trimmed.bin` and has to match the game's vertex count. If GHPC
  changes that mesh the log says `bones collapsed only`.
- **The T-72M1's armour and interior**: the pieces War Thunder's model replaces (`BaseRigT72M1.ImportReplaces`, plus
  `BaseOff` in `T90M\Armour.cs`), and the collider paths in `Resources\interior_map.txt` and the bustle box's
  `T-72 TURRET COLLIDERS/SPARE AMMO RACK` (`T90M\Firepower.cs`). Some names carry GHPC's own trailing space. Keep it.
- **Donors**: units `T80B`, `M60A3TTS`, a T-64B (`RemoteMg.Donor` takes the best unit whose name starts `T64`), the
  white smoke donors in `ShtoraScreen.Settings` and the units `material_roles` names. `DonorParts.cs` copies them at
  the main menu, because VanillaAssetUtil can destroy the prefabs after a mission.
- **Ammunition** (`T90M\Firepower.cs`, `Common\Heat125.cs`): clips `clip_3BM32`, `clip_9M112M`, `clip_3OF26` and
  `clip_3BK18M` from the T-80B's list. All three HEAT rounds are copies of `clip_3BK18M`. The 3BK31's ERA cost goes
  through `LiveRound.RhaPenetrationOverride`, and built-in ERA is told apart by the armour's name (Kontakt-5, Relikt):
  if another mod renames its blocks, 3BK29M treats them as add-on.
- **3BK31's second jet** (`Common\SplitCharge.cs`): a postfix on `LiveRound.logHit` (reads `_jetActive`,
  `_jetStartPoint`, sets `_armed`) launches it as a child round, and a postfix on `AarController.createShotTrace` (its
  `_shotTraces`) draws it as a jet. If `logHit` goes, 3BK31 is back to one jet. If the trace list goes, the second jet
  shows as fragment lines.
- **Tandem look** (`Common\TandemLook.cs`): `LiveRound.doImpactVFX` and `ParticleEffectsManager.CreateImpactEffectOfType`
  hold back a tandem round's impact effect for the precursor's pop (`_fusedStatus`, `_materialHit`, `HeatJet`), and
  `LiveRound.logHit` adds the precursor's recap line, coloured through `ShotTraceVisual.jetColor`. If those move, the
  rounds just look like plain HEAT again.
- **Fonts and materials**: `Soviet7SegmentFont SDF` and `Reticle LiberationSans SDF` (named in the reticle files), fonts
  starting `TPD_Etch`, `Standard (FLIR)`, and `see-through teal gray` for the x-ray (no log line if that one goes).
- **Scenes and input**: the menu scenes in `MenuScenes.cs`, `TR01_showcase`, and the buttons `Toggle Night Sight`,
  `Smoke Grenades` and `Aim Override`. A renamed button silently does nothing.

## Test list after a repair

1. Start the game and read the log from the top: no `Game field`, no Harmony error.
2. T-90M in Instant Action and the customiser. Spawn one and read the log.
3. Drive, traverse, elevate. Lase and fire. One "AZ" callout per reload.
4. The thermal, the tracker (lock, coast behind smoke, drop on a shot) and the ПДТ.
5. A Telnik on each fuse setting, lased first.
6. The panorama: the Kord moves and fires, the camera cycles through the slots, Dubl lays the gun, Aim Override hands a
   locked target to the gunner.
7. TShU-1-2M: lase it from the front, check the page, tone and screen, then the smoke key.
8. Shoot it with an APFSDS, a HEAT and a missile and read the damage report. Then a HEAT grenade at the nets and a shot
   into the bustle box.
9. Arena-M: set `Arena-M kit on my tank` to true and start a mission. The console should give the `Arena-M:` line with
   your tank as yes. Have an RPG or a missile fired at you: two rounds go up and the box counts in the weapon info drop.
   Switch it off and on from the vehicle menu and reload the empty tubes there.
10. Traverse, then watch the AAR replay.
11. A vanilla T-72M1 and T-80B in the same mission, untouched.
