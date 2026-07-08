using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using UnityEngine;

namespace ClassicUs.VaderMod
{
    internal static class VaderSaberSystem
    {
        private const string RpcRequestSaberKey = "classicus.vadermod.RequestSaber";
        private const string RpcBroadcastSaberKey = "classicus.vadermod.BroadcastSaber";
        private const string RpcAimSaberKey = "classicus.vadermod.AimSaber";
        private const string RpcBroadcastAimSaberKey = "classicus.vadermod.BroadcastAimSaber";
        private const string RpcStopSaberKey = "classicus.vadermod.StopSaber";

        private sealed class SaberState
        {
            public byte OwnerId;
            public float StartedAt;
            public float Duration;
            public float Range;
            public float SpinSpeed;
            public float AimAngle;
            public float LastAimSentAt;
            public float LastAimSentAngle;
            public GameObject Object;
            public Transform VisualTransform;
            public SpriteRenderer Renderer;
        }

        private static readonly Dictionary<byte, SaberState> _active = new();

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref Point point);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out NativeRect rect);

        public static bool IsActive(byte ownerId) => _active.ContainsKey(ownerId);

        public static void RequestStart(byte ownerId)
        {
            var client = AmongUsClient.Instance;
            if (client != null && client.AmHost)
                StartSaber(ownerId, Time.time, VaderPlugin.ActiveDuration, VaderPlugin.ActiveRange, VaderPlugin.ActiveSpinSpeed, true);
            else
                ManactorAPI.SendRpcMethod(RpcRequestSaberKey, ownerId);
        }

        [ManactorRpc(RpcRequestSaberKey)]
        private static void OnRequestSaberRpc(byte senderId, byte ownerId)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;

            var owner = FindPlayer(ownerId);
            if (owner == null || owner.Data == null || owner.Data.IsDead || !VaderPlugin.IsVader(owner)) return;

            StartSaber(ownerId, Time.time, VaderPlugin.ActiveDuration, VaderPlugin.ActiveRange, VaderPlugin.ActiveSpinSpeed, true);
        }

        [ManactorRpc(RpcBroadcastSaberKey)]
        private static void OnBroadcastSaberRpc(byte senderId, byte ownerId, float duration, float range, float spinSpeed)
        {
            var client = AmongUsClient.Instance;
            if (client != null && client.AmHost) return;

            StartSaber(ownerId, Time.time, duration, range, spinSpeed, false);
        }

        [ManactorRpc(RpcStopSaberKey)]
        private static void OnStopSaberRpc(byte senderId, byte ownerId) => StopSaber(ownerId, false);

        [ManactorRpc(RpcAimSaberKey)]
        private static void OnAimSaberRpc(byte senderId, byte ownerId, float aimAngle)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            if (!_active.TryGetValue(ownerId, out var state)) return;

            state.AimAngle = aimAngle;
            ManactorAPI.SendRpcMethod(RpcBroadcastAimSaberKey, ownerId, aimAngle);
        }

        [ManactorRpc(RpcBroadcastAimSaberKey)]
        private static void OnBroadcastAimSaberRpc(byte senderId, byte ownerId, float aimAngle)
        {
            var client = AmongUsClient.Instance;
            if (client != null && client.AmHost) return;
            if (_active.TryGetValue(ownerId, out var state))
                state.AimAngle = aimAngle;
        }

        public static void Tick()
        {
            if (_active.Count == 0) return;

            var ended = new List<byte>();
            foreach (var kv in _active)
            {
                var state = kv.Value;
                if (Time.time - state.StartedAt >= state.Duration)
                {
                    ended.Add(kv.Key);
                    continue;
                }

                UpdateLocalAim(state);
                UpdateSaber(state);
                TryHostKill(state);
            }

            foreach (byte ownerId in ended)
                StopSaber(ownerId, true);
        }

        public static void ClearAll()
        {
            foreach (var kv in _active)
                DestroySaber(kv.Value);
            _active.Clear();
        }

        private static void StartSaber(byte ownerId, float startedAt, float duration, float range, float spinSpeed, bool broadcast)
        {
            StopSaber(ownerId, false);

            var state = new SaberState
            {
                OwnerId = ownerId,
                StartedAt = startedAt,
                Duration = duration,
                Range = range,
                SpinSpeed = spinSpeed,
                AimAngle = GetOwnerAimAngle(ownerId),
                LastAimSentAngle = float.NaN,
            };

            CreateSaberObject(state);
            _active[ownerId] = state;
            UpdateSaber(state);

            if (broadcast)
                ManactorAPI.SendRpcMethod(RpcBroadcastSaberKey, ownerId, duration, range, spinSpeed);
        }

        private static void StopSaber(byte ownerId, bool broadcast)
        {
            if (_active.TryGetValue(ownerId, out var state))
            {
                DestroySaber(state);
                _active.Remove(ownerId);
            }

            if (broadcast)
                ManactorAPI.SendRpcMethod(RpcStopSaberKey, ownerId);
        }

        private static void CreateSaberObject(SaberState state)
        {
            state.Object = new GameObject("VaderSaber_" + state.OwnerId);

            var visual = new GameObject("VaderSaberVisual");
            visual.transform.SetParent(state.Object.transform, false);
            state.VisualTransform = visual.transform;

            state.Renderer = visual.AddComponent<SpriteRenderer>();
            state.Renderer.sprite = VaderAssets.LoadSaber();
            state.Renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            state.Renderer.sortingOrder = 20;
            state.Object.SetActive(true);
        }

        private static void DestroySaber(SaberState state)
        {
            if (state?.Object != null)
                UnityEngine.Object.Destroy(state.Object);
        }

        private static void UpdateSaber(SaberState state)
        {
            var owner = FindPlayer(state.OwnerId);
            if (owner == null || owner.Data == null || owner.Data.IsDead)
            {
                StopSaber(state.OwnerId, true);
                return;
            }

            if (state.Object == null || state.Renderer == null)
                CreateSaberObject(state);

            if (state.VisualTransform != null && state.VisualTransform.gameObject.layer != owner.gameObject.layer)
                state.VisualTransform.gameObject.layer = owner.gameObject.layer;

            if (state.Renderer != null)
            {
                var ownerRenderer = owner.GetComponent<SpriteRenderer>();
                if (ownerRenderer == null) ownerRenderer = owner.GetComponentInChildren<SpriteRenderer>();
                if (ownerRenderer != null)
                {
                    state.Renderer.sortingLayerID = ownerRenderer.sortingLayerID;
                    state.Renderer.sharedMaterial = ownerRenderer.sharedMaterial;
                    state.Renderer.sortingOrder = ownerRenderer.sortingOrder + 1;
                }
            }

            Vector3 center = owner.transform.position + new Vector3(0f, 0.05f, -0.2f);
            float angle = state.AimAngle;
            Vector2 dir = DegreeToVector(angle);
            float length = Mathf.Max(0.25f, state.Range);

            float scale = 1f;
            if (state.Renderer != null && state.Renderer.sprite != null)
            {
                float spriteLength = Mathf.Max(0.01f, state.Renderer.sprite.bounds.size.x);
                scale = length / spriteLength;
            }

            state.Object.transform.position = center;
            state.Object.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            const float HiltGap = 0.35f;

            if (state.VisualTransform != null)
            {
                state.VisualTransform.localPosition = new Vector3(HiltGap + length * 0.5f, 0f, -0.1f);
                state.VisualTransform.localScale = new Vector3(scale, scale, 1f);
            }

            if (state.Renderer != null)
                state.Renderer.flipY = dir.x < 0f;
        }

        private static void UpdateLocalAim(SaberState state)
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || local.Data.PlayerId != state.OwnerId) return;

            float angle = GetMouseAimAngle(local);
            state.AimAngle = angle;
            if (Time.time - state.LastAimSentAt < 0.05f && !float.IsNaN(state.LastAimSentAngle) &&
                Mathf.Abs(Mathf.DeltaAngle(state.LastAimSentAngle, angle)) < 2f)
                return;

            state.LastAimSentAt = Time.time;
            state.LastAimSentAngle = angle;

            var client = AmongUsClient.Instance;
            if (client != null && client.AmHost)
                ManactorAPI.SendRpcMethod(RpcBroadcastAimSaberKey, state.OwnerId, angle);
            else
                ManactorAPI.SendRpcMethod(RpcAimSaberKey, state.OwnerId, angle);
        }

        private static void TryHostKill(SaberState state)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;

            var owner = FindPlayer(state.OwnerId);
            if (owner == null || owner.Data == null || owner.Data.IsDead) return;

            const float HiltGap = 0.35f;

            Vector2 center = owner.GetTruePosition();
            float angle = state.AimAngle;
            Vector2 dir = DegreeToVector(angle);
            Vector2 bladeStart = center + dir * HiltGap;
            Vector2 bladeEnd = center + dir * (HiltGap + state.Range);

            foreach (var target in PlayerControl.AllPlayerControls)
            {
                if (target == null || target == owner || target.Data == null || target.Data.IsDead || target.Data.Disconnected) continue;
                if (VaderPlugin.IsVader(target)) continue;

                float dist = DistancePointToSegment(target.GetTruePosition(), bladeStart, bladeEnd);
                if (dist > 0.4f) continue;

                KillManager.Kill(owner, target, new KillRequest { TeleportKiller = false });
                VaderPlugin.Log.LogInfo("Darth Vader saber killed " + target.Data.PlayerName + ".");
            }
        }

        private static PlayerControl FindPlayer(byte playerId)
        {
            foreach (var p in PlayerControl.AllPlayerControls)
                if (p != null && p.Data != null && p.Data.PlayerId == playerId)
                    return p;
            return null;
        }

        private static Vector2 DegreeToVector(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static float GetOwnerAimAngle(byte ownerId)
        {
            var local = PlayerControl.LocalPlayer;
            if (local != null && local.Data != null && local.Data.PlayerId == ownerId)
                return GetMouseAimAngle(local);

            var owner = FindPlayer(ownerId);
            if (owner == null) return 0f;
            return owner.transform.localScale.x < 0f ? 180f : 0f;
        }

        private static float GetMouseAimAngle(PlayerControl owner)
        {
            var camera = Camera.main;
            if (camera == null || owner == null) return 0f;

            if (!TryGetMouseScreenPosition(out var cursorPosition))
                return owner.transform.localScale.x < 0f ? 180f : 0f;

            Vector3 mouseWorld = camera.ScreenToWorldPoint(new Vector3(cursorPosition.x, cursorPosition.y, 0f));
            Vector2 delta = mouseWorld - owner.transform.position;
            if (delta.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        }

        private static bool TryGetMouseScreenPosition(out Vector2 screenPosition)
        {
            screenPosition = Vector2.zero;

            try
            {
                if (!GetCursorPos(out var point)) return false;

                var window = GetForegroundWindow();
                if (window != IntPtr.Zero && ScreenToClient(window, ref point) && GetClientRect(window, out var rect))
                {
                    screenPosition = new Vector2(point.X, rect.Bottom - point.Y);
                    return true;
                }

                screenPosition = new Vector2(point.X, Screen.height - point.Y);
                return true;
            }
            catch
            {
                var virtualCursor = VirtualCursor.currentPosition;
                if (virtualCursor.sqrMagnitude < 0.0001f) return false;
                screenPosition = virtualCursor;
                return true;
            }
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float denom = Vector2.Dot(ab, ab);
            if (denom <= 0.0001f) return Vector2.Distance(point, a);

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / denom);
            return Vector2.Distance(point, a + ab * t);
        }
    }
}
