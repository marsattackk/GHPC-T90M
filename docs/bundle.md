# The asset bundle

The model, textures and armour meshes live in one Unity asset bundle, `Mods\T90M\t90m`. It isn't in this repo and
neither is the Unity project it came from, so this page is what the code expects to find in it. If you rebuild it, this
is what to build. Where it says "silently", nothing goes in the log and the tank just looks or behaves wrong.

Built with Unity **2022.3.62f2**, the same as GHPC. A bundle has to match the game's Unity version (see
[game-updates.md](game-updates.md)).

## What gets loaded

`T90MMod.LoadData` in `T90M\Mod.cs` opens the bundle through `BundleAssets` and loads the visual prefab by name, and
`Armour.Load` in `T90M\Armour.cs` the armour one:

| Asset | Needed | If missing |
|---|---|---|
| `T90M_visual` | yes | error, no model and no conversion at all |
| `T90M_armor` | no | warning, the T-72M1's armour stays |

The text data is in `Resources\` as `<name>.txt` and goes into the DLL. `PortText` reads the DLL's copy first and only
falls back to a `TextAsset` of the same name in the bundle, which the 0.2.2 bundle carries and a new one doesn't need.
Edit these and rebuild the DLL, no Unity involved:

| File | Needed | If missing |
|---|---|---|
| `dm_armour` | with `T90M_armor` | warning, the T-72M1's armour stays |
| `vanilla_edits` | with `T90M_armor` | silently, the T-72M1's own armour stays switched on inside the T-90M's |
| `rig_map` | yes, in practice | warnings: no moving hatches or lids, the turret on the T-72M1's pivots, the commander's station fixed and unarmed |
| `material_roles` | no | silently, glass and lamps keep the bundle's own materials |
| `era_links` | no | silently, each Relikt piece is matched to a container by position only |
| `era_gone` | no | silently, nothing hides with a container. The current bundle has none |
| `smoke_points` | no | warning, grenades leave from the T-72's tubes |

The catch is that most lines name things in the bundle. Numbers, armour types, x-ray colours, roles, donors, hinge
angles and smoke points are safe to change on their own. Renaming an armour piece, or pointing a line at a different
node or material, only works if the bundle has that name too. Otherwise it's a new bundle, built with the renamed
pieces.

Empty lines and lines starting `#` are skipped. A bad line in `dm_armour`, `vanilla_edits` or `rig_map` is skipped with
a warning naming the asset and the line, and the rest loads. A short line in `material_roles`, `era_links` or `era_gone`
is just skipped. Numbers use a full stop for the decimal point. `<TAB>` below means a real tab character.

## `T90M_visual`

`VisualParts.Attach` instantiates it, moves the root's `HULL`, `TURRET` and `GUN` children onto the matching
`---MESH---` bones, and `MANTLET_COVER` onto the gun bone, then throws the rest of the instance away. Each moved part
gets `T90M_` in front of its name and its local transform reset, so model each one in its bone's own space.

- `MANTLET_COVER` needs a plain, readable mesh on the node itself. `MantletCover` re-skins it from turret at the back to
  gun at the front by z. Without it: `bundle has no MANTLET_COVER; the mantlet cover stays on the turret`.
- Hatches, the bustle lids, the wheels, the antenna whip and the turret nets are found by the names in `rig_map`, as
  `T90M_<name>` or `<name>` anywhere under the unit.
- **Commander's station.** `Commander.Apply` needs `mount` lines for `RWS` (the drum, turning about its axis) and
  `RWS_CRADLE` (the elevating cradle). Other `mount` lines with no axis ride one of those two by name. Today that's
  `RWS_BOX`, the ammunition box with the belt, and `RWS_KORD`, the gun. Without the first two the log says the bundle has
  no remote-station mounts and the station stays fixed and unarmed. A missing ridden part gets named in the
  commander's station warning.
- **The Kord belt** is cut at run time out of the renderer under `RWS_BOX` whose material starts
  `t90m_mg_6p49_kord_ammo`. The mesh has to be readable, or there's no moving belt and no spent cases.
- **Bustle lids** are `BUSTLE_LID_C`, `BUSTLE_LID_L` and `BUSTLE_LID_R`. A missing one is named in the `bustle box:`
  warning and the box vents without it.
- **Smoke tube caps** are renderers whose names start `smoke_cap_`. They pop off on a salvo once the launch points have
  been moved onto the tubes.
