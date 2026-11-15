# The T-90M in detail

The bits that didn't fit in the [README](../README.md): the comparison with the T-90A, the sights, the Kord and the
ammo up close, the numbers, and where the mod isn't quite the real tank. The armour and TShU-1-2M have their own pages,
[armour.md](armour.md) and [screens.md](screens.md).

## From the T-90A to the T-90M

A lot of T-90Ms are rebuilt T-90As, so underneath it's the same tank as my other mod. Same hull, running gear,
autoloader and gearbox, still the T-72's 4.2 km/h reverse. What changed:

| | T-90A (my other mod) | T-90M (this one) |
|---|---|---|
| ERA | Kontakt-5, about +140 vs APFSDS / +550 vs HEAT | Relikt, about +250 / +650 (+600 on the hull), also in the rubber side screens |
| Other screens | none | nets on the turret, slats at the back |
| Turret | welded, every round inside the hull | the same welded front, plus an armoured bustle box on the rear with 10 rounds behind blow-off lids |
| Gunner's sight | 1G46 with the ESSA thermal on its own head, 1A45T fire control | Sosna-U, day and thermal in one head, Kalina fire control and a target tracker |
| Commander | TKN-4S day / night sight | PK PAN panorama, TV and thermal, can lay the gun and hand targets to the gunner |
| Remote Kord | on the cupola, 2 belts of 150, 625 rounds a minute | on top of the panorama, one belt of 300, 200 or 100 rounds a minute |
| Defensive aids | Shtora-1: IR jammer, laser warning, aerosol screens | TShU-1-2M: laser warning and aerosol screens, no jammer |
| Engine | V-92S2, 1000 hp | V-92S2F, 1130 hp |
| Turret traverse | 24°/s | 40°/s |
| Main gun ammo | 42 rounds | 40: 22 in the carousel, 8 in the hull, 10 in the bustle box |
| Rounds | 3BM42 / 3BM60, 3BK18M, 3OF26, 9M119M Invar (700 mm) | the same shells plus the 3OF82 Telnik, and the 9M119M1 Invar-M (850 mm) |

So it's better protected where the ERA, the screens and the bustle box come in, quicker onto a target off to the side,
and the commander can hunt on his own. The front armour under the ERA hasn't changed, though, and the Kord fires at a
third of the T-90A's rate or less.

GHPC has no T-72B, so the T-90M is built on the T-72M1 (running gear, hull internals, autoloader, diesel) and takes its
army, voices, sight sounds and HUD from the T-80B. Those borrowed parts, plus the T-64B's machine-gun station and the
M60A3 TTS thermal screen effect, are copied once from the game's own files at the main menu, so another mod freeing
those vanilla assets later doesn't break it.

## A bit of history

Uralvagonzavod's design bureau started on it in the early 2010s as "Proryv-3". It took what worked on the T-90AM and
its export version, the T-90MS ("Proryv-2"), and fixed what the army had found wrong with the older tanks. It was first
shown at Army-2018 and the first ones reached the army in spring 2020.

## The sights

**Sosna-U:** the day channel at 4x and 12x, with the reticle from the manual's figure. The thermal channel has three
fields (9 × 6.75°, 3 × 2.25° and 1.5 × 1.12° with the electronic zoom), rendered 768 x 576 across the 4:3 picture and
shown grey on a flat monitor. The grey comes from the Sosna-U's own figures. Laser rangefinder from 300 m to 10 km.
Line of sight -10° / +30°, stabilised on its own, and up to 7.5° ahead of the turret sideways.

**PK PAN:** the TV channel 7.4° and 2.5° wide in black and white, and the thermal in the same three fields as the
Sosna-U, with the same marks. Its own laser, 300 m to 7.5 km. It turns all round on the turret roof and looks from -15°
to +45°, and the Kord turns with it. Leave the panorama and the station holds its bearing on the turret, so it swings
round with the turret until you come back.

**ПДТ:** the gunner's back-up TV sight, left of the gun. It's laid along the bore, not on the computer's impact point,
so it gives no lead. -9° / +17°, a stadiametric scale, and the range set by hand.

**The tracker's ready frame** comes up when the target is big enough in the picture to lock, about 4 km for a T-72 in
the narrow thermal. Nobody publishes that figure, so it's my guess.

## The Kord

