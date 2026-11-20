using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace T90M.Common
{
	internal static partial class PortKits
	{
		internal sealed class Kit
		{
			public string Name;
			public readonly List<(string Group, string Root)> Roots = new List<(string, string)>();
			public readonly List<(string Group, string Path)> Hides = new List<(string, string)>();
			public readonly List<float[]> SmokePoints = new List<float[]>();
			public readonly List<(string Group, string Path)> SmokeCaps = new List<(string, string)>();
			public readonly List<(string ArmourGroup, string RigGroup)> ArmourGroups = new List<(string, string)>();
			public readonly List<string> ArmourHides = new List<string>();
			public readonly List<string> RigLines = new List<string>();
			public readonly List<(string Piece, string Container)> EraLinks = new List<(string, string)>();
			public readonly List<string> ModuleHides = new List<string>();
		}

		internal static List<Kit> Parse(string text, List<string> problems)
		{
			var kits = new List<Kit>();
			Kit current = null;
			var lines = (text ?? "").Replace("\r", "").Split('\n');
			for (int number = 1; number <= lines.Length; number++)
			{
				string line = lines[number - 1].Trim();
				if (line.Length == 0 || line[0] == '#') continue;
				var words = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				string kind = words[0];
				string Rest(int from) => string.Join(" ", words.Skip(from));
				void Bad(string why) => problems?.Add($"line {number}: {why}");
				if (kind == "kit")
				{
					if (words.Length != 2)
					{
						Bad("'kit' wants one name");
						continue;
					}
					current = kits.FirstOrDefault(k => string.Equals(k.Name, words[1], StringComparison.OrdinalIgnoreCase));
					if (current == null)
					{
						current = new Kit { Name = words[1] };
						kits.Add(current);
					}
					continue;
				}
				if (current == null)
				{
					Bad($"'{kind}' before any 'kit' line");
					continue;
				}
				switch (kind)
				{
					case "root":
						if (words.Length == 3) current.Roots.Add((words[1], words[2]));
						else Bad("'root' wants a group and an object name");
						break;
					case "hide":
						if (words.Length >= 3) current.Hides.Add((words[1], Rest(2)));
						else Bad("'hide' wants a group and a path");
						break;
					case "smoke_point":
						if (words.Length == 4 && TryFloats(words, 1, 3, out var point)) current.SmokePoints.Add(point);
						else Bad("'smoke_point' wants three numbers");
						break;
					case "smoke_cap":
						if (words.Length >= 3) current.SmokeCaps.Add((words[1], Rest(2)));
						else Bad("'smoke_cap' wants a group and a path");
						break;
					case "armour_group":
						if (words.Length == 3) current.ArmourGroups.Add((words[1], words[2]));
						else Bad("'armour_group' wants an armour group and a rig group");
						break;
					case "armour_hide":
						if (words.Length >= 2) current.ArmourHides.Add(Rest(1));
						else Bad("'armour_hide' wants a piece name");
						break;
					case "rig":
						if (words.Length >= 2 && (words[1].StartsWith("hatch|") || words[1].StartsWith("flap|"))) current.RigLines.Add(Rest(1));
						else Bad("'rig' wants a hatch|... or flap|... entry");
						break;
					case "era_link":
						if (words.Length == 3) current.EraLinks.Add((words[1], words[2]));
						else Bad("'era_link' wants an armour piece and a container object");
						break;
					case "module_hide":
						if (words.Length == 2) current.ModuleHides.Add(words[1]);
						else Bad("'module_hide' wants a module id");
						break;
					default:
						Bad($"unknown '{kind}'");
						break;
				}
			}
			return kits;
		}

		static bool TryFloats(string[] words, int from, int count, out float[] values)
		{
			values = new float[count];
			for (int i = 0; i < count; i++)
				if (!float.TryParse(words[from + i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])
					|| float.IsNaN(values[i])
					|| float.IsInfinity(values[i]))
					return false;
			return true;
		}

		internal static int ClampShare(double value, out bool changed)
		{
			changed = false;
			if (double.IsNaN(value))
			{
				changed = true;
				return 0;
			}
			double rounded = Math.Round(value, MidpointRounding.AwayFromZero);
			if (rounded < 0)
			{
				changed = true;
				return 0;
			}
			if (rounded > 100)
			{
				changed = true;
				return 100;
			}
			changed = rounded != value;
			return (int)rounded;
		}

		internal static bool TryParseShare(string text, out int share, out bool changed)
		{
			share = 0;
			changed = false;
			if (!double.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
				return false;
			share = ClampShare(value, out changed);
			return true;
		}

		internal static bool Roll(int share, Random random, out double draw)
		{
			draw = random.NextDouble() * 100.0;
			return draw < share;
		}

		internal static Random NewRandom()
		{
			int seed = unchecked(Environment.TickCount * 397 ^ Guid.NewGuid().GetHashCode());
			return new Random(seed);
		}

		internal static bool Decide(bool isPlayer, bool playerSwitch, int share, Random random, out double draw)
		{
			draw = -1.0;
			return isPlayer ? playerSwitch : Roll(share, random, out draw);
		}

		internal sealed class Tally
		{
			public bool PlayerSeen, PlayerKit;
			public int Others, OtherKits;

			public void Add(bool isPlayer, bool kit)
			{
				if (isPlayer)
				{
					PlayerSeen = true;
					PlayerKit = kit;
				}
				else
				{
					Others++;
					if (kit) OtherKits++;
				}
			}

			public int Total => Others + (PlayerSeen ? 1 : 0);

			public string Line(string kitLabel, string unitName, int share) =>
				$"{kitLabel}: your tank {(PlayerKit ? "yes" : "no")}; {OtherKits} of {Others} other {unitName}s (share {share} %)";
		}
	}
}