- **Relikt containers** are matched up through `era_links`. A container node can have `<name>_open` beside it for the
  burst look, plus optional `<name>_lid` and `<name>_soot`. With `ERA destruction effects` on, the T-90M hides a
  container outright when it goes off (`EraBricks.Vanish`) and only shows its soot. A turret box whose containers are
  named `ex_era_turret_<n>_c<k>` loses its shell `ex_era_turret_<n>` as well once every container on it has gone. That
  prefix is in `Mod.cs`.
- **Nets** over a container that goes off get their triangles cut inside its box. The nets are the `flap` lines in
  `rig_map`, so there's nothing more to name.
- LODs: each `LODGroup` with one renderer in LOD0 and one in LOD1 gets LOD0's materials on LOD1, so LOD1 needs as many
  submeshes as LOD0 has materials.

## Materials

Matched with `StartsWith` unless it says exact.

- `t90m_t_90m_*` on the moved parts is replaced with a copy of the base vehicle's `GHPC/VehicleShader` material, taking
  your `_MainTex`, `_BumpMap`, `_MetallicGlossMap`, `_DetailMask` (paint mask) and `_OcclusionMap`. `_DetailAlbedoMap`
  and `_DetailNormalMap` hold the clean albedo and metal-gloss for `Weathered paint` off.
- `t90m_t_90m_turret` must stay a 4096 x 4096 atlas: the hatch undersides are painted as a flat patch at x 1312,
  y 3856, 230 x 110 px.
- `t90m_hatch_inner*` is the inside of the hatch lids. Without it the log says `no hatch underside material found`.
- Anything on those parts using plain `Standard` is switched to GHPC's `Standard (FLIR)` so it shows in thermal.
- Names in the first column of `material_roles` are matched exactly.

## `T90M_armor`

Shapes only, cut from War Thunder's damage model of the T-90M. The numbers come from `dm_armour`. `DmArmourApply`
applies `vanilla_edits` first, then puts the pieces under the root's `HULL`, `TURRET` and `GUN` onto the matching bone
and collider group. The pieces under `RWS` and `RWS_CRADLE` are applied later by `Commander.Apply`, onto the station's
drum and cradle.

Each piece is a direct child of its group, with a readable mesh, named `dm_<anything>|<piece>`. `<piece>` is the key in
`dm_armour` and becomes the object's name, which `era_links` refers to. Children not starting `dm_` are skipped as
armour. Two other kinds sit beside them:

- `reshape|<name>` gives the T-72M1 piece of that name a new mesh, when `vanilla_edits` asks for it.
- `volume|<name>` is a volume mesh, not armour. `Firepower` looks for `volume|Bustle Volume (COMPARTMENT)` under
  `TURRET` and builds the bustle box's compartment from it. Without it the box isn't a compartment of its own and its
  rounds burn with the crew's.

`geometry` pieces are measured through the mesh, so it has to be closed.

## Text files

### `dm_armour`

One piece per line, tab separated, at least 11 fields:

```
<piece> <mode> <ke> <ce> <ke after> <ce after> <angle> <material> <x-ray> <name> <source> [<slat>]
```

- `mode`: `geometry` (`ke` and `ce` are multipliers per mm of path), `era` (`ke after` and `ce after` once it has gone
  off), `composite` (uniform, convex collider), `drawn` (x-ray and AAR only, no armour), anything else uniform (the
  bundle uses `nominal`).
- `ke`, `ce`: mm of rolled steel against kinetic and chemical rounds.
- `angle`: `1` to count the hit angle on a uniform piece.
- `material`: `armour_steel`, `mild_steel`, `plain`, `composite`, `filling`, `filling_steel` or `liner`, all the mod's
  own armour types. Empty for `drawn`.
