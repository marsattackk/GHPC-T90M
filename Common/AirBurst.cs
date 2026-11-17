using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GHPC;
using GHPC.Effects;
using GHPC.PhysicsHelpers;
using GHPC.Player;
using GHPC.Vehicle;
using GHPC.Weapons;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;

namespace T90M.Common
{
	// Programmed air-burst fuse: short of the lased range, over it, or on impact.
	internal static class AirBurst
	{
		internal enum Mode { Short, Over, Impact }

		// Fields a tank never sets keep their defaults here: another tank sets them (CS0649 is off for this declaration only).
#pragma warning disable CS0649
		internal sealed class Settings
		{
			public string Tag = "Port";
			public string RoundName;
			public string MenuName;
			public Mode[] MenuModes;
			public Mode Default = Mode.Short;
			public float ShortOf = 10f;
			public float Over = 3f;
			public float MinRange = 300f;
			public float TimeSd = 0.05f;
			public int Elements = 450;
			public float HalfAngle = 20f;
			public float BlockSpeed = 400f;
			public float ElementMass;
			public float ElementArea;
			public float ElementCoeff;
			public float ElementCaliber;
			public float ElementPen;
			public float ElementPenSpeed;
		}
#pragma warning restore CS0649

		internal static Settings S;
		static readonly Mode[] DefaultMenuModes = { Mode.Short, Mode.Over, Mode.Impact };

		internal sealed class Fuse
		{
			public Mode Mode;
			public float Range;
			public float Time = -1f;
			public override string ToString()
				=> Time < 0f
					? $"impact{(Mode != Mode.Impact ? $" (no valid range: {Range:0} m)" : "")}"
					: $"{Name(Mode)}, {Range:0} m, {Time:0.000} s";
		}

		sealed class Station
		{
			public Unit Unit;
			public WeaponSystem Gun;
			public FireControlSystem Fcs;
			public Mode Mode;
			public Fuse Breech;
		}

		static readonly List<Station> Stations = new List<Station>();

		internal sealed class Flight
		{
			public int Id;
			public Fuse Fuse;
			public float At;
			public Unit Shooter;
			public bool Triggered;
			public Vector3 Point, Dir;
			public float Speed;
		}

		static readonly Dictionary<LiveRound, Flight> Flights = new Dictionary<LiveRound, Flight>();

		internal static string Name(Mode m)
			=> m == Mode.Short ? $"short of the target ({S?.ShortOf:0} m)" : m == Mode.Over ? $"over the target ({S?.Over:0} m)" : "impact";

		internal static bool Ours(AmmoType a) => S != null && a != null && a.Name == S.RoundName;

		internal static void Attach(Vehicle vehicle)
		{
			if (S == null || vehicle == null) return;
			var gun = vehicle.GetComponent<WeaponsManager>()?.Weapons?.FirstOrDefault()?.Weapon;
			if (gun?.Feed == null) return;
			Stations.RemoveAll(e => e.Gun == null);
			if (Stations.Any(e => e.Gun == gun)) return;
			var station = new Station { Unit = vehicle, Gun = gun, Fcs = gun.FCS, Mode = S.Default };
			gun.Feed.LoadedRoundInBreech += a => Loaded(station, a);
			gun.Fired += (a, r) => Fired(station, a, r);
			Stations.Add(station);
		}

		internal static Mode? ModeOf(Unit u) => Stations.FirstOrDefault(s => s.Unit != null && s.Unit == u)?.Mode;
		internal static Fuse BreechOf(Unit u) => Stations.FirstOrDefault(s => s.Unit != null && s.Unit == u)?.Breech;

		internal static void SetMode(Unit u, Mode m, string by)
		{
			var station = Stations.FirstOrDefault(c => c.Unit != null && c.Unit == u);
			if (station == null) return;
			station.Mode = m;
		}

		static void Loaded(Station st, AmmoType a)
		{
			if (!Ours(a))
			{
				st.Breech = null;
				return;
			}
			float range = st.Fcs != null ? st.Fcs.CurrentRange : 0f;
			var fuse = new Fuse { Mode = st.Mode, Range = range };
			if (st.Mode != Mode.Impact && range >= S.MinRange)
			{
				// The range runs from the laser, the round starts at the muzzle about 6 m ahead of it.
				var dir = st.Fcs.AimWorldVector.sqrMagnitude > 1e-6f ? st.Fcs.AimWorldVector.normalized : st.Gun.MuzzleIdentity.forward;
				var from = st.Fcs.LaserOrigin != null ? st.Fcs.LaserOrigin : st.Fcs.ReferenceTransform;
				float ahead = st.Gun.MuzzleIdentity != null && from != null
					? Vector3.Dot(st.Gun.MuzzleIdentity.position - from.position, dir)
					: 0f;
				if (FuseMath.BurstDistance(false, st.Mode == Mode.Short, range, S.ShortOf, S.MinRange, ahead, out float distance))
					fuse.Time = FlightTime(a, dir, distance);
			}
			st.Breech = fuse;
		}

