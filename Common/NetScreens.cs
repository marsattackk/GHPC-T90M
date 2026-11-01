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
	internal static class NetScreens
	{
		internal const string NamePrefix = "net screen";
		internal const float Spoil = 0.55f;
		const float GhpcSlatChance = 0.65f;

		internal static bool Active;

		static readonly AccessTools.FieldRef<LiveRound, bool> IsHeat = Reflect.FieldRefAccess<LiveRound, bool>("_isHeat");
		static readonly AccessTools.FieldRef<LiveRound, bool> JetActive = Reflect.FieldRefAccess<LiveRound, bool>("_jetActive");
		static readonly Action<LiveRound, string> AddEvent = AccessTools.MethodDelegate<Action<LiveRound, string>>(Reflect.Method(typeof(LiveRound), "AddEvent", new[] { typeof(string) }));

		internal static bool IsNet(Collider c) => c != null && c.TryGetComponent<IArmor>(out var a) && a.IsSlatArmor
			&& a.Name != null && a.Name.StartsWith(NamePrefix, StringComparison.OrdinalIgnoreCase);

		internal static float SlatChance(Collider c) => IsNet(c) ? 1f : GhpcSlatChance;

		static bool _applied;

		internal static void Apply(HarmonyLib.Harmony harmony)
		{
			if (_applied) return;
			_applied = true;
			var target = Reflect.Method(typeof(LiveRound), "penCheck");
			if (target == null) return;
			harmony.Patch(target, prefix: new HarmonyMethod(typeof(PenCheckPatch), "Prefix"), transpiler: new HarmonyMethod(typeof(PenCheckPatch), "Transpiler"));
		}

		static class PenCheckPatch
		{
			static readonly MethodInfo Chance = Reflect.Method(typeof(NetScreens), nameof(SlatChance));

			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
			{
				var list = code.ToList();
				int at = list.FindIndex(x => x.opcode == OpCodes.Ldc_R4 && x.operand is float f && Mathf.Approximately(f, GhpcSlatChance));
				bool other = list.Any(x => x.opcode == OpCodes.Call && x.operand is MethodInfo m && m.Name == nameof(SlatChance) && m != Chance);
				if (at < 0 || list.Skip(at + 1).Any(x => x.opcode == OpCodes.Ldc_R4 && x.operand is float f && Mathf.Approximately(f, GhpcSlatChance)))
				{
					Active = false;
					if (!other) MelonLogger.Warning($"Net screens: GHPC's slat chance not found once in LiveRound.penCheck (game update or another mod's patch); net screens keep GHPC's slat rule.");
					return list;
				}
				var ld = new CodeInstruction(OpCodes.Ldarg_1);
				ld.labels.AddRange(list[at].labels);
				list[at] = ld;
				list.Insert(at + 1, new CodeInstruction(OpCodes.Call, Chance));
				Active = true;
				return list;
			}

			static bool Prefix(LiveRound __instance, Collider cObject, ref bool __result)
			{
				if (!Active || __instance.IsSpall || !IsHeat(__instance) || JetActive(__instance)) return true;
				var info = __instance.Info;
				if (info == null || info.Tandem || info.IgnoreSlat || !IsNet(cObject)) return true;
				var unit = cObject.GetComponent<IArmor>().Unit;
				if (unit != null && __instance.ShotInfo.Shooter == unit) return true;
				if (UnityEngine.Random.value < Spoil) return true;
				AddEvent(__instance, "Passed through net screen");
				__instance.transform.position += __instance.transform.forward * 0.1f * Time.deltaTime;
				__result = true;
				return false;
			}
		}
	}
}
