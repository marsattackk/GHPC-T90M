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
	internal static class AirBurst
	{
		internal enum Mode { Short, Over, Impact }

		internal sealed class Settings
		{
			public string Tag = "Port";
			public string RoundName;
			public string MenuName;
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

		internal static Settings S;

		internal sealed class Fuse
		{
			public Mode Mode;
			public float Range;
			public float Time = -1f;
			public override string ToString() => Time < 0f ? $"impact{(Mode != Mode.Impact ? $" (no valid range: {Range:0} m)" : "")}" : $"{Name(Mode)}, {Range:0} m, {Time:0.000} s";
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
			public Vector3 Point, Dir;
			public float Speed;
		}

		static readonly Dictionary<LiveRound, Flight> Flights = new Dictionary<LiveRound, Flight>();

		internal static string Name(Mode m) => m == Mode.Short ? $"short of the target ({S?.ShortOf:0} m)" : m == Mode.Over ? $"over the target ({S?.Over:0} m)" : "impact";

		internal static bool Ours(AmmoType a) => S != null && a != null && a.Name == S.RoundName;

		internal static void Attach(Vehicle vehicle)
		{
			if (S == null || vehicle == null) return;
			var gun = vehicle.GetComponent<WeaponsManager>()?.Weapons?.FirstOrDefault()?.Weapon;
			if (gun?.Feed == null) return;
			Stations.RemoveAll(x => x.Gun == null);
			if (Stations.Any(x => x.Gun == gun)) return;
			var st = new Station { Unit = vehicle, Gun = gun, Fcs = gun.FCS, Mode = S.Default };
			gun.Feed.LoadedRoundInBreech += a => Loaded(st, a);
			gun.Fired += (a, r) => Fired(st, a, r);
			Stations.Add(st);
		}

		internal static void SetMode(Unit u, Mode m)
		{
			var st = Stations.FirstOrDefault(x => x.Unit != null && x.Unit == u);
			if (st == null) return;
			st.Mode = m;
		}

		static void Loaded(Station st, AmmoType a)
		{
			if (!Ours(a)) { st.Breech = null; return; }
			float range = st.Fcs != null ? st.Fcs.CurrentRange : 0f;
			var f = new Fuse { Mode = st.Mode, Range = range };
			if (st.Mode != Mode.Impact && range >= S.MinRange)
			{
				// The range runs from the laser, the round starts at the muzzle about 6 m ahead of it.
				float to = st.Mode == Mode.Short ? range - S.ShortOf : range;
				var dir = st.Fcs.AimWorldVector.sqrMagnitude > 1e-6f ? st.Fcs.AimWorldVector.normalized : st.Gun.MuzzleIdentity.forward;
				var from = st.Fcs.LaserOrigin != null ? st.Fcs.LaserOrigin : st.Fcs.ReferenceTransform;
				float ahead = st.Gun.MuzzleIdentity != null && from != null ? Vector3.Dot(st.Gun.MuzzleIdentity.position - from.position, dir) : 0f;
				f.Time = FlightTime(a, dir, to - ahead);
			}
			st.Breech = f;
		}

		// Not GHPC's ballistic computer, its flight times come out one 0.02 s step short.
		static float FlightTime(AmmoType a, Vector3 dir, float distance)
		{
			if (distance <= 0f) return 0f;
			float v0 = a.MuzzleVelocity * (1f + 0.0004f * (GHPC.World.WorldEnvironmentManager.AmmoTempFahrenheit - 70f));
			var s = new MotionState { position = Vector3.zero, velocity = dir.normalized * v0 };
			const float step = 0.002f;
			for (float t = 0f; t < 20f; t += step)
			{
				var n = a.UseErrorCorrection ? BallisticEvaluatorRK4.EvaluateTimestep(s, step, a) : BallisticEvaluatorNonCorrected.EvaluateTimestep(s, step, a);
				float d0 = s.position.magnitude, d1 = n.position.magnitude;
				if (d1 >= distance) return t + step * Mathf.Clamp01((distance - d0) / Mathf.Max(1e-5f, d1 - d0));
				s = n;
			}
			return -1f;
		}

		static void Fired(Station st, AmmoType a, LiveRound r)
		{
			var f = st.Breech;
			st.Breech = null;
			if (!Ours(a) || r == null) return;
			foreach (var dead in Flights.Where(x => x.Key == null || x.Key.IsDestroyed || x.Key.ID != x.Value.Id).Select(x => x.Key).ToList()) Flights.Remove(dead);
			if (f == null || f.Time < 0f) return;
			float scatter = Gaussian() * S.TimeSd;
			var fl = new Flight { Id = r.ID, Fuse = f, At = Mathf.Max(0.02f, f.Time + scatter)};
			Flights[r] = fl;
		}

		static float Gaussian()
		{
			float u1 = 1f - UnityEngine.Random.value, u2 = UnityEngine.Random.value;
			return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
		}

		static readonly AccessTools.FieldRef<LiveRound, MotionState> Motion = Reflect.FieldRefAccess<LiveRound, MotionState>("_currentMotionState");
		static readonly AccessTools.FieldRef<LiveRound, bool> FuseActive = Reflect.FieldRefAccess<LiveRound, bool>("_rangedFuseActive");
		static readonly AccessTools.FieldRef<LiveRound, float> FuseLeft = Reflect.FieldRefAccess<LiveRound, float>("_rangedFuseCountdown");
		static readonly AccessTools.FieldRef<LiveRound, bool> Armed = Reflect.FieldRefAccess<LiveRound, bool>("_armed");
		static readonly AccessTools.FieldRef<LiveRound, bool> DrewEffect = Reflect.FieldRefAccess<LiveRound, bool>("_didNonTerrainImpactVfx");

		[HarmonyPatch(typeof(LiveRound), "DoUpdateImpl")]
		static class RoundPatch
		{
			static void Prefix(LiveRound __instance, float __0)
			{
				float dt = __0;
				if (Flights.Count == 0 || !Flights.TryGetValue(__instance, out var f)) return;
				try { Step(__instance, f, dt); }
				catch (Exception e) { Flights.Remove(__instance); PatchGuard.Failed($"{S?.Tag} {S?.RoundName} fuse", e); }
			}

			// GHPC picks a burst's effect by the surface it hit, in thin air it finds none and draws nothing.
			static void Postfix(LiveRound __instance)
			{
				if (_drawn == null || _drawn != __instance) return;
				var at = _drawnAt;
				_drawn = null;
				if (DrewEffect(__instance) || ParticleEffectsManager.Instance == null) return;
				ParticleEffectsManager.Instance.CreateImpactEffectOfType(__instance.Info, ParticleEffectsManager.FusedStatus.Fuzed, ParticleEffectsManager.SurfaceMaterial.Steel, false, at);
			}
		}

		static LiveRound _drawn;
		static Vector3 _drawnAt;

		static void Step(LiveRound r, Flight f, float dt)
		{
			if (r.IsDestroyed || r.ID != f.Id) { Flights.Remove(r); return; }
			if (r.TimeTraveled + dt < f.At) return;
			Flights.Remove(r);
			float tau = Mathf.Max(0f, f.At - r.TimeTraveled);
			var s0 = Motion(r);
			var s1 = tau <= 0f ? s0 : r.UseErrorCorrection ? BallisticEvaluatorRK4.EvaluateTimestep(s0, tau, r.Info) : BallisticEvaluatorNonCorrected.EvaluateTimestep(s0, tau, r.Info);
			var from = r.transform.position;
			var d = s1.position - from;
			if (d.sqrMagnitude > 1e-6f && RaycastColliderUtils.Raycast(new Ray(from, d), out var hit, d.magnitude, ConstantsAndInfoManager.Instance.LiveRoundLayerMask))
			{
				return;
			}
			// GHPC bursts a timed round where it was at the frame's start, so it goes onto the burst point first.
			r.transform.position = s1.position;
			Motion(r) = s1;
			FuseActive(r) = true;
			FuseLeft(r) = 0f;
			Armed(r) = true;   // rounds only arm on a hit, unarmed it bursts as a dud
			_drawn = r;
			_drawnAt = s1.position;
			f.Point = s1.position;
			f.Dir = s1.velocity.sqrMagnitude > 1e-6f ? s1.velocity.normalized : r.transform.forward;
			f.Speed = s1.velocity.magnitude;
			Elements(r, f);
		}

		static AmmoType _element;

		static AmmoType Element() => _element ?? (_element = FragmentCone.Type(S.RoundName + " element", S.ElementMass, S.ElementArea, S.ElementCoeff, S.ElementCaliber, S.ElementPen, S.ElementPenSpeed));

		static void Elements(LiveRound shell, Flight f)
		{
			if (S.Elements <= 0) return;
			float v = f.Speed + S.BlockSpeed;
			float pen = S.ElementPen * (v / S.ElementPenSpeed) * (v / S.ElementPenSpeed);
			FragmentCone.Spawn(Element(), f.Point, f.Dir, S.HalfAngle, S.Elements, v, pen, shell.Shooter, shell.NpcRound, shell, $"{S.RoundName} element",
				() => UnityEngine.Random.value, penAfterInit: true, fallbackRight: shell.transform.right
				);
		}

		static float OverHeight(FireControlSystem fcs)
		{
			if (S == null || Stations.Count == 0 || fcs == null) return 0f;
			for (int i = 0; i < Stations.Count; i++)
			{
				var st = Stations[i];
				if (st.Fcs != fcs) continue;
				var b = st.Breech;
				return b != null && b.Mode == Mode.Over && b.Time >= 0f && fcs.CurrentRange >= S.MinRange ? S.Over : 0f;
			}
			return 0f;
		}

		[HarmonyPatch(typeof(FireControlSystem), nameof(FireControlSystem.CompensatedWeaponVector))]
		static class AimVectorPatch
		{
			static void Postfix(FireControlSystem __instance, ref Vector3 __result)
			{
				if (S == null) return;
				float h = OverHeight(__instance);
				if (h > 0f) __result += Vector3.up * h;
			}
		}

		[HarmonyPatch(typeof(FireControlSystem), nameof(FireControlSystem.CompensatedWeaponAimPoint))]
		static class AimPointPatch
		{
			static void Postfix(FireControlSystem __instance, ref Vector3 __result)
			{
				if (S == null) return;
				float h = OverHeight(__instance);
				if (h > 0f) __result += Vector3.up * h;
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
				float h = OverHeight(_gate);
				if (h > 0f) __result += Mathf.Atan2(h, rangeMeters) * Mathf.Rad2Deg;
			}
		}

		[HarmonyPatch(typeof(PlayerInput), "AddSmokeButtons")]
		static class MenuPatch
		{
			static readonly MethodInfo Add = Reflect.Method(typeof(PlayerInput), "AddMenuButton", new[] { typeof(string), typeof(UnityAction) });

			static void Postfix(PlayerInput __instance)
			{
				if (S == null || Add == null) return;
				var u = __instance.CurrentPlayerUnit;
				var st = Stations.FirstOrDefault(x => x.Unit != null && x.Unit == u);
				if (st == null) return;
				string breech = st.Breech != null ? $"; in the breech: {(st.Breech.Time < 0f ? "impact" : $"{Name(st.Breech.Mode)} at {st.Breech.Range:0} m")}" : "";
				foreach (var m in new[] { Mode.Short, Mode.Over, Mode.Impact })
				{
					var mode = m;
					string label = mode == st.Mode ? $"{S.MenuName}/{Name(mode)} (selected{breech})" : $"{S.MenuName}/{Name(mode)}";
					Add.Invoke(__instance, new object[] { label, (UnityAction)(() => SetMode(u, mode)) });
				}
			}
		}
	}
}
