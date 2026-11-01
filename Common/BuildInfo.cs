using System.Reflection;
using MelonLoader;

namespace T90M.Common
{
	// Not BuildInfo, MelonLoader already has one.
	internal static class BuildStamp
	{
		internal static string Text => typeof(BuildStamp).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false) is object[] a && a.Length > 0
			? ((AssemblyInformationalVersionAttribute)a[0]).InformationalVersion : "unstamped";

		internal static void Log(string name) => MelonLogger.Msg($"{name} {Text}");
	}
}
