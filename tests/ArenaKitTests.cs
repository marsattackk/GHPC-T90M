using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using T90M.Common;
using Xunit;

namespace T90M.Tests
{
	// The Arena-M kit's text in Resources\ as it ships (the bundle's list of what the kit puts where) and the rules that decide
	// which T-90Ms carry it.
	public class ArenaKitTests
	{
		static readonly string[] RigGroups = { "HULL", "TURRET", "GUN", "MANTLET_COVER" };

		static PortKits.Kit Shipped(List<string> problems)
		{
			var kits = PortKits.Parse(File.ReadAllText(Repo.At("Resources", "kits.txt")), problems);
			return kits.Single(kit => kit.Name == "arena");
		}

		[Fact]
		public void The_shipped_kits_text_reads_without_a_problem_and_holds_the_arena_kit()
		{
			var problems = new List<string>();
			var kit = Shipped(problems);
			Assert.Empty(problems);
			Assert.NotEmpty(kit.Roots);
			Assert.NotEmpty(kit.ArmourGroups);
		}

		[Fact]
		public void Every_root_and_armour_group_is_named_for_the_kit_and_rides_a_rig_group()
		{
			var kit = Shipped(new List<string>());
			foreach (var (group, root) in kit.Roots)
			{
				Assert.Contains(group, RigGroups);
				Assert.Equal("KIT_ARENA", root);
			}
			Assert.Equal(kit.Roots.Count, kit.Roots.Select(root => root.Group).Distinct().Count());
			foreach (var (armourGroup, rigGroup) in kit.ArmourGroups)
			{
				Assert.Contains(rigGroup, new[] { "HULL", "TURRET", "GUN" });
				Assert.Equal("KIT_ARENA_" + rigGroup, armourGroup);
			}
		}

		[Fact]
		public void Nothing_is_hidden_or_dropped_twice_and_every_hide_names_a_rig_group()
		{
			var kit = Shipped(new List<string>());
			foreach (var (group, path) in kit.Hides)
			{
				Assert.Contains(group, RigGroups);
				Assert.False(string.IsNullOrWhiteSpace(path));
			}
			Assert.Equal(kit.Hides.Count, kit.Hides.Distinct().Count());
			Assert.Equal(kit.ArmourHides.Count, kit.ArmourHides.Distinct().Count());
			Assert.DoesNotContain(kit.ArmourHides, name => name.StartsWith("kit_"));
		}

		[Theory]
		[InlineData(-5, 0)]
		[InlineData(0, 0)]
		[InlineData(30, 30)]
		[InlineData(100, 100)]
		[InlineData(150, 100)]
		public void The_share_is_kept_to_0_to_100(int given, int want) =>
			Assert.Equal(want, PortKits.ClampShare(given, out _));

		[Fact]
		public void Share_0_carries_no_other_tank_and_share_100_every_one()
		{
			var random = new Random(42);
			for (int i = 0; i < 5000; i++)
			{
				Assert.False(PortKits.Decide(false, true, 0, random, out _));
				Assert.True(PortKits.Decide(false, false, 100, random, out _));
			}
		}

		[Fact]
		public void The_players_tank_follows_its_switch_and_is_never_rolled()
		{
			var random = new Random(42);
			Assert.True(PortKits.Decide(true, true, 0, random, out double draw));
			Assert.Equal(-1.0, draw);
			Assert.False(PortKits.Decide(true, false, 100, random, out draw));
			Assert.Equal(-1.0, draw);
		}
	}
}