- `x-ray`: empty (not drawn), `template` (GHPC's see-through material) or `r,g,b[,a]` (a tinted copy).
- `name`: the label in the armour view and damage report. `source` isn't read.
- `slat`: `1` for slat armour. A slat piece whose name starts `net screen` is a net (`NetScreens.NamePrefix`). If no
  piece is named that way the log says so and the nets act as plain slats.

The numbers are the `game-scale` preset. `source-only` scales down every `geometry` piece with `_pkg` in its name and
the glacis fillings listed in `Armour.GlacisFilling`, so renaming those leaves them at game scale in both.

### `vanilla_edits`

What to do to the T-72M1's own armour, by collider object name, `|` separated.

| Line | Does |
|---|---|
| `off\|name` | switches the piece and its children off |
| `strip\|name` | removes its colliders, armour and renderer, keeps the children (done last) |
| `reshape\|name` | gives it the mesh of `reshape\|name` in `T90M_armor` |
| `xform\|name\|m0,...,m15` | moves it by a `Matrix4x4` (column-major), in the unit's root space at rest |
| `bone\|path\|group\|dx,dy,dz` | moves a bone and the colliders following it, in metres (`group` is a label, not read) |

The code adds to whatever the file says. The T-72M1 pieces War Thunder's model replaces (`BaseRigT72M1.ImportReplaces`)
and `Fire Wall` and `Rear Fuel tank` (`BaseOff` in `Armour.cs`) are always switched off. `Cannon` is always moved by
`BaseMoved`, which replaces any `xform` for it in the file, unless the file reshapes it.

### `material_roles`

```
<bundle material><TAB><role><TAB><vanilla material or -><TAB><donor units, comma separated>
```

The role is only for the log. `-` makes the line a note. Donors are unit keys like `T80B`, tried in order.

### `rig_map`

`|` separated, positions in the unit's root space at rest, extra trailing fields ignored.

| Line | Fields |
|---|---|
| `gear\|mode` | `base` (leave it, the default), `follow` (wheels and tracks follow the suspension) or `port` (park parts on bones) |
| `follow\|restY\|uvX,uvY\|radius\|rimGap\|riseLimit` | the wheel numbers `GearFollow` needs |
| `fpart\|id\|node\|kind\|drive\|pivot\|target\|mate` | one moving part, `kind` is `wheel`, `arm`, `rod`, `spin` or `track` |
| `axes\|turret\|gun\|muzzle\|pitch` | the real turret and gun pivots, the muzzle, and the model's pitch in degrees. Without it the turret turns on the T-72M1's pivots |
| `hatch\|?\|role\|node\|baseBone\|hinge\|axis\|angle\|lift` | one hatch or lid, driven by the base bone's `Openness`: lifts for the first 30 %, then turns |
| `mount\|?\|node\|rides\|pivot\|axis` | a station part. With an axis it's a drive, with a zero axis it rides the node named in `rides` |
| `part\|?\|node\|bone\|qx,qy,qz,qw` | `port` only, parks the node on the bone |
| `whip\|?\|node\|base\|tip\|rigid` | the radio whip |
| `flap\|?\|node\|a\|b\|?\|limit\|taper\|ramp\|lag` | a hanging flap on a jiggle bone (the turret nets), `lag` optional |

`?` fields aren't read. What `rigid`, `taper`, `ramp` and `lag` do is in `HatchRig.Whip` and `HatchRig.Flap`. The
station levels the cradle by the `axes` pitch, because the model has the Kord pitched nose down with the turret.

### `era_links` and `era_gone`

```
<armour piece><TAB><visual container>
<visual part><TAB><container>,<container>
```

`era_links` ties each `era` piece to its container in the visual. Without a line the container is picked by geometry,
which can pick the wrong one where boxes sit close together. Pieces on the same container set each other off. `era_gone` hides a visual part
when any of its containers goes off, along with an armour piece `box_<part>` if there is one.

### `smoke_points`

Floats separated by any spaces, commas, tabs or newlines, three per tube mouth, in the turret bone's space. Left and
right go by the sign of x, then top down, matched to the T-72M1's smoke slots. One bad number drops the whole list,
since it would shift every tube after it.

## Released bundles

SHA-256 of `Mods/T90M/t90m` in each release zip. When a release changes the bundle, add a row.

| Version | Bundle |
|---|---|
| 0.1.0 | `2b1fcf41f2b5605818d530b405a31b507659288a0083a7f4f7de53a995ffcd4b` |
| 0.1.1 | `310a388c951e2abb3a9bf3fe09fed78523db616416cd84c3b2b6510aee17f1b0` |
| 0.1.2 | `c215f50f68971f5434eaad4df98426a1623b84a09c2d0696c4bdbb49595eaca4` |
| 0.1.3 | `277c75f883cca4c5a469c14118a67d0753b20624f35b7a647116c2b3bd9aaf99` |
| 0.2.0 | `dedb5244628a75d6dede8bc5f87b63b0831b6be5bcf68f64b15b4f64f748f9f2` |
| 0.2.1 | `e5202f37113d0728960e3ec8c4489e054e5c9306426f9424a59b0c32e4a4160b` |
| 0.2.2 | `deba555eb68f7aaf2e2d68d5e84a4ff380b8db3bd70ee0a95df2067afbabdfe2` |
| 0.3.0 | `5df70af9173f96bce09b3736391a10ad244f07c456a6b63d07516867eda3b3fe` |

```
Expand-Archive T90M_0.2.2.zip -DestinationPath T90M_0.2.2
(Get-FileHash T90M_0.2.2\Mods\T90M\t90m -Algorithm SHA256).Hash
```
