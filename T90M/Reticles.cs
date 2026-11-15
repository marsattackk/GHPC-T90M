using System.Collections.Generic;
using GHPC.Weaponry;
using Reticle;
using T90M.Common;

namespace T90M
{
	internal static class Reticles
	{
		static readonly PortReticles All = new PortReticles(typeof(Reticles).Assembly, T90MMod.FriendlyName);

		internal static ReticleSpec Spec(string file) => All.Spec(file);

		internal static ReticleSO Build(string file, ReticleSO host, ReticleSO fontHost, Dictionary<string, AmmoCodexScriptable> rounds) =>
			All.Build(file, host, fontHost, rounds);
	}
}
