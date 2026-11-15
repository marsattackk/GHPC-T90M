using System.Collections.Generic;
using System.Linq;
using GHPC;
using GHPC.Camera;
using GHPC.Equipment.Optics;
using GHPC.Weaponry;
using GHPC.Weapons;
using T90M.Common;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace T90M
{
	// The panorama's TV and thermal channels.
	internal static partial class Commander
	{
		static void SetupTv(
			UsableOptic o,
			string name,
			FireControlSystem fcs,
			Transform head,
			Transform level,
			WeaponSystem[] weapons,
			AmmoCodexScriptable[] rounds,
			CameraSlot onDestroy,
			bool ownFcs,
			List<string> problems)
		{
			o.name = name;
			var slot = o.slot;
			if (slot.gameObject != o.gameObject) slot.gameObject.name = name;
			SlotReparent.SetValue(slot, head);
			o.FCS = fcs;
			o.RegisterAtStart = ownFcs;
			o.Mounts = new AimablePlatform[0];
			o.LevelTransform = level;
			if (rounds != null) o.RegisteredAmmoTypes = rounds;
			slot.ExclusiveWeapons = weapons;
			slot.SlotToUseOnDestroy = onDestroy;
			slot.IsExterior = false;
			slot.DefaultFov = TvWide;
			slot.OtherFovs = new[] { TvNarrow };
			slot.AllowFreeZoom = false;
			slot.fovAspect = false;
			slot.VisionType = NightVisionType.None;
			slot.NightSightAtNightOnly = false;
			slot.ZoomInAudioEvent = "event:/Effects/Optic/Optic_Zoom_In";
			slot.ZoomOutAudioEvent = "event:/Effects/Optic/Optic_Zoom_Out";
			SightMotion.Stabilised(slot);
			if (ownFcs) GunnerFcs.Apply(o, PanFcs);
			else
			{
				o.Alignment = OpticAlignment.BoresightStabilized;
				o.RotateElevation = true;
				o.RotateAzimuth = true;
				o.CantCorrect = true;
				o.ForceHorizontalReticleAlign = true;
				o.LocalElevationLimits = PanFcs.LineOfSightLimits;
				GunnerFcs.AimClamp.Register(fcs, o);
			}
			if (o.reticleMesh != null) o.reticleMesh.gameObject.SetActive(false);
			MonoPicture(o, name);
			o.gameObject.AddComponent<ThermalSight.NoScopeMask>().Init(slot);
			var wide = ReticleBuild.BuildOverlay(Reticles.Spec("pk_pan_tv_wide.jsonc"), o.transform, TvWide, problems);
			var narrow = ReticleBuild.BuildOverlay(Reticles.Spec("pk_pan_tv_narrow.jsonc"), o.transform, TvNarrow, problems);
			var row = Row(o, problems);
			ZoomReticles.Attach(o, null, new[] { new[] { wide.Root }, new[] { narrow.Root, row.Root } });
		}

		static void SetupThermal(
			UsableOptic day,
			UsableOptic o,
			string name,
			FireControlSystem fcs,
			Transform head,
			WeaponSystem[] weapons,
			AmmoCodexScriptable[] rounds,
			CameraSlot onDestroy,
			List<string> problems)
		{
			o.name = name;
			var slot = o.slot;
			if (slot.gameObject != o.gameObject) slot.gameObject.name = name;
			SlotReparent.SetValue(slot, head);
			o.FCS = fcs;
			o.RegisterAtStart = false;
			o.Mounts = new AimablePlatform[0];
			if (rounds != null) o.RegisteredAmmoTypes = rounds;
			slot.ExclusiveWeapons = weapons;
			slot.SlotToUseOnDestroy = onDestroy;
			slot.IsExterior = false;
			slot.LinkedNightSight = null;
			day.slot.LinkedNightSight = slot;
			ThermalSight.Apply(day, o, new ThermalSight.Settings
			{
				Tag = Tag,
				Name = name,
				WideFov = Sights.ThermalWide,
				OtherFovs = new[] { Sights.ThermalNarrow, Sights.ThermalZoom },
				ElectronicFov = Sights.ThermalZoom,
				Width = Sights.ThermalWidth,
				Height = Sights.ThermalHeight,
				ElevationLimits = PanFcs.LineOfSightLimits,
				Reticle = x => ThermalMarks(x, problems),
				Monitor = true,
				Crt = false,
			});
		}

		static ReticleBuild.Overlay ThermalOverlay(string file, UsableOptic o, float fov, List<string> problems)
			=> ReticleBuild.BuildOverlay(Reticles.Spec(file), o.transform, fov, problems);

		static string ThermalMarks(UsableOptic o, List<string> problems)
		{
			if (o.reticleMesh != null) o.reticleMesh.gameObject.SetActive(false);
			var wide = ThermalOverlay("sosna_u_thermal_wide.jsonc", o, Sights.ThermalWide, problems);
			var narrow = ThermalOverlay("sosna_u_thermal_narrow.jsonc", o, Sights.ThermalNarrow, problems);
			var zoom = ThermalOverlay("sosna_u_thermal_zoom.jsonc", o, Sights.ThermalZoom, problems);
			var row = Row(o, problems);
			ZoomReticles.Attach(o, null, new[] { new[] { wide.Root }, new[] { narrow.Root, row.Root }, new[] { zoom.Root, row.Root } });
			return "the «Сосна-У» thermal marks";
		}

		static ReticleBuild.Overlay Row(UsableOptic o, List<string> problems)
		{
			var spec = Reticles.Spec("sosna_u_thermal_row.jsonc");
			var row = ReticleBuild.BuildOverlay(spec, o.transform, (float)spec.Fov, problems);
			string mono = spec.Num("Mono").ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
			SightReadout.Attach(o, row, new SightReadout.Settings
			{
				Range = "range", RangePrefix = $"<mspace={mono}em>", RangeSuffix = "</mspace>", RangeQuantize = 5,
				Ready = "ready",
				Ammo = "ammo", AmmoText = a => Sights.ThermalLetter(a),
			}, problems);
			return row;
		}

		static PostProcessProfile _mono;
		internal static void MonoPicture(UsableOptic o, string name)
		{
			if (_mono == null)
			{
				_mono = ScriptableObject.CreateInstance<PostProcessProfile>();
				_mono.name = $"{Tag} black-and-white TV";
				var grading = _mono.AddSettings<ColorGrading>();
				grading.enabled.Override(true);
				grading.saturation.Override(-100f);
				Object.DontDestroyOnLoad(_mono);
			}
			var host = new GameObject(name + " post (black-and-white)") { layer = o.post != null ? o.post.gameObject.layer : 16 };
			host.transform.SetParent(o.transform, false);
			var monoVolume = host.AddComponent<PostProcessVolume>();
			monoVolume.isGlobal = true;
			monoVolume.weight = 1f;
			monoVolume.priority = 100f;
			monoVolume.sharedProfile = _mono;
			monoVolume.enabled = false;
			o.postAdditional = (o.postAdditional ?? new PostProcessVolume[0]).Where(v => v != null).Append(monoVolume).ToArray();
		}
	}
}
