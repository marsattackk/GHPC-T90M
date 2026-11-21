using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GHPC;
using GHPC.Weapons;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace T90M.Common
{
	// The net screens. GHPC treats them as slat armour with a fixed 0.65 chance, which is swapped here for SlatChance.
	internal static class NetScreens
	{
		internal const string NamePrefix = "net screen";
		internal const float Spoil = 0.55f;   // a net does 0.5-0.6 against a PG-7, but a particular vehicle's own figure isn't public
		const float GhpcSlatChance = 0.65f;

		internal static bool Active;

		static readonly AccessTools.FieldRef<LiveRound, bool> IsHeat = Reflect.FieldRefAccess<LiveRound, bool>("_isHeat");
		static readonly AccessTools.FieldRef<LiveRound, bool> JetActive = Reflect.FieldRefAccess<LiveRound, bool>("_jetActive");
		static readonly MethodInfo AddEventMethod = Reflect.Method(
			typeof(LiveRound),
			"AddEvent",
			new[] { typeof(string) });
		static readonly Action<LiveRound, string> AddEvent = AddEventMethod == null
			? null
			: AccessTools.MethodDelegate<Action<LiveRound, string>>(AddEventMethod);

		internal static bool IsNet(Collider c) => c != null && c.TryGetComponent<IArmor>(out var armor) && armor.IsSlatArmor
			&& armor.Name != null && armor.Name.StartsWith(NamePrefix, StringComparison.OrdinalIgnoreCase);

		internal static float SlatChance(Collider c) => IsNet(c) ? 1f : GhpcSlatChance;

		static bool _applied;

		// Not a [HarmonyPatch]: every port compiles this, and with two ports installed the first one's DLL handled the other's nets.
		internal static void Apply(HarmonyLib.Harmony harmony)
		{
			if (_applied) return;
			_applied = true;
			var target = Reflect.Method(typeof(LiveRound), "penCheck");
			if (target == null || IsHeat == null || JetActive == null || AddEvent == null) return;
			harmony.Patch(
				target,
				prefix: new HarmonyMethod(typeof(PenCheckPatch), "Prefix"),
				transpiler: new HarmonyMethod(typeof(PenCheckPatch), "Transpiler"));
		}

		static class PenCheckPatch
		{
			static readonly MethodInfo Chance = Reflect.Method(typeof(NetScreens), nameof(SlatChance));

			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
			{
				// The first port to patch swaps GHPC's 0.65 for SlatChance. Any other finds it gone and stands down.
				var list = code.ToList();
				int chanceIndex = list.FindIndex(i => i.opcode == OpCodes.Ldc_R4
					&& i.operand is float chance
					&& Mathf.Approximately(chance, GhpcSlatChance));
				bool other = list.Any(i => i.opcode == OpCodes.Call
					&& i.operand is MethodInfo method
					&& method.Name == nameof(SlatChance)
					&& method != Chance);
				if (chanceIndex < 0
					|| list
						.Skip(chanceIndex + 1)
						.Any(i => i.opcode == OpCodes.Ldc_R4
							&& i.operand is float chance
							&& Mathf.Approximately(chance, GhpcSlatChance)))
				{
					Active = false;
					if (!other)
						MelonLogger.Warning($"Net screens: GHPC's slat chance not found once in LiveRound.penCheck (game update or another mod's patch); net screens keep GHPC's slat rule.");
					return list;
				}
				var load = new CodeInstruction(OpCodes.Ldarg_1);
				load.labels.AddRange(list[chanceIndex].labels);
				list[chanceIndex] = load;
				list.Insert(chanceIndex + 1, new CodeInstruction(OpCodes.Call, Chance));
				Active = true;
				return list;
			}

			// Never throw out of a penCheck patch: a throw there loses the round for every vehicle in the game.
			static bool Prefix(LiveRound __instance, Collider cObject, ref bool __result)
			{
				try { return Net(__instance, cObject, ref __result); }
				catch (Exception exception)
				{
					PatchGuard.Failed("net screens", exception);
					return true;
				}
			}

			static bool Net(LiveRound __instance, Collider cObject, ref bool __result)
			{
				if (!Active || __instance.IsSpall || !IsHeat(__instance) || JetActive(__instance)) return true;
				var info = __instance.Info;
				if (info == null || info.Tandem || info.IgnoreSlat || !IsNet(cObject)) return true;
				var unit = cObject.GetComponent<IArmor>()?.Unit;
				if (unit != null && __instance.ShotInfo?.Shooter == unit) return true;
				if (ModRandom.Value < Spoil) return true;
				AddEvent(__instance, "Passed through net screen");
				// Nudged on past the net, the way GHPC moves a round through a detection zone.
				__instance.transform.position += __instance.transform.forward * 0.1f * Time.deltaTime;
				__result = true;
				return false;
			}
		}
	}
}
