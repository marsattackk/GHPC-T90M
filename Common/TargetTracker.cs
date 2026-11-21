using System;
using System.Collections.Generic;
using System.Linq;
using GHPC;
using GHPC.Camera;
using GHPC.Effects;
using GHPC.Equipment.Optics;
using GHPC.PhysicsHelpers;
using GHPC.Utility;
using GHPC.Weapons;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace T90M.Common
{
	[DefaultExecutionOrder(-50)]
	// Has to lay the aim before FireControlSystem's LateUpdate takes the aim's rate for the lead, or the lead lags a frame.
	internal sealed partial class TargetTracker : MonoBehaviour
	{
		internal enum Mode { Manual, Auto, Inertial }

		internal sealed class Settings
		{
			internal string Tag = "";
			internal float SlewRate = 5f;
			internal float PullIn = 0.1f;
			internal float InertialMax = 6f;
			internal float MissileFlash = 0.5f;
			internal float ReadyPixels = 8f;
			internal float SearchWidth = 3f;
			internal float SampleInterval = 0.1f;
			internal float Alpha = 0.15f, Beta = 0.01f, AlphaStart = 0.6f, BetaStart = 0.3f, Settle = 1.5f;
			internal float LossConfirm = 0.3f;
			internal float Cell = 0.25f;
			internal int MaxGrid = 21;
			internal Func<KeyCode> Key;
			internal WeaponSystem[] ResetOnFire;
			internal Func<string> ResetWhen;
			internal bool NeedsStabiliser = true;
		}

		internal sealed class Channel
		{
			internal string Name;
			internal UsableOptic Optic;
			internal CameraSlot Slot;
			internal int[] CaptureSteps = new int[0];
			internal int Lines = 576;
			internal float PixelFov;
			internal bool Thermal;
			internal string Group = "main";
			internal bool KeyCaptures = true, KeyResets = true;
			internal Func<AmmoType, bool> LaseCaptures;
			internal bool ZoomResets;
			internal bool AmmoResets;
			internal TextMeshProUGUI SymbolLeft, SymbolRight;
			internal string Left = "[", Right = "]";
			internal Marks Marks;
			internal int LastFov = -1;
			internal bool WasView;
		}

		internal FireControlSystem Fcs;
		internal Settings S;
		internal Channel[] Channels;
		internal Mode State { get; private set; }

		internal IUnit Target { get; private set; }

		internal string Group { get; private set; }

		internal Vector2 ImageHalf => _half;
		internal Vector3 TrackedPoint
		{
			get
			{
				var targetTransform = TargetTransform();
				return targetTransform == null ? Vector3.zero : State == Mode.Inertial ? Predicted() : Point;
			}
		}

		Transform TargetTransform()
		{
			if (Target == null) return null;
			try { return Target.transform; }
			catch (NullReferenceException) { return null; }
		}

		internal bool Ready => _ready;
		internal Vector2 Offset => _offset;
		internal Vector3 EyePoint => Eye(ActiveChannel());
		IUnit _own;

		Vector3 _local;
		readonly TrackFilter _filter = new TrackFilter();
		Vector2 _offset;
		Vector2 _pendingInput;
		Vector2 _half;
		Vector3 _lastCentre;
		float _lastCentreTime = -1f;
		float _nextSample;
		bool _ready;
		IUnit _readyUnit;
		Vector3 _readyLocal;
		Vector2 _readyHalf;
		bool _lased;
		Vector3 _prevWant;
		bool _hasPrevWant;
		readonly TrackServo _servo = new TrackServo();
		Channel _source;

		static readonly Dictionary<FireControlSystem, TargetTracker> ByFcs = new Dictionary<FireControlSystem, TargetTracker>();
		internal static TargetTracker For(FireControlSystem fcs)
			=> fcs != null && ByFcs.TryGetValue(fcs, out var tracker) && tracker != null ? tracker : null;

		internal static TargetTracker Register(
			FireControlSystem fcs,
			Settings s,
			Channel[] channels,
			ReticleSpec marks,
			List<string> problems)
		{
			if (fcs == null) return null;
			var tracker = fcs.gameObject.AddComponent<TargetTracker>();
			tracker.Fcs = fcs;
			tracker.S = s;
			tracker.Channels = channels.Where(c => c != null && c.Slot != null).ToArray();
			tracker._own = fcs.GetComponentInParent<IUnit>();
			tracker._servo.SlewRate = s.SlewRate;
			tracker._servo.PullIn = s.PullIn;
			foreach (var channel in tracker.Channels)
			{
				if (marks != null && channel.Optic != null && channel.CaptureSteps.Length > 0)
					channel.Marks = Marks.Build(marks, channel.Optic.transform, problems);
				channel.LastFov = channel.Slot.FovIndex;
				channel.WasView = channel.Slot.IsActive;
			}
			fcs.LaserFired += tracker.OnLase;
			if (s.ResetOnFire != null) foreach (var weapon in s.ResetOnFire) if (weapon != null) weapon.Fired += tracker.OnFired;
			ByFcs[fcs] = tracker;
			UnityKeys.Prune(ByFcs);
			return tracker;
		}

		void OnDestroy()
		{
			if (Fcs != null)
			{
				Fcs.LaserFired -= OnLase;
				ByFcs.Remove(Fcs);
			}
			if (S?.ResetOnFire != null) foreach (var weapon in S.ResetOnFire) if (weapon != null) weapon.Fired -= OnFired;
		}

		void OnLase() => _lased = true;

		void OnSelect(AmmoType.AmmoClip clip)
		{
			if (State == Mode.Manual) return;
			if (Channels.Any(c => c.Group == Group && c.AmmoResets))
				Reset($"round '{clip?.Name}' selected in group {Group}");
		}

		void OnFired(AmmoType a, LiveRound r)
		{
			if (State == Mode.Manual || a == null) return;
			if (a.Guidance != AmmoType.GuidanceType.Unguided)
			{
				EnterInertial($"missile '{a.Name}' launched (the picture's flash)", S.MissileFlash);
				return;
			}
			Reset($"'{a.Name}' fired");
		}

		void ChannelReset(Channel c, string why)
		{
			if (State != Mode.Manual && c.Group == Group) Reset(why);
		}

		internal void Reset(string why)
		{
			if (State == Mode.Manual) return;
			State = Mode.Manual;
			Target = null;
			_offset = Vector2.zero;
			_pendingInput = Vector2.zero;
			_hasPrevWant = false;
			_lastCentreTime = -1f;
			_servo.Reset();
		}

		internal void HandOver(IUnit unit, Vector3 worldPoint, Vector2 half, string group = "main")
		{
			if (unit == null || unit.transform == null) return;
			Capture(unit, unit.transform.InverseTransformPoint(worldPoint), half, group, "handed over");
		}

		void Capture(IUnit unit, Vector3 local, Vector2 half, string group, string how)
		{
			State = Mode.Auto;
			Target = unit;
			_local = local;
			Use();
			_filter.Capture(unit.transform.TransformPoint(local).ToV3(), Time.time);
			_half = half;
			_offset = Vector2.zero;
			_pendingInput = Vector2.zero;
			Group = group;
			_hasPrevWant = false;
			_lastCentreTime = -1f;
			_servo.Reset();
			_nextSample = 0f;
			if (_source == null || _source.Group != group)
				_source = Channels.FirstOrDefault(c => c.Group == group && c.CaptureSteps.Length > 0);
		}

		void EnterInertial(string why, float hold = 0f)
		{
			var targetTransform = TargetTransform();
			if (targetTransform == null) return;
			// The visible part's centre slows down as the target slides behind cover, so the target's own speed is used.
			var chassis = Target.Chassis;
			var speed = chassis != null ? chassis.Velocity.ToV3() : _filter.Measured ? _filter.Speed : new V3(0f, 0f, 0f);
			_filter.Freeze(speed, Time.time, hold);
			State = Mode.Inertial;
			_hasPrevWant = false;
		}

		Vector3 Point => ToVector3(_filter.Point);
		Vector3 Predicted() => ToVector3(_filter.Predicted(Time.time));
		static Vector3 ToVector3(V3 v) => new Vector3(v.x, v.y, v.z);

		void Use()
		{
			_filter.Alpha = S.Alpha;
			_filter.Beta = S.Beta;
			_filter.AlphaStart = S.AlphaStart;
			_filter.BetaStart = S.BetaStart;
			_filter.Settle = S.Settle;
			_filter.SampleInterval = S.SampleInterval;
			_filter.LossConfirm = S.LossConfirm;
			_filter.InertialMax = S.InertialMax;
			_filter.SearchWidth = S.SearchWidth;
		}

		Vector3 Eye(Channel c)
		{
			if (c?.Slot != null) return c.Slot.transform.position;
			if (_source?.Slot != null) return _source.Slot.transform.position;
			return Fcs.LaserOrigin != null ? Fcs.LaserOrigin.position : Fcs.ReferenceTransform.position;
		}

		bool Players()
		{
			var playerInput = GHPC.Player.PlayerInput.Instance;
			return playerInput != null && _own != null && (object)playerInput.CurrentPlayerUnit == _own;
		}

		Channel ActiveChannel()
		{
			foreach (var channel in Channels) if (channel.Slot != null && channel.Slot.IsActive) return channel;
			return null;
		}

		bool KeyDown()
		{
			var keyCode = S.Key != null ? S.Key() : KeyCode.None;
			return keyCode != KeyCode.None && Input.GetKeyDown(keyCode);
		}

		void LateUpdate()
		{
			if (Fcs == null || !Fcs.enabled) return;
			var view = ActiveChannel();
			string off = !Players()
				? "not the player's unit"
				: Fcs.FcsFault ? "fire control out of action" : S.NeedsStabiliser && !Fcs.StabsActive ? "stabiliser off"
					: S.ResetWhen?.Invoke();
			if (off != null)
			{
				Reset(off);
				_ready = false;
				_lased = false;
				WatchFields();
				ShowMarks(view);
				return;
			}
			WatchFields();
			if (State != Mode.Manual && view != null && view.Group != Group) Reset($"view changed to {view.Name} ({view.Group})");
			if (State != Mode.Manual && view != null && view.Slot.Damaged) Reset($"{view.Name} damaged");
			if (view != null && view.CaptureSteps.Length > 0 && (State == Mode.Manual || view.Group == Group)) _source = view;
			bool key = view != null && KeyDown();
			if (State == Mode.Manual)
			{
				bool lase = _lased && view != null && view.LaseCaptures != null && view.LaseCaptures(Fcs.CurrentAmmoType);
				UpdateReady(view, lase || key);
				if (_ready && view != null && (key && view.KeyCaptures || lase))
					Capture(_readyUnit, _readyLocal, _readyHalf, view.Group, key ? "key" : $"lased with '{Fcs.CurrentAmmoType?.Name}'");
			}
			else if (key && view.KeyResets) Reset("key");
			if (State != Mode.Manual) Track(view);
			_lased = false;
			_pendingInput = Vector2.zero;
			ShowMarks(view);
		}

		// ForceUpdateFov fires ZoomChanged with the field unchanged and an attach can put a field back, so the index has to actually move while the channel is the view.
		void WatchFields()
		{
			foreach (var channel in Channels)
			{
				int fovIndex = channel.Slot.FovIndex;
				bool isView = channel.Slot.IsActive;
				if (channel.ZoomResets && isView && channel.WasView && fovIndex != channel.LastFov)
					ChannelReset(channel, $"{channel.Name}: field changed (step {channel.LastFov} -> {fovIndex})");
				channel.LastFov = fovIndex;
				channel.WasView = isView;
			}
		}

		void UpdateReady(Channel view, bool now)
		{
			if (view == null || !view.CaptureSteps.Contains(view.Slot.FovIndex) || view.Slot.Damaged)
			{
				_ready = false;
				Why(view == null
					? "no tracker channel is the view"
					: view.Slot.Damaged ? $"{view.Name} damaged" : $"{view.Name} at step {view.Slot.FovIndex}, not a capture field");
				return;
			}
			if (!now && Time.time < _nextSample) return;
			_nextSample = Time.time + S.SampleInterval;
			_ready = false;
			var cam = view.Slot.transform;
			var eye = cam.position;
			var line = cam.forward;
			var mask = ConstantsAndInfoManager.Instance.LaserRangefinderLayerMask;
			if (!RaycastColliderUtils.Raycast(
				new Ray(eye + line * ClearOfOwn, line),
				out var hit,
				Fcs.MaxLaserRange > 0f ? Fcs.MaxLaserRange : 10000f,
				mask))
			{
				Why("nothing on the line");
				return;
			}
			var unit = Owner(hit.collider);
			if (unit == null || unit == _own || unit.transform == null || Smoked(eye, line, hit.distance + ClearOfOwn, view.Thermal))
			{
				Why(unit == null
					? $"first on the line '{hit.collider.name}' at {hit.distance + ClearOfOwn:0} m is no unit"
					: unit == _own ? $"first on the line is the own unit ('{hit.collider.name}')" : "smoke on the line");
				return;
			}
			var image = Sample(unit, eye, view.Thermal, S.Cell, S.MaxGrid);
			if (!image.Visible)
			{
				Why($"'{unit.FriendlyName}' on the line, no ray of its image reaches it ({image.Rays} rays)");
				return;
			}
			float pixels = Mathf.Min(
				image.Half.x,
				image.Half.y) * 2f / ((view.PixelFov > 0f ? view.PixelFov : VerticalFov(view.Slot)) * Mathf.Deg2Rad / view.Lines);
			if (pixels < S.ReadyPixels)
			{
				Why($"'{unit.FriendlyName}' image {pixels:0.0} lines, under {S.ReadyPixels}");
				return;
			}
			Why($"ready on '{unit.FriendlyName}' at {hit.distance + ClearOfOwn:0} m, {pixels:0.0} lines, {image.Hits} of {image.Rays} rays");
			_ready = true;
			_readyUnit = unit;
			_readyLocal = unit.transform.InverseTransformPoint(image.Centre);
			_readyHalf = image.Half;
		}

		void Why(string s) { }

		void Track(Channel view)
		{
			var targetTransform = TargetTransform();
			if (targetTransform == null)
			{
				Reset("target gone");
				return;
			}
			float deltaTime = Time.deltaTime;
			if (deltaTime <= 0f) return;
			var cam = view != null && view.Group == Group ? view : _source;
			var eye = Eye(cam);
			bool thermal = _source == null || _source.Thermal;
			if (Time.time >= _nextSample)
			{
				_nextSample = Time.time + S.SampleInterval;
				if (State == Mode.Auto)
				{
					var image = Sample(Target, eye, thermal, S.Cell, S.MaxGrid);
					if (image.Visible)
					{
						_filter.Correct(image.Centre.ToV3(), Time.time);
						_half = image.Half;
						_lastCentre = image.Centre;
						_lastCentreTime = Time.time;
					}
					else if (_filter.Missed(Time.time))
						EnterInertial($"target not seen for {_filter.LostFor(Time.time):0.0} s (covered, smoke or out of sight)");
				}
				else if (_filter.Searching(Time.time))
				{
					var image = Sample(Target, eye, thermal, S.Cell, S.MaxGrid);
					float halfMax = Mathf.Max(_half.x, _half.y);
					if (image.Visible && _filter.InWindow(eye.ToV3(), image.Centre.ToV3(), halfMax, Time.time))
					{
						_local = targetTransform.InverseTransformPoint(image.Centre);
						Use();
						_filter.Recapture(image.Centre.ToV3(), Time.time);
						_half = image.Half;
						State = Mode.Auto;
						_hasPrevWant = false;
						_servo.Reset();
						_lastCentre = image.Centre;
						_lastCentreTime = Time.time;
					}
					else if (_filter.Expired(Time.time))
					{
						Reset($"inertial longer than {S.InertialMax:0} s");
						return;
					}
				}
			}
			if (State == Mode.Auto) _filter.Coast(deltaTime);
			Steer(State == Mode.Auto ? Point : Predicted(), eye, deltaTime);
		}

		void Steer(Vector3 point, Vector3 eye, float dt)
		{
			var referenceTransform = Fcs.ReferenceTransform;
			var aim = Fcs.AimWorldVector.normalized;
			if (_pendingInput != Vector2.zero)
			{
				float lim = Mathf.Max(Mathf.Max(_half.x, _half.y) * 1.5f, 0.002f) * Mathf.Rad2Deg;
				var offset = _offset + _pendingInput;
				_pendingInput = Vector2.zero;
				if (Mathf.Abs(offset.x) > lim || Mathf.Abs(offset.y) > lim)
				{
					Reset("aimed away from the target");
					return;
				}
				_offset = offset;
			}
			var line = (point - eye).normalized;
			var want = (Quaternion.AngleAxis(_offset.x, referenceTransform.up) * Quaternion.AngleAxis(
				-_offset.y,
				referenceTransform.right) * line).normalized;
			var error = YawPitch(referenceTransform, aim, want);
			var dWant = _hasPrevWant ? YawPitch(referenceTransform, _prevWant, want) : Vector2.zero;
			_prevWant = want;
			_hasPrevWant = true;
			_servo.Step(error.x, error.y, dWant.x, dWant.y, dt, out float servoYaw, out float servoPitch);
			// Same call as the gunner's handles. GHPC builds the lead off AdjustAimByDegrees and nothing else.
			Fcs.AdjustAimByDegrees(servoYaw, -servoPitch);
		}

		static Vector2 YawPitch(Transform frame, Vector3 a, Vector3 b)
		{
			var localA = frame.InverseTransformDirection(a);
			var localB = frame.InverseTransformDirection(b);
			float yawA = Mathf.Atan2(localA.x, localA.z), yawB = Mathf.Atan2(localB.x, localB.z);
			float pitchA = Mathf.Atan2(
				localA.y,
				new Vector2(localA.x, localA.z).magnitude), pitchB = Mathf.Atan2(localB.y, new Vector2(localB.x, localB.z).magnitude);
			return new Vector2(Mathf.DeltaAngle(yawA * Mathf.Rad2Deg, yawB * Mathf.Rad2Deg), (pitchB - pitchA) * Mathf.Rad2Deg);
		}

		internal struct Image
		{
			internal bool Visible;
			internal Vector3 Centre;
			internal Vector2 Half;
			internal int Hits, Rays;
		}

		const float ClearOfOwn = 8f;   // from the laser window a ray to a target off to the side clips the tank's own turret stowage
		static readonly RaycastHit[] SmokeBuf = new RaycastHit[16];
		struct Hit
		{
			internal Vector3 P;
			internal float X, Y;
		}

		static readonly List<Hit> HitBuf = new List<Hit>();

		static readonly Dictionary<IUnit, Renderer[]> RenderersOf = new Dictionary<IUnit, Renderer[]>();
		static readonly Dictionary<Collider, IUnit> OwnerOf = new Dictionary<Collider, IUnit>();
		static readonly Vector3[] CornerBuf = new Vector3[8];

		static void CornersOf(Renderer r, Vector3[] into)
		{
			if (r is MeshRenderer)
			{
				var localBounds = r.localBounds;
				if (localBounds.size.sqrMagnitude > 1e-6f)
				{
					var localToWorld = r.transform.localToWorldMatrix;
					for (int i = 0;
						i < 8;
						i++)
							into[i] = localToWorld.MultiplyPoint3x4(new Vector3(
								(i & 1) == 0 ? localBounds.min.x : localBounds.max.x,
								(i & 2) == 0 ? localBounds.min.y : localBounds.max.y,
								(i & 4) == 0 ? localBounds.min.z : localBounds.max.z));
					return;
				}
			}
			var bounds = r.bounds;
			for (int i = 0;
				i < 8;
				i++)
					into[i] = new Vector3(
						(i & 1) == 0 ? bounds.min.x : bounds.max.x,
						(i & 2) == 0 ? bounds.min.y : bounds.max.y,
						(i & 4) == 0 ? bounds.min.z : bounds.max.z);
		}

		static IUnit Owner(Collider c)
		{
			if (c == null) return null;
			if (OwnerOf.TryGetValue(c, out var unit)) return unit;
			if (OwnerOf.Count > 4096) OwnerOf.Clear();
			return OwnerOf[c] = CodeUtils.FindComponentInParentFollowAware<IUnit>(c.transform);
		}

		static bool Smoked(Vector3 eye, Vector3 dir, float dist, bool thermal)
		{
			int hitCount = Physics.RaycastNonAlloc(eye, dir, SmokeBuf, dist, 1 << CodeUtils.LAYER_INDEX_VISIBILITYONLY);
			for (int i = 0; i < hitCount; i++)
			{
				var hit = SmokeBuf[i];
				if (!hit.collider.CompareTag("Smoke")) continue;
				if (!thermal) return true;
				var blocker = hit.collider.GetComponent<BlockerInformation>();
				if (blocker != null && blocker.IsMultispectral) return true;
			}
			return false;
		}

		internal static Image Sample(IUnit u, Vector3 eye, bool thermal, float cell = 0.25f, int maxGrid = 21)
		{
			var img = new Image();
			if (u == null || u.transform == null) return img;
			if (!RenderersOf.TryGetValue(u, out var renderers) || renderers == null || renderers.Any(r => r == null))
			{
				renderers = u.transform
					.GetComponentsInChildren<Renderer>(true)
					.Where(r => (r is MeshRenderer || r is SkinnedMeshRenderer)
						&& !(r.sharedMaterial != null && r.sharedMaterial.renderQueue >= 3000))
					.ToArray();
				if (RenderersOf.Count > 256) RenderersOf.Clear();
				RenderersOf[u] = renderers;
			}
			var box = new Bounds();
			bool any = false;
			foreach (var renderer in renderers)
			{
				if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
				var bounds = renderer.bounds;
				if (bounds.size.sqrMagnitude > 30f * 30f) continue;
				if (!any)
				{
					box = bounds;
					any = true;
				}
				else box.Encapsulate(bounds);
			}
			if (!any) return img;
			var fwd = (box.center - eye).normalized;
			var right = Vector3.Cross(Vector3.up, fwd).normalized;
			if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
			var up = Vector3.Cross(fwd, right);
			float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
			foreach (var renderer in renderers)
			{
				if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
				if (renderer.bounds.size.sqrMagnitude > 30f * 30f) continue;
				CornersOf(renderer, CornerBuf);
				for (int i = 0; i < 8; i++)
				{
					var corner = CornerBuf[i] - eye;
					float z = Mathf.Max(Vector3.Dot(corner, fwd), 0.1f);
					float x = Mathf.Atan2(Vector3.Dot(corner, right), z), y = Mathf.Atan2(Vector3.Dot(corner, up), z);
					minX = Mathf.Min(minX, x);
					maxX = Mathf.Max(maxX, x);
					minY = Mathf.Min(minY, y);
					maxY = Mathf.Max(maxY, y);
				}
			}
			float dist = Vector3.Distance(eye, box.center);
			float reach = dist + box.extents.magnitude + 5f;
			float cellAng = Mathf.Max(cell, 0.02f) / Mathf.Max(dist, 1f);
			int columns = Mathf.Clamp(Mathf.CeilToInt((maxX - minX) / cellAng), 5, Mathf.Max(maxGrid, 5));
			int rows = Mathf.Clamp(Mathf.CeilToInt((maxY - minY) / cellAng), 5, Mathf.Max(maxGrid, 5));
			var mask = ConstantsAndInfoManager.Instance.LaserRangefinderLayerMask;
			float cellWidth = (maxX - minX) / columns, cellHeight = (maxY - minY) / rows;
			float hx0 = float.MaxValue, hx1 = float.MinValue, hy0 = float.MaxValue, hy1 = float.MinValue;
			float sumX = 0f, sumY = 0f;
			HitBuf.Clear();
			for (int i = 0; i < columns; i++)
				for (int j = 0; j < rows; j++)
				{
					float angleX = minX + (i + 0.5f) * cellWidth, angleY = minY + (j + 0.5f) * cellHeight;
					var dir = (fwd + right * Mathf.Tan(angleX) + up * Mathf.Tan(angleY)).normalized;
					img.Rays++;
					if (!RaycastColliderUtils.Raycast(new Ray(eye + dir * ClearOfOwn, dir), out var hit, reach, mask)
						|| Owner(hit.collider) != u
						|| Smoked(eye, dir, hit.distance + ClearOfOwn, thermal))
						continue;
					img.Hits++;
					sumX += angleX;
					sumY += angleY;
					HitBuf.Add(new Hit { P = hit.point, X = angleX, Y = angleY });
					hx0 = Mathf.Min(hx0, angleX);
					hx1 = Mathf.Max(hx1, angleX);
					hy0 = Mathf.Min(hy0, angleY);
					hy1 = Mathf.Max(hy1, angleY);
				}
			if (img.Hits == 0) return img;
			img.Visible = true;
			float meanX = sumX / img.Hits, meanY = sumY / img.Hits;
			// The silhouette's centroid can fall beside the tank (hull, turret and barrel), so the hit nearest it is the aim point.
			float best = float.MaxValue;
			foreach (var candidateHit in HitBuf)
			{
				float distanceSquared = (candidateHit.X - meanX) * (candidateHit.X - meanX)
					+ (candidateHit.Y - meanY) * (candidateHit.Y - meanY);
				if (distanceSquared < best)
				{
					best = distanceSquared;
					img.Centre = candidateHit.P;
				}
			}
			img.Half = new Vector2((hx1 - hx0 + cellWidth) / 2f, (hy1 - hy0 + cellHeight) / 2f);
			return img;
		}

		static float VerticalFov(CameraSlot s)
		{
			var cam = CameraManager.MainCam;
			return s.IsActive && cam != null ? cam.fieldOfView : s.CurrentFov;
		}

		void ShowMarks(Channel view)
		{
			foreach (var channel in Channels)
			{
				if (channel.SymbolLeft != null) channel.SymbolLeft.text = State != Mode.Manual ? channel.Left : "";
				if (channel.SymbolRight != null) channel.SymbolRight.text = State != Mode.Manual ? channel.Right : "";
				if (channel.Marks == null) continue;
				bool visible = channel == view && channel.CaptureSteps.Contains(channel.Slot.FovIndex);
				if (!visible)
				{
					channel.Marks.Show(false, false, false, default, default);
					continue;
				}
				if (State == Mode.Manual)
				{
					channel.Marks.Show(_ready, false, false, default, default);
					continue;
				}
				var cam = channel.Slot.transform;
				var point = State == Mode.Auto ? Point : Predicted();
				var localPoint = cam.InverseTransformDirection(point - cam.position);
				float unitsPerRadian = 600f / (VerticalFov(channel.Slot) * Mathf.Deg2Rad);
				var centre = new Vector2(Mathf.Atan2(localPoint.x, localPoint.z), Mathf.Atan2(localPoint.y, localPoint.z)) * unitsPerRadian;
				channel.Marks.Show(false, State == Mode.Auto, State == Mode.Inertial, centre, _half * unitsPerRadian);
			}
		}

		internal sealed class Marks
		{
			struct Part
			{
				internal RectTransform Rt;
				internal Vector2 Pos, Size;
			}

			Part[] _ready, _lock, _memory;
			Vector2 _lockHalf, _memHalf;
			bool _rOn, _lOn, _mOn;

			internal static Marks Build(ReticleSpec spec, Transform parent, List<string> problems)
			{
				var overlay = ReticleBuild.BuildOverlay(spec, parent, (float)spec.Fov, problems);
				var marks = new Marks();
				Part[] Of(string name) => overlay.Root.GetComponentsInChildren<RectTransform>(true).Where(r => r.name == name)
					.Select(r => new Part { Rt = r, Pos = r.anchoredPosition, Size = r.sizeDelta }).ToArray();
				marks._ready = Of("ready");
				marks._lock = Of("lock");
				marks._memory = Of("memory");
				Vector2 Half(Part[] parts)
					=> parts.Length == 0
						? Vector2.zero
						: new Vector2(
							parts.Max(p => Mathf.Abs(p.Pos.x) + p.Size.x / 2f),
							parts.Max(p => Mathf.Abs(p.Pos.y) + p.Size.y / 2f));
				marks._lockHalf = Half(marks._lock);
				marks._memHalf = Half(marks._memory);
				if (marks._ready.Length == 0 || marks._lock.Length == 0 || marks._memory.Length == 0)
					problems.Add($"tracker marks '{spec.Name}': ready {marks._ready.Length}, lock {marks._lock.Length}, memory {marks._memory.Length} items (each wanted)");
				// The overlay comes out visible, so flag it on or the first Show skips the hide.
				marks._rOn = marks._lOn = marks._mOn = true;
				marks.Show(false, false, false, default, default);
				return marks;
			}

			internal void Show(bool ready, bool locked, bool memory, Vector2 centre, Vector2 half)
			{
				Set(_ready, ready, ref _rOn);
				Set(_lock, locked, ref _lOn);
				Set(_memory, memory, ref _mOn);
				if (locked) Fit(_lock, _lockHalf, centre, half);
				if (memory) Fit(_memory, _memHalf, centre, half);
			}

			static void Set(Part[] ps, bool on, ref bool was)
			{
				if (on == was) return;
				was = on;
				foreach (var part in ps) if (part.Rt != null) part.Rt.gameObject.SetActive(on);
			}

			static void Fit(Part[] ps, Vector2 nominal, Vector2 centre, Vector2 half)
			{
				var halfSize = new Vector2(Mathf.Clamp(half.x, nominal.x, 270f), Mathf.Clamp(half.y, nominal.y, 270f));
				var growth = halfSize - nominal;
				foreach (var part in ps)
				{
					if (part.Rt == null) continue;
					float signX = Mathf.Abs(part.Pos.x) < 0.01f
						? 0f
						: Mathf.Sign(part.Pos.x), signY = Mathf.Abs(part.Pos.y) < 0.01f ? 0f : Mathf.Sign(part.Pos.y);
					part.Rt.anchoredPosition = centre + part.Pos + new Vector2(signX * growth.x, signY * growth.y);
					part.Rt.sizeDelta = part.Size + new Vector2(signX == 0f ? 2f * growth.x : 0f, signY == 0f ? 2f * growth.y : 0f);
				}
			}
		}

		[HarmonyPatch(typeof(FireControlSystem), nameof(FireControlSystem.AdjustAimByRatio))]
		static class InputPatch
		{
			static void Prefix(FireControlSystem __instance, out Vector3 __state) => __state = __instance.AimWorldVector;

			static void Postfix(FireControlSystem __instance, float horizontalRatio, float verticalRatio, Vector3 __state)
			{
				try
				{
					if (horizontalRatio == 0f && verticalRatio == 0f) return;
					if (!ByFcs.TryGetValue(__instance, out var tracker)
						|| tracker == null
						|| tracker.State == Mode.Manual
						|| __state == Vector3.zero)
						return;
					tracker._pendingInput += YawPitch(
						__instance.ReferenceTransform,
						__state.normalized,
						__instance.AimWorldVector.normalized);
				}
				catch (Exception exception) { PatchGuard.Failed("Target tracker input", exception); }
			}
		}

		[HarmonyPatch(typeof(AmmoFeed), nameof(AmmoFeed.SetNextClipType))]
		static class SelectPatch
		{
			static void Prefix(AmmoFeed __instance, out AmmoType.AmmoClip __state)
			{
				__state = null;
				try { if (ByFcs.Count > 0) __state = __instance.QueuedClipType; }
				catch (Exception exception) { PatchGuard.Failed("Target tracker round select", exception); }
			}

			static void Postfix(AmmoFeed __instance, AmmoType.AmmoClip clipType, AmmoType.AmmoClip __state)
			{
				try
				{
					if (ByFcs.Count == 0 || clipType == null || clipType == __state) return;
					foreach (var tracker in ByFcs.Values)
						if (tracker != null
							&& tracker.Fcs.LinkedWeaponSystems != null
							&& tracker.Fcs.LinkedWeaponSystems.Any(w => w != null && w.Feed == __instance))
							tracker.OnSelect(clipType);
				}
				catch (Exception exception) { PatchGuard.Failed("Target tracker round select", exception); }
			}
		}

		[HarmonyPatch(typeof(CameraSlot), nameof(CameraSlot.ToggleFLIRPolarity))]
		static class PolarityPatch
		{
			static void Postfix(CameraSlot __instance)
			{
				try
				{
					foreach (var tracker in ByFcs.Values)
						if (tracker != null)
							foreach (var channel in tracker.Channels)
								if (channel.Slot == __instance && channel.Thermal)
									tracker.ChannelReset(channel, $"{channel.Name}: polarity changed");
				}
				catch (Exception exception) { PatchGuard.Failed("Target tracker polarity", exception); }
			}
		}
	}
}
