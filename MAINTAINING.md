# Maintaining the T-90M mod

For someone who knows C# but hasn't done GHPC modding. The mod is a MelonLoader DLL that adds the T-90M as its own
unit: a copy of the game's T-72M1 that gets converted when it spawns. Model, textures and armour meshes come from an
asset bundle, everything else is the code and data here.

If the log fills with warnings after a game update, go to [docs/game-updates.md](docs/game-updates.md).

## Building

You need the .NET SDK (6 or newer, online once for the 4.7.2 reference assemblies) and GHPC with MelonLoader 0.7.3 or
newer, because the project references the game's DLLs. Everything here is meant for Windows: the game is Windows only
and so are the PowerShell scripts under `tools\`. Start the game once after installing MelonLoader so it sets itself up:
`tools\check-game.ps1` reads its Mono.Cecil.

```
dotnet build -c Release -p:GameDir="<GHPC folder>\Bin"
```

`GameDir` is the folder with `GHPC.exe` and defaults to the usual Steam path. The DLL ends up in
`bin\Release\T90M.dll`. Copy it to `Mods\T90M.dll` in the game. The bundle (model, textures, armour meshes) is not in this
repo: take `Mods\T90M\t90m` from the latest [release zip](https://github.com/marsattackk/GHPC-T90M/releases) and put it at
`Mods\T90M\t90m`. Without it the mod loads but the T-90M has no model.

The DLL logs a stamp when it loads, like `T-90M 0.2.2+577257a.public, game <version>, Steam build <number>`, so a
player's log tells you which build they ran. The version comes from `MelonInfo` in `T90M\Mod.cs`, the commit from
`-p:GitCommit=<hash>` (otherwise `unknown`), the flavour from `-p:BuildFlavour` (default `public`).

## Tests

`tests\` covers the bits that don't need the game: the reticle files' expression language, the reticle JSON, MiniJson,
`dm_armour` parsing, the bundle text numbers, version comparison, the settings file read, the fire gate, the aim clamp
and the target tracker's laying loop. It compiles those files straight out of `Common\` with stand-ins for
MelonLoader, so it runs without GHPC and GitHub Actions runs it on every push. It also reads what ships in `Resources\`:
`ShippedTextTests.cs` takes `dm_armour.txt` and fails on a line that doesn't read, a mode or material the damage model
doesn't know, or a piece count other than the one at its top (change `Pieces` there when you add or remove a piece on
purpose), and `ShippedReticleTests.cs` does the reticles. CI never builds the mod itself, because
that needs the game's DLLs, so build it against your own copy and run `tools\check-game.ps1` before a pull request.

```
dotnet test tests
```

The two checks run from the repo's folder in PowerShell. `check-game.ps1` reads the game's DLLs, so it needs the game with
MelonLoader started once (it uses MelonLoader's Mono.Cecil); it builds the mod itself into a temporary folder, so it needs
the .NET SDK too. If the game has never been started it stops with "No Mono.Cecil.dll under <GHPC folder>\Bin\MelonLoader.
Start the game once so MelonLoader sets itself up." `check-settings.ps1` only reads the sources and the two tables.

```
powershell -ExecutionPolicy Bypass -File tools\check-game.ps1 -GameDir "<GHPC folder>\Bin"
powershell -ExecutionPolicy Bypass -File tools\check-settings.ps1
```

A second workflow runs `tools\check-settings.ps1` on every push and pull request, Markdown-only ones included, because
the tests skip those and a README edit is the usual way for the settings tables to drift from the code.

The reticle language is described in [docs/reticles.md](docs/reticles.md), and `ExpressionTests.cs` pins it: read both
before changing `ReticleSpec.Expressions.cs`. Every reticle in `Resources\` is also expanded and compared line by line
with `tests\expanded\`. After changing a reticle on purpose, set `T90M_UPDATE_EXPANDED=1`, run the tests once and check
the diff is what you meant.

## Layout

```
T90M\       the tank itself
Common\     the stamped, sealed code shared with my other ports
Resources\  data embedded in the DLL, file by file in its README.md
tools\      check-game.ps1, check-settings.ps1, verify-release.ps1
tests\      the tests above, a project of their own
docs\       game-updates.md, bundle.md, reticles.md and shared-features.md for you; armour.md, screens.md and details.md for
            players; the images
