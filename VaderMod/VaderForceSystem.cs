using System;
using System.Collections.Generic;
using ClassicUs.Manactor;
using UnityEngine;

namespace ClassicUs.VaderMod
{
    internal static class VaderForceSystem
    {
        private const string RpcRequestForceKey = "classicus.vadermod.RequestForce";
        private const string RpcBroadcastForceKey = "classicus.vadermod.BroadcastForce";
        private const string RpcRequestMoveKey = "classicus.vadermod.RequestForceMove";
        private const string RpcBroadcastPositionKey = "classicus.vadermod.BroadcastForcePosition";
        private const string RpcStopForceKey = "classicus.vadermod.StopForce";

        private const float MoveSpeed = 1.6f;
        private const float SpinSpeed = 140f;

        private sealed class ForceState
        {
            public byte OwnerId;
            public byte TargetId;
            public float StartedAt;
            public float Duration;
            public Vector2 CurrentPos;
            public Vector2 DesiredPos;
            public float LastMoveSentAt;
        }

        private static readonly Dictionary<byte, ForceState> _activeByTarget = new();

        public static bool IsBeingForced(byte playerId) => _activeByTarget.ContainsKey(playerId);
        public static bool IsForcing(byte ownerId)
        {
            foreach (var kv in _activeByTarget)
                if (kv.Value.OwnerId == ownerId) return true;
            return false;
        }

        public static PlayerControl FindNearbyTarget(PlayerControl owner, float range)
        {
            if (owner == null || owner.Data == null) return null;

            PlayerControl best = null;
            float bestDist = float.MaxValue;

            foreach (var p in PlayerControl.AllPlayerControls)
            {
                if (p == null || p == owner || p.Data == null || p.Data.IsDead || p.Data.Disconnected) continue;
                if (VaderPlugin.IsVader(p)) continue;
                if (IsBeingForced(p.Data.PlayerId)) continue;

                float dist = Vector2.Distance(owner.GetTruePosition(), p.GetTruePosition());
                if (dist > range || dist >= bestDist) continue;

                best = p;
                bestDist = dist;
            }

            return best;
        }

        public static void RequestStart(byte ownerId, byte targetId, float duration)
        {
            var client = AmongUsClient.Instance;
            if (client != null && client.AmHost)
                StartForce(ownerId, targetId, duration, true);
            else
                ManactorAPI.SendRpcMethod(RpcRequestForceKey, ownerId, targetId, duration);
        }

        [ManactorRpc(RpcRequestForceKey)]
        private static void OnRequestForceRpc(byte senderId, byte ownerId, byte targetId, float duration)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;

            var owner = FindPlayer(ownerId);
            var target = FindPlayer(targetId);
            if (owner == null || target == null || target.Data == null || target.Data.IsDead) return;
            if (!VaderPlugin.IsVader(owner)) return;
            if (IsBeingForced(targetId)) return;

            StartForce(ownerId, targetId, duration, true);
        }

        [ManactorRpc(RpcBroadcastForceKey)]
        private static void OnBroadcastForceRpc(byte senderId, byte ownerId, byte targetId, float duration)
        {
            var client = AmongUsClient.Instance;
            if (client != null && client.AmHost) return;

            StartForce(ownerId, targetId, duration, false);
        }

        [ManactorRpc(RpcStopForceKey)]
        private static void OnStopForceRpc(byte senderId, byte targetId) => StopForce(targetId, false);

        [ManactorRpc(RpcRequestMoveKey)]
        private static void OnRequestMoveRpc(byte senderId, byte targetId, float x, float y)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;
            if (!_activeByTarget.TryGetValue(targetId, out var state)) return;