![The Kord's belt feeding up into the gun](images/kord_belt.jpg)

- A 6P49MT Kord, one belt of 300, three armour-piercing incendiary to one tracer. The belt's reloaded under armour,
  though the real box sits outside on the mount.
- 316° of traverse and -10° / +45°. The station turns at 45°/s and elevates at 10°/s.
- The 44° it can't fire through is over the gunner's sight head. The manual's figure gives the size of that gap but not
  where it is, so where it sits is my guess.
- Aimed only through the panorama, not from the commander's head view (the one with the binoculars).
- The drives, the panorama and the gun can each be knocked out.
- The belt feeds round by round as it fires. The spent cases go out forward on the right and lie on the tank until it
  tilts or brakes hard enough to shake them off. `Kord spent cases` turns them off.

## The ammo

- **22 in the autoloader's carousel** under the turret floor.
- **8 in the hull**, against the engine bulkhead, as the T-90M's manual has it: the charges in a rack on the floor, the
  shells in racks under the hull roof behind spall screens. Only six shells fit under the roof, so two lie in the floor
  rack, and how they're arranged there is my best guess.
- **10 in the bustle box** on the turret rear, behind blow-off lids. It's refilled only outside a fight, like on the
  real tank, and a round out of it takes 20 s.
- The T-72's middle fuel tank rack is gone on the real T-90M, so there's nothing stowed there.

The 3BM60 has its own longer rod and sabot, and the Invar-M is its own missile. They're made from the game's own round
models, so they don't look out of place next to the vanilla shells.

**The Telnik** is the 3OF26's shell with an air-burst fuse. The Kalina works out the flight time from the lased range as
the round goes into the breech, allowing for the muzzle standing a few metres ahead of the laser. Set short, it bursts
about 10 m in front of the target. Set over, the gun lays about 3 m high and the round bursts at the range. The fuse
spread is 0.05 s either way. That's the published figure, which I read as one standard deviation, so now and then a
round bursts well off. It throws 450 fragments in a cone 20° either side of its line. Under 300 m, or with no valid
range, it falls back to impact, and the vehicle menu shows what the round in the breech was set to.

## The paint

<p align="center">
  <img src="images/paint_weathered.jpg" width="49%" alt="Weathered paint (default)">
  <img src="images/paint_clean.jpg" width="49%" alt="Clean factory paint">
</p>

Weathered by default and made for the T-90M's own shape: dust and mud heavier toward the ground, grime in the crevices,
streaks down the sides, rust running from the bolts and edges at the back, chipped edges. `Weathered paint` set to false
gives the clean factory finish on the right.

## Numbers

| | |
|---|---|
| Engine | V-92S2F, 1130 hp at 2000 rpm, T-72 gearbox |
| Mass | 51 t |
| Speed | 60 km/h forward, 4.2 km/h reverse (the real gearbox's reverse) |
| Turret traverse / gun elevation | 40°/s / 28°/s |
| Gun | 2A46M-5, -6°13′ / +13°47′ over the front, less depression over the engine deck |
| Autoloader | The game's own T-72M1 timing, about 7.2 s for every round |
| Main gun ammo | 40 rounds: 3BM60 or 3BM42, 3BK18M HEAT, 3OF26 HE-Frag, 3OF82 Telnik, 9M119M1 Invar-M |
| Coax PKTM | 2000 rounds (5 boxes of 400) |
| Remote Kord MT | 300 rounds, 200 / 100 rounds a minute |
| Smoke | 12 × 81 mm "Tucha", 3D17 aerosol grenades |

| Round | |
|---|---|
| 3BM60 "Svinets-2" (default) | 693 mm at the muzzle, 1660 m/s |
| 3BM42 "Mango" | as on the T-90A |
| 9M119M1 "Invar-M" | 850 mm, tandem warhead (sets off ERA first), arms at 100 m, out to 5000 m, guided along the gunner's sight line |
| 3OF82 "Telnik" | the 3OF26's shell with an air-burst fuse, 450 fragments |
| 3BK18M HEAT, 3OF26 HE-Frag | the game's own rounds |

## Where it isn't the real tank

- The armour presets are the T-90A's figures (game-scale: turret 690 / 820, glacis 550 / 750; source-only: 575 / 750 and
  500 / 650, without ERA), because I found no figures for the T-90M I could trust. Relikt's bonus on top is worked out
  from the maker's factor.
- The plate shapes are War Thunder's. None of its armour ratings is used, but where no source gives a thickness (the
  turret shell, the cupola ring, the driver's hatch, the bustle box) the plate is as thick as the model draws it.
- The gun's elevation speed isn't published for the T-90M: 28°/s comes from a patent for the hydraulic elevation drive.
  The autoloader keeps the game's T-72M1 timing, because no cycle time is published for the T-90M's either.
- The gun's depression at each turret angle is my own table, with the barrel kept clear of the hull. No source gives it.
- The bustle box's lids blow off like the M1's; the manual describes hinged, bolted lids. The lids' thickness, the size
  of the fire and the restock time are my own.
- The Kord's belt reloads under armour, though the real box sits outside on the mount. The station's speeds are the
  panorama's, and its blind sector and the mouth of its case chute are guesses.
- The warning page is my own layout, because the real one isn't published. The panorama's and the back-up sight's marks
  are borrowed from the Sosna-U or inferred.
- A set-off Relikt container is simply gone, because there's no model of a spent one, and the nets being cut over it is
  my guess.
- How the eight hull rounds sit and the tracker's lock distance are my guesses too.

## What it shares with my T-90A

I built the T-90M after the [T-90A](https://github.com/marsattackk/GHPC-T90A), on the same groundwork. Everything I
worked out there carries over: how the armour's put together, the ERA that doesn't go off for a PKT round, the Invar's
tandem precursor, the Kord's belt feed and spent cases, the paint, the T-80B's crew voices, and a fix on one goes into
the other. What's new is what's new on the real tank: the bustle box as its own compartment, the panorama with the Kord
on it, Relikt and the nets, TShU-1-2M, the tracker and the Telnik. It's still its own mod and neither needs the other.

My [T-80UM2](https://github.com/marsattackk/GHPC-T80UM2) came after both, on the same groundwork: a T-80U with
Kontakt-5 and the Drozd active protection, the T-90A's rival from the T-80 side rather than a step towards the T-90M.
