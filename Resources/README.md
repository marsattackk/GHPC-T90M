# Resources

Data files embedded in the DLL. The csproj embeds every file here under its plain name except this README, so a new
file only has to be dropped in. The reticle files (`*.jsonc`) are written in the language described in
[docs/reticles.md](../docs/reticles.md).

| File | What it holds | Read by |
|---|---|---|
| `T72M1_skin_hull_trimmed.bin` | T-72M1 hull skin mesh without the side skirts | `EmbeddedFile.T72M1Skin`, then `BaseRigT72M1.HideBaseVisuals` |
| `gun_depression.txt` | gun depression by turret angle, for the hull profile | `GunSetup`, name set in `Firepower` |
| `interior_map.txt` | where each crew member and interior part moves to | `InteriorMap`, through `Conversion` |
| `modules_built.json` | the T-90M's damageable modules and what they hook into | `PortModules`, through `Conversion` |
| `sosna_u_day.jsonc` | gunner's Sosna-U day reticle | `Sights` |
| `sosna_u_thermal_wide.jsonc`, `sosna_u_thermal_narrow.jsonc`, `sosna_u_thermal_zoom.jsonc` | Sosna-U thermal reticles, wide, narrow and zoomed field | `Sights`, `Commander` (in `Commander.Sight.cs`) |
| `sosna_u_thermal_row.jsonc` | Sosna-U readout row under the thermal reticle | `Sights`, `Commander` (in `Commander.Sight.cs`) |
| `pk_pan_tv_wide.jsonc`, `pk_pan_tv_narrow.jsonc` | commander's panoramic sight, wide and narrow field | `Commander` (in `Commander.Sight.cs`) |
| `pdt.jsonc` | the PDT back-up sight's reticle | `Pdt` (in `Sights.Pdt.cs`) |
| `asc_marks.jsonc` | the target tracker's ready, lock and memory frames | `Tracker` (in `Sights.Tracker.cs`) |
| `round_3bm42_co.png`, `round_3bm60_co.png`, `round_3of82_co.png` | stencil textures for the 3BM42, 3BM60 and 3OF82 rounds | `Firepower`, applied by `PortAmmo` |
| `round_9m119m1_co.png` | stencil texture for the 9M119M1 missile | `Firepower`, applied by `PortAmmo` |
| `round_meshes.txt` | index of the round mesh pieces below, by round | `PortAmmo.Models` |
| `round_mesh_3bm60_*.mesh` (charge, oring, rod, sabot 001 / 003 / 004) | 3BM60 shell pieces | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_9m119m_missile.mesh` | 9M119M missile body | `PortAmmo`, via `round_meshes.txt` |
| `round_mesh_9x949_charge.mesh` | booster piece for the missile | `PortAmmo`, via `round_meshes.txt` |
| `shtora_pmf_page.png`, `shtora_pmf_over.png` | the TShU-1-2M warning page on the commander's display, and its overlay | `ShtoraPage` |
| `shtora_pmf_layout.txt` | where the words sit on that page | `ShtoraPage` |
| `shtora_alarm.wav` | laser warning tone | `ShtoraScreen.Hud` |
| `dm_armour.txt`, `vanilla_edits.txt`, `material_roles.txt`, `rig_map.txt`, `era_links.txt`, `smoke_points.txt` | armour values, hatches, glass, ERA and smoke data, formats in [docs/bundle.md](../docs/bundle.md) | `PortText`, before the bundle's copy |