		// Not GHPC's ballistic computer, its flight times come out one 0.02 s step short.
		static float FlightTime(AmmoType a, Vector3 dir, float distance)
		{
			float muzzleVelocity = a.MuzzleVelocity * (1f + 0.0004f * (GHPC.World.WorldEnvironmentManager.AmmoTempFahrenheit - 70f));
			var state = new MotionState { position = Vector3.zero, velocity = dir.normalized * muzzleVelocity };
			const float step = 0.002f;
			return FuseMath.TimeToDistance(distance, step, 20f, () =>
			{
				state = a.UseErrorCorrection
					? BallisticEvaluatorRK4.EvaluateTimestep(state, step, a)
					: BallisticEvaluatorNonCorrected.EvaluateTimestep(state, step, a);
				return state.position.magnitude;
			});
		}

		static void Fired(Station st, AmmoType a, LiveRound r)
		{
			var fuse = st.Breech;
			st.Breech = null;
			if (!Ours(a) || r == null) return;
			foreach (var dead in Flights.Where(e => e.Key == null || e.Key.IsDestroyed || e.Key.ID != e.Value.Id)
				.Select(e => e.Key)
				.ToList())
				Flights.Remove(dead);
			if (fuse == null || fuse.Time < 0f) return;
			float scatter = S.TimeSd > 0f ? Gaussian() * S.TimeSd : 0f;
			var flight = new Flight { Id = r.ID, Fuse = fuse, At = FuseMath.BurstTime(fuse.Time, scatter), Shooter = st.Unit };
			Flights[r] = flight;
		}

		static float Gaussian()
		{
			float firstSample = 1f - UnityEngine.Random.value, secondSample = UnityEngine.Random.value;
			return FuseMath.Gaussian(firstSample, secondSample);
		}

		static readonly AccessTools.FieldRef<LiveRound, MotionState> Motion =
			Reflect.FieldRefAccess<LiveRound, MotionState>("_currentMotionState");
		static readonly AccessTools.FieldRef<LiveRound, bool> FuseActive = Reflect.FieldRefAccess<LiveRound, bool>("_rangedFuseActive");
		static readonly AccessTools.FieldRef<LiveRound, float> FuseLeft = Reflect.FieldRefAccess<LiveRound, float>("_rangedFuseCountdown");
		static readonly AccessTools.FieldRef<LiveRound, bool> Armed = Reflect.FieldRefAccess<LiveRound, bool>("_armed");
		static readonly AccessTools.FieldRef<LiveRound, bool> DrewEffect =
			Reflect.FieldRefAccess<LiveRound, bool>("_didNonTerrainImpactVfx");

		[HarmonyPatch(typeof(LiveRound), "DoUpdateImpl")]
		static class RoundPatch
		{
			static void Prefix(LiveRound __instance, float __0)
			{
				float deltaTime = __0;
				if (Flights.Count == 0 || !Flights.TryGetValue(__instance, out var flight)) return;
				try { Step(__instance, flight, deltaTime); }
				catch (Exception exception)
				{
					Flights.Remove(__instance);
					PatchGuard.Failed($"{S?.Tag} {S?.RoundName} fuse", exception);
				}
			}

			// GHPC picks a burst's effect by the surface it hit, in thin air it finds none and draws nothing.
			static void Postfix(LiveRound __instance)
			{
				if (_drawn == null || _drawn != __instance) return;
				var position = _drawnAt;
				_drawn = null;
				if (DrewEffect == null || DrewEffect(__instance) || ParticleEffectsManager.Instance == null) return;
				var effect = ParticleEffectsManager.Instance.CreateImpactEffectOfType(
					__instance.Info,
					ParticleEffectsManager.FusedStatus.Fuzed,
					ParticleEffectsManager.SurfaceMaterial.Steel,
					false,
					position);
			}
		}

		static LiveRound _drawn;
		static Vector3 _drawnAt;

		static void Step(LiveRound r, Flight f, float dt)
		{
			if (Motion == null || FuseActive == null || FuseLeft == null || Armed == null)
			{
				Flights.Remove(r);
				return;
			}
			if (r.IsDestroyed || r.ID != f.Id)
			{
				Flights.Remove(r);
				return;
			}
			if (!FuseMath.Due(r.TimeTraveled, dt, f.At, out float tau)) return;
			Flights.Remove(r);
			var before = Motion(r);
			var after = tau <= 0f
				? before
				: r.UseErrorCorrection
					? BallisticEvaluatorRK4.EvaluateTimestep(before, tau, r.Info)
					: BallisticEvaluatorNonCorrected.EvaluateTimestep(before, tau, r.Info);
			var from = r.transform.position;
			var segment = after.position - from;
			if (segment.sqrMagnitude > 1e-6f
				&& RaycastColliderUtils.Raycast(
					new Ray(from, segment),
					out var hit,
					segment.magnitude,
					ConstantsAndInfoManager.Instance.LiveRoundLayerMask)) return;
			// GHPC bursts a timed round where it was at the frame's start, so it goes onto the burst point first.
			r.transform.position = after.position;
			Motion(r) = after;
			FuseActive(r) = true;
			FuseLeft(r) = 0f;
			Armed(r) = true;   // rounds only arm on a hit, unarmed it bursts as a dud
			_drawn = r;
			_drawnAt = after.position;
			f.Triggered = true;
			f.Point = after.position;
			f.Dir = after.velocity.sqrMagnitude > 1e-6f ? after.velocity.normalized : r.transform.forward;
			f.Speed = after.velocity.magnitude;
			Elements(r, f);
		}