```

`T90M\` is small, one file per job (`<Job>.<Part>.cs` when a job has parts). `Mod.cs` is the entry point: the settings,
the data load, the unit registration and the scene hooks. `Conversion.cs` does the converting (`Convert`, then
`LateSetup` a moment later) and `Armour` holds the armour. The rest is one file per system: `Firepower` (gun,
autoloader, rounds, the 3OF82's fuse), `Sights` (the gunner's Sosna-U and its laser; `Pdt`, in `Sights.Pdt.cs`, is the
back-up TV sight and `Tracker`, in `Sights.Tracker.cs`, the target tracker in both sights), `Commander` (the remote Kord
and the PK PAN panorama; its optics are in `Commander.Sight.cs`, its behaviour and patches in `Commander.Link.cs`),
`Countermeasures` (TShU-1-2M; the Arena-M kit is `Arena`, in `Countermeasures.Arena.cs`), `Drive` (engine and gearbox) and
`Reticles` (the one reticle loader), with `SettingText`
holding the settings' descriptions.

Where to change things:

| What | Where |
|---|---|
| Tunable numbers | `Firepower.cs` (gun, rounds, coax belt, the 3OF82's fuse), `Mod.cs` (coax rounds), `Commander.cs` (Kord), `Drive.cs` (engine and gears), `Countermeasures.Arena.cs` (the Arena-M's blocks, radar arcs and ranges, the kit's mass) |
| A setting's description | `SettingText.cs`, except three that live in `Common`: `Weathered paint` (`PaintFinish.cs`), `Campaign prevalence` (`UnitRegistry.cs`) and `Check for updates` (`UpdateCheck.cs`) |
| Armour values, hatches, glass, ERA, smoke points | the text files in `Resources\` |

Every method named `Dev*` is an empty partial method (`static partial void Dev...`) that my own test harness fills in.
Nothing here implements them, so the compiler removes each declaration and every call to it, and a normal build contains
nothing of them. List them with `Select-String -Path T90M\*.cs -Pattern "partial void Dev"`. Ignore them, or delete a
declaration with its calls and, for a hook with a `ref` argument, the local it sets, and nothing changes.

`Common\` is the machinery, fed by settings objects from `T90M\`, so the T-90M's numbers stay in `T90M\`. It is shared by
my GHPC mods and stamped into each repo under the `T90M.Common` namespace: the same file wherever it appears, each tank
getting the subset it needs, so it also holds features the T-90M doesn't switch on
([docs/shared-features.md](docs/shared-features.md) lists them). `Common\STAMP.txt` seals it: `CommonSealTests` fails,
naming the file, when one differs. So `Common\` is never edited here. To change it, open an issue or a pull request on this
repo describing the change; I apply it to the shared copy and stamp every mod again, so a fix reaches my other mods without
anybody porting it by hand.

One trade-off comes with that. Each port compiles `Common\` into its own DLL under its own namespace, so a player who
installs the T-90A, T-90M and T-80UM2 together gets one copy of every Harmony patch per port, for example three on
`FireControlSystem.Update`, `PlayerInput.Update` and `WeaponHud.Update`. A copy only acts on its own vehicles and
returns early when the instance isn't one of them, so what the extra copies cost is a lookup per call. The few things
the copies have to share go through `AppDomain` data under a versioned key: the screen system's lasing, blinded and
HUD-lift tables (`ShtoraScreen.Laser.cs`, `ShtoraScreen.Menu.cs`, keys like `"GHPCMods.ShtoraScreen.Lasing.v2"`) and the
Unity error sinks in `UnityErrors.cs`. They hold only game and mscorlib types, because a type from one DLL isn't the
same type to another. If you change the shape of one, bump the version suffix in every port. A port that finds another
shape under its key logs a warning and keeps a table of its own, and the ports stop sharing.

`Resources\` is embedded under each file's plain name (one wildcard line in `T90M.csproj`, its README left out) and read
with `GetManifestResourceStream`: the reticles (`*.jsonc`), round textures and meshes, the screen page art and alarm,
the module, interior and gun depression data, the trimmed T-72M1 hull skin, and the text files that used to sit in the
bundle (armour values, vanilla edits, rig map, material roles, ERA links, smoke points, the Arena-M kit's list).

The bundle's contents and formats are in [docs/bundle.md](docs/bundle.md).

## What happens when a T-90M spawns

At game start `T90MMod.OnInitializeMelon` makes the settings (`CreateSettings`), stops there if `Enabled` is off, loads
the bundle and the text in `Resources\` (`LoadData`) and registers a unit `T90M` (`RegisterUnit`) based on the `T72M1`
with the `T80B`'s army (GHPC has the T-72M1 as NVA and I wanted it Soviet). `UnitRegistry` (in `Common\`) copies the
T-72M1's entry under that name so Instant Action, the customiser and the campaign offer it, and re-adds it whenever a
scene or spawner turns up without it.

At spawn, `SpawnPatch` on `UnitSpawner.SpawnUnit` (`Common\UnitRegistry.cs`) renames the unit and runs
`Conversion.Convert`, which walks `Conversion.Steps`, a table of named steps: base visuals, visuals, paint, hatch
undersides, tactical numbers, leftovers, armour, interior, the firepower steps (bustle box, ammunition, the 3OF82's
fuse, coax, gun), mobility, smoke launchers, hatches, turret axis, then the commander's station, modules, laser and
periscope placement and TShU-1-2M, and last the LOD materials. `ConversionSteps.Run` (in `Common\`) gives every step its
own try/catch, so one that fails logs an error naming it and what stays as the T-72M1's, and the rest carry on. Two
frames later `LateSetup` walks `Conversion.LateSteps` once the game has built the crew: the voices, the sights, the PDT,
the station's late part, the tracker, the module wiring, the thermal heat and the ammunition self-check.

The station, modules, laser and periscope placement and TShU-1-2M only work at spawn, because GHPC builds the unit's
weapon and camera lists straight afterwards (a step marked `SpawnOnly`). A T-90M the spawn patch missed is caught at
`PlayerReady` by `ConvertSweep` (in `Common\`) and gets everything else, and the log says which step it skipped and what
that leaves undone.

The Arena-M kit comes last (`Arena.Offer`, called at the end of `LateSetup`, so the racks are filled and the modules wired).
With both Arena-M settings at 0 / false it returns at once and the tank is the plain T-90M. Otherwise it waits for
`PlayerReady` (`Arena.OnSceneLoaded` asks `StateController` for it, low priority): GHPC's mission composer only hands the
player his tank then, so a flex mission's T-90Ms are converted before anyone knows which is the player's. At that moment
the player's tank takes its answer from `Arena-M kit on my tank` and every other waiting T-90M rolls against `Arena-M kit share`
(`PortKits.Decide`, a generator of its own seeded from the clock, never `UnityEngine.Random`, which GHPC reseeds). A T-90M
that finishes its setup after `PlayerReady` is decided at once. A tank the player switches to later keeps what it was given.
A kit tank then walks `Arena.Steps`: `PortKits.Apply` (the kit's parts on, the parts they replace hidden, its armour pieces
built and the ones they replace switched off, its modules built and wired), 1150 kg on the rigid body's mass, the bustle
box taken away with its 10 rounds (`BlowoutRack.Remove`: 30 left, 22 in the carousel and 8 in the hull) and `Aps.Apply` with
the Arena-M's settings. The console gets one line a mission, a moment after `PlayerReady`: your tank yes or no, how many of
the others carry it.

Parts borrowed from other vehicles (T-80B voices and sight sounds, the T-64B's machine gun station, the M60A3's thermal
screen) are copied at the main menu by `DonorParts` (in `Common\`), because GHPC can unload those vehicles between
missions.

Private game members are read through `Common\Reflect.cs`, which warns once instead of throwing when a name has gone.
Patches that run every frame report through `Common\PatchGuard.cs` so one throw doesn't kill the game's loop.
`tools\check-game.ps1` checks all of it against a game build without starting the game. If you add a `Reflect` call
whose member name comes from a variable, put the names in `tools\check-game-list.txt` with a `covers` line.

## Settings

MelonPreferences in the `[T-90M]` category of `UserData\MelonPreferences.cfg`. The README has the player wording and
this table says where each one lives. `tools\check-settings.ps1` checks both tables against the code. The descriptions of
three settings live in `Common` (`Weathered paint`, `Campaign prevalence`, `Check for updates`); all the others are in
`SettingText.cs`.

| Setting | Defined in | Read by |
|---|---|---|
| `Enabled` | `Mod.cs` | everything, off means the mod does nothing |
| `Add to campaign roster`, `Campaign prevalence`, `Campaign unlock after missions` | `T90M\Mod.cs`, `Common\UnitRegistry.cs` (`CampaignConfig`) | the campaign roster, re-read at each mission load |
| `APFSDS` | `Mod.cs` | `Firepower`, the default round where the game doesn't pick one |
| `Force APFSDS` | `Mod.cs` | `Firepower` (`ForceApfsds`): the campaign's roll gives the `APFSDS` round |
| `Random APFSDS`, `APFSDS pool` | `Mod.cs` | `Firepower` (`RandomApfsds`, `ApfsdsPool`): each tank rolls its own APFSDS outside the campaign |
| `Ammunition load`, `Empty racks` | `Mod.cs` | `Firepower` (`LoadPreset`, `EmptyRacks`, the racks' names and `LoadPresets`): racks left empty at spawn |
| `Fire only when the gun is laid` | `Mod.cs` | `Sights` and `Commander`, handed to `GunnerFcs` as the firing permission |
| `Target tracker`, `Target tracker key`, `Target tracker from the day sight` | `Mod.cs` | `Tracker` (in `Sights.Tracker.cs`) |
| `Weathered paint` | `T90M\Mod.cs`, `Common\PaintFinish.cs` | `Conversion` |
| `TShU-1-2M` | `Mod.cs` | `Countermeasures`, off leaves the game's ordinary smoke salvo |
| `TShU-1-2M automatic screens`, `TShU-1-2M alarm tone`, `TShU-1-2M warning page`, `TShU-1-2M page always on screen` | `Mod.cs` | copied into the screen settings in `Countermeasures.cs` |
| `Arena-M kit on my tank` | `Mod.cs` | `Arena` (`Countermeasures.Arena.cs`): the player's own T-90M carries the kit, never rolled |
| `Arena-M kit share` | `Mod.cs` | `Arena`: the percentage of the other T-90Ms that roll the kit, kept to 0..100 with one warning |
| `Kord spent cases` | `Mod.cs` | `Commander` |
| `ERA destruction effects` | `Mod.cs` | `EraBricks.Look` |
| `Armour preset` | `T90M\Armour.cs`, `Common\ArmourPresets.cs` | `Armour` (`game-scale` or `source-only` numbers) |
| `Check for updates` | `Common\UpdateCheck.cs` | asks GitHub once per start, one console line |
| `ConfigVersion` | `T90M\Mod.cs`, `Common\PortSettings.cs` | hidden, the version that last wrote the section |

Most only apply to T-90Ms spawned after the change. Two catches when you add or change one:

- MelonLoader writes the old default into the player's file and keeps it. To move players to a new default, add a
  `SettingsMigration` to `Migrations` in `Mod.cs`. It only changes a value still at the old default, once.
- A decimal-comma Windows can leave `"0,3"` in the file, which MelonLoader quietly ignores. `PortSettings.Finish` reads
  it anyway, so call `Finish` after all the entries exist.

## Releasing

1. Bump the version in `MelonInfo` at the top of `T90M\Mod.cs`. `Repo` next to it is what the update check asks. Commit.
2. Take the commit's 7-character short hash (`git rev-parse --short=7 HEAD`) and build with it:
   `dotnet build -c Release -p:GameDir="<GHPC folder>\Bin" -p:GitCommit=<that hash>`.
3. Install the DLL, start a mission and read `MelonLoader\Latest.log`: the stamp first, then no warnings or errors from the
   mod.
4. Take `t90m` from the last release. The armour numbers, hatches, glass and smoke points are text in `Resources\`, so
   changing them is a rebuild of the DLL and nothing else. The bundle only has to change with the model, and that needs the
   Unity project, which isn't in this repo. [docs/bundle.md](docs/bundle.md) says which edits are safe. For every release,
   new bundle or not, add a row with the version and the bundle's hash to its hash table:
   `(Get-FileHash Mods\T90M\t90m -Algorithm SHA256).Hash`.
5. Zip it as `T90M_<version>.zip` with `Mods\T90M.dll` and `Mods\T90M\t90m` inside a `Mods` folder at the zip's root: put
   the two files in a `Mods` folder as laid out here and run `Compress-Archive -Path Mods -DestinationPath T90M_<version>.zip`.
6. Tag the commit `v<version>` (for 0.2.2: `v0.2.2`), push the tag, and create the GitHub release from it with the zip
   attached and release notes: what changed for the player, a line or two each.
7. Check the release against the tag:
   `powershell -ExecutionPolicy Bypass -File tools\verify-release.ps1 -Tag v<version> -Release T90M_<version>.zip -GameDir "<GHPC folder>\Bin"`.
   It rebuilds the tag in a temporary folder and says IDENTICAL or what differs. Expect IDENTICAL for a release built from its
   tag with the commit its stamp carries (the released 0.2.2 rebuilds byte for byte from tag `v0.2.2`). DIFFERENT means the
   SDK or the source differs: then compare the decompiled code.
