using GHPC.Utility;
using System.Reflection;
using GHPC.Crew;
using GHPC.Player;
using GHPC.Vehicle;
using GHPC.Weapons;
using HarmonyLib;
using T90M.Common;
using UnityEngine;
using UnityEngine.Events;

namespace T90M
{
	// The remote Kord follows the panorama and the gunner. These patches keep the two apart.
	internal static partial class Commander
	{
		[DefaultExecutionOrder(-40)]
		internal class Link : MonoBehaviour
		{
			Station _st;
			Vector3 _lasedLine;
			internal void Init(Station st)
			{
				_st = st;
				if (st.Fcs != null) st.Fcs.LaserFired += () => _lasedLine = st.Fcs.AimWorldVector;
			}

			internal static bool HandOverHeld()
			{
				bool held = false;
				DevHandOver(ref held);
				if (held) return true;
				try { return InputUtil.MainPlayer != null && InputUtil.MainPlayer.GetButton("Aim Override"); }
				catch (System.Exception exception)
				{
					PatchGuard.Failed("Kord MT aim override key", exception);
					return false;
				}
			}

			void Update()
			{
				if (_st == null || _st.Fcs == null || _st.MainFcs == null) return;
				bool held = _st.PanoramaView && HandOverHeld() && IsPlayers(_st);
				var panTracker = TargetTracker.For(_st.Fcs);
				if (_heldLast && !held && panTracker != null && panTracker.State == TargetTracker.Mode.Auto)
					TargetTracker.For(_st.MainFcs)?.HandOver(panTracker.Target, panTracker.TrackedPoint, panTracker.ImageHalf);
				_heldLast = held;
				if (_st.DublView) _st.Fcs.SetAimVector(_st.MainFcs.AimWorldVector, true);
				else if (_st.PanoramaView && HandOverHeld() && IsPlayers(_st))
				{
					var from = _st.Fcs.ReferenceTransform.position;
					var aim = _st.Fcs.AimWorldVector;
					// A range lased on another line puts the gun on the wrong point (the sights stand 2 m apart).
					bool lasedHere = _st.Fcs.CurrentRange > 0f
						&& !_st.Fcs.RangeInvalid
						&& _lasedLine != Vector3.zero
						&& Vector3.Angle(aim, _lasedLine) < 0.5f;
					if (lasedHere) _st.MainFcs.SetAimWorldPosition(from + aim.normalized * _st.Fcs.CurrentRange);
					else _st.MainFcs.SetAimVector(aim, true);
				}
				else if (!_st.PanoramaView
					&& IsPlayers(_st)
					&& _st.Turret != null
					&& (panTracker == null || panTracker.State == TargetTracker.Mode.Manual))
				{
					var aim = _st.Fcs.AimWorldVector;
					if (!_follow || Vector3.Angle(aim, _setLast) > 0.01f)
					{
						_onTurret = _st.Turret.InverseTransformDirection(aim);
						_follow = true;
					}
					_setLast = _st.Turret.TransformDirection(_onTurret);
					_st.Fcs.SetAimVector(_setLast, true);
					return;
				}
				_follow = false;
			}

			bool _follow;
			bool _heldLast;
			Vector3 _onTurret;
			Vector3 _setLast;
		}

		static bool IsPlayers(Station st) =>
			PlayerInput.Instance != null
			&& (object)PlayerInput.Instance.CurrentPlayerUnit == st.GetComponentInParent<Vehicle>();

		// GHPC's Aim Override swings the active weapon at the AI's target. In the panorama that key hands the line to the gunner instead.
		[HarmonyPatch(typeof(CrewBrainWeaponsModule), nameof(CrewBrainWeaponsModule.SetAimVector))]
		static class AimOverridePatch
		{
			static bool Prefix(CrewBrainWeaponsModule __instance, WeaponSystemRole weaponSystemRole)
			{
				try
				{
					if (weaponSystemRole != WeaponSystemRole.RoofGun || !Link.HandOverHeld()) return true;
					var input = PlayerInput.Instance;
					if (input == null || (object)input.CurrentPlayerCrewBrain?.WeaponsModule != __instance) return true;
					var unit = input.CurrentPlayerUnit as Vehicle;
					var station = unit != null ? unit.GetComponentInChildren<Station>(true) : null;
					return station == null || !station.PanoramaView;
				}
				catch (System.Exception exception)
				{
					PatchGuard.Failed("Kord MT aim override hand-over", exception);
					return true;
				}
			}
		}

		[HarmonyPatch(typeof(WeaponSystem), nameof(WeaponSystem.AbleToFire), MethodType.Getter)]
		static class BlindSectorPatch
		{
			static void Postfix(WeaponSystem __instance, ref bool __result)
			{
				try
				{
					if (!__result || !ByWeapon.TryGetValue(__instance, out var station) || station == null || !station.InBlindSector)
						return;
					__result = false;
				}
				catch (System.Exception exception) { PatchGuard.Failed("Kord MT blind sector", exception); }
			}
		}

		[HarmonyPatch(typeof(PlayerInput), "AddSmokeButtons")]
		static class RateMenuPatch
		{
			static readonly MethodInfo Add = Reflect.Method(
				typeof(PlayerInput),
				"AddMenuButton",
				new[] { typeof(string), typeof(UnityAction) });

			static void Postfix(PlayerInput __instance)
			{
				try
				{
					var unit = __instance.CurrentPlayerUnit as Vehicle;
					var station = unit != null ? unit.GetComponentInChildren<Station>(true) : null;
					if (station == null || station.Weapon == null || Add == null) return;
					bool low = !station.LowRate;
					string next = low ? $"low, {LowRoundsPerMinute:0}" : $"high, {RoundsPerMinute:0}";
					Add.Invoke(
						__instance,
						new object[]
						{
							$"Kord MT/Rate of fire: {next} rds/min (now {station.Rate:0})",
							(UnityAction)(() =>
							{
								station.SetLowRate(low);
							}),
						});
				}
				catch (System.Exception exception) { PatchGuard.Failed("Kord MT rate menu", exception); }
			}
		}
	}
}