		static AmmoType _element;

		static AmmoType Element()
			=> _element
				?? (_element = FragmentCone.Type(
					S.RoundName + " element",
					S.ElementMass,
					S.ElementArea,
					S.ElementCoeff,
					S.ElementCaliber,
					S.ElementPen,
					S.ElementPenSpeed));

		static void Elements(LiveRound shell, Flight f)
		{
			if (S.Elements <= 0) return;
			float speed = f.Speed + S.BlockSpeed;
			float pen = FuseMath.ElementPen(S.ElementPen, S.ElementPenSpeed, speed);
			FragmentCone.Spawn(Element(), f.Point, f.Dir, S.HalfAngle, S.Elements, speed, pen, shell.Shooter, shell.NpcRound, shell, $"{S.RoundName} element",
				() => UnityEngine.Random.value, penAfterInit: true, fallbackRight: shell.transform.right
				);
		}

		static float OverHeight(FireControlSystem fcs)
		{
			if (S == null || Stations.Count == 0 || fcs == null) return 0f;
			for (int i = 0; i < Stations.Count; i++)
			{
				var station = Stations[i];
				if (station.Fcs != fcs) continue;
				if (fcs.CurrentWeaponSystem != null && fcs.CurrentWeaponSystem != station.Gun) return 0f;
				var fuse = station.Breech;
				if (fuse == null) return 0f;
				return FuseMath.OverHeight(fuse.Mode == Mode.Over, fuse.Time, fcs.CurrentRange, S.MinRange, S.Over);
			}
			return 0f;
		}

		[HarmonyPatch(typeof(FireControlSystem), nameof(FireControlSystem.CompensatedWeaponVector))]
		static class AimVectorPatch
		{
			static void Postfix(FireControlSystem __instance, ref Vector3 __result)
			{
				if (S == null) return;
				float height = OverHeight(__instance);
				if (height > 0f) __result += Vector3.up * height;
			}
		}

		[HarmonyPatch(typeof(FireControlSystem), nameof(FireControlSystem.CompensatedWeaponAimPoint))]
		static class AimPointPatch
		{
			static void Postfix(FireControlSystem __instance, ref Vector3 __result)
			{
				if (S == null) return;
				float height = OverHeight(__instance);
				if (height > 0f) __result += Vector3.up * height;
			}
		}

		// GetSuperelevation doesn't know which fire control asked, the Update prefix tells it.
		static FireControlSystem _gate;

		[HarmonyPatch(typeof(FireControlSystem), "Update")]
		static class GatePatch
		{
			static void Prefix(FireControlSystem __instance) { if (S != null && Stations.Count > 0) _gate = __instance; }

			static void Postfix() { _gate = null; }
		}

		[HarmonyPatch(typeof(BallisticComputerRepository), nameof(BallisticComputerRepository.GetSuperelevation))]
		static class GateSuperelevationPatch
		{
			static void Postfix(float __1, ref float __result)
			{
				float rangeMeters = __1;
				if (_gate == null || rangeMeters <= 1f) return;
				float height = OverHeight(_gate);
				if (height > 0f) __result += Mathf.Atan2(height, rangeMeters) * Mathf.Rad2Deg;
			}
		}

		[HarmonyPatch(typeof(PlayerInput), "AddSmokeButtons")]
		static class MenuPatch
		{
			static readonly MethodInfo Add = Reflect.Method(
				typeof(PlayerInput),
				"AddMenuButton",
				new[] { typeof(string), typeof(UnityAction) });

			static void Postfix(PlayerInput __instance)
			{
				if (S == null || Add == null) return;
				var unit = __instance.CurrentPlayerUnit;
				var station = Stations.FirstOrDefault(c => c.Unit != null && c.Unit == unit);
				if (station == null) return;
				string breech = station.Breech != null
					? $"; in the breech: {(station.Breech.Time < 0f ? "impact" : $"{Name(station.Breech.Mode)} at {station.Breech.Range:0} m")}"
					: "";
				foreach (var menuMode in S.MenuModes ?? DefaultMenuModes)
				{
					var mode = menuMode;
					string label = mode == station.Mode ? $"{S.MenuName}/{Name(mode)} (selected{breech})" : $"{S.MenuName}/{Name(mode)}";
					Add.Invoke(__instance, new object[] { label, (UnityAction)(() => SetMode(unit, mode, "player")) });
				}
			}
		}
	}
}