            state.DesiredPos = new Vector2(x, y);
        }

        [ManactorRpc(RpcBroadcastPositionKey)]
        private static void OnBroadcastPositionRpc(byte senderId, byte targetId, float x, float y)
        {
            var target = FindPlayer(targetId);
            if (target == null) return;

            var pos = new Vector2(x, y);
            ApplyPosition(target, pos);

            if (_activeByTarget.TryGetValue(targetId, out var state))
                state.CurrentPos = pos;
        }

        private static void ApplyPosition(PlayerControl target, Vector2 pos)
        {
            target.transform.position = new Vector3(pos.x, pos.y, target.transform.position.z);
        }

        private static void StartForce(byte ownerId, byte targetId, float duration, bool broadcast)
        {
            StopForce(targetId, false);

            var target = FindPlayer(targetId);
            var startPos = target != null ? target.GetTruePosition() : Vector2.zero;

            var state = new ForceState
            {
                OwnerId = ownerId,
                TargetId = targetId,
                StartedAt = Time.time,
                Duration = duration,
                CurrentPos = startPos,
                DesiredPos = startPos,
            };

            _activeByTarget[targetId] = state;

            KillAnimation.SetMovement(target, false);

            TriggerShakeIfLocal(ownerId, targetId, duration);

            if (broadcast)
                ManactorAPI.SendRpcMethod(RpcBroadcastForceKey, ownerId, targetId, duration);
        }

        private static void StopForce(byte targetId, bool broadcast)
        {
            if (_activeByTarget.TryGetValue(targetId, out var state))
            {
                var target = FindPlayer(targetId);
                if (target != null)
                {
                    KillAnimation.SetMovement(target, true);
                    ApplyPosition(target, state.CurrentPos);
                    if (target.NetTransform != null)
                    {
                        target.NetTransform.SnapTo(state.CurrentPos);
                        target.NetTransform.RpcSnapTo(state.CurrentPos);
                    }

                    if (target.transform.rotation != Quaternion.identity)
                        target.transform.rotation = Quaternion.identity;
                    if (target.HatRenderer != null)
                        target.HatRenderer.transform.rotation = Quaternion.identity;
                }

                _activeByTarget.Remove(targetId);
            }

            if (broadcast)
            {
                var client = AmongUsClient.Instance;
                if (client != null && client.AmHost)
                    ManactorAPI.SendRpcMethod(RpcStopForceKey, targetId);
            }
        }

        public static void Tick()
        {
            if (_activeByTarget.Count == 0) return;

            var client = AmongUsClient.Instance;
            bool isHost = client != null && client.AmHost;

            List<byte> ended = null;
            foreach (var kv in _activeByTarget)
            {
                var state = kv.Value;

                if (Time.time - state.StartedAt >= state.Duration)
                {
                    (ended ??= new List<byte>()).Add(kv.Key);
                    continue;
                }

                var target = FindPlayer(state.TargetId);
                if (target == null) continue;

                SendLocalDesiredPosition(state);
                RotateFloating(target);

                if (!isHost) continue;

                state.CurrentPos = Vector2.MoveTowards(state.CurrentPos, state.DesiredPos, MoveSpeed * Time.fixedDeltaTime);
                ApplyPosition(target, state.CurrentPos);
                ManactorAPI.SendRpcMethod(RpcBroadcastPositionKey, state.TargetId, state.CurrentPos.x, state.CurrentPos.y);
            }

            if (ended == null) return;
            foreach (var targetId in ended)
                StopForce(targetId, isHost);
        }

        private static void SendLocalDesiredPosition(ForceState state)
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || local.Data.PlayerId != state.OwnerId) return;

            if (Time.time - state.LastMoveSentAt < 0.05f) return;
            state.LastMoveSentAt = Time.time;

            var camera = Camera.main;
            if (camera == null) return;
            if (!TryGetMouseScreenPosition(out var screenPos)) return;

            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));

            var clientInstance = AmongUsClient.Instance;
            if (clientInstance != null && clientInstance.AmHost)
                state.DesiredPos = new Vector2(world.x, world.y);
            else
                ManactorAPI.SendRpcMethod(RpcRequestMoveKey, state.TargetId, world.x, world.y);
        }

        private static void RotateFloating(PlayerControl target)
        {
            float angle = (Time.time * SpinSpeed) % 360f;
            target.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            if (target.HatRenderer != null)
                target.HatRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static void TriggerShakeIfLocal(byte ownerId, byte targetId, float duration)
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null) return;
            if (local.Data.PlayerId != ownerId && local.Data.PlayerId != targetId) return;

            var camera = Camera.main;
            var followerCam = camera != null ? camera.GetComponent<FollowerCamera>() : null;
            followerCam?.ShakeScreen(duration, 0.6f);
        }

        public static void ClearAll()
        {
            var ids = new List<byte>(_activeByTarget.Keys);
            foreach (var id in ids)
                StopForce(id, false);
        }

        private static PlayerControl FindPlayer(byte playerId)
        {
            foreach (var p in PlayerControl.AllPlayerControls)
                if (p != null && p.Data != null && p.Data.PlayerId == playerId)
                    return p;
            return null;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point point);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref Point point);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out NativeRect rect);

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
    }
}
