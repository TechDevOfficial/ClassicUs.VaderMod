using System;
using ClassicUs.ManuAPI;
using HarmonyLib;

namespace ClassicUs.VaderMod
{
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.FixedUpdate))]
    internal static class HudManager_FixedUpdate_Patch
    {
        private static void Prefix(HudManager __instance)
        {
            try
            {
                ForceAbilityHolder.Tick(__instance);
                SaberAbilityHolder.Tick(__instance);
                VaderSaberSystem.Tick();
                VaderForceSystem.Tick();
            }
            catch (Exception e) { VaderPlugin.Log.LogError("Vader HUD tick: " + e); }
        }
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
    internal static class HudManager_Start_Patch
    {
        private static void Prefix()
        {
            VaderSaberSystem.ClearAll();
            VaderForceSystem.ClearAll();
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    internal static class AmongUsClient_OnGameEnd_Patch
    {
        private static void Prefix() => VaderCleanup.ResetMatchState("OnGameEnd");
    }

    [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.NextGame))]
    internal static class EndGameManager_NextGame_Patch
    {
        private static void Prefix() => VaderCleanup.ResetMatchState("NextGame");
    }

    [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.Exit))]
    internal static class EndGameManager_Exit_Patch
    {
        private static void Prefix() => VaderCleanup.ResetMatchState("EndGameExit");
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
    internal static class AmongUsClient_ExitGame_Patch
    {
        private static void Prefix() => VaderCleanup.ResetMatchState("ExitGame");
    }

    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.AssignRolesForTeam))]
    internal static class RoleManager_AssignRolesForTeam_Patch
    {
        private static void Prefix()
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost) return;

            try { VaderPlugin.HostBroadcastSettings(); }
            catch (Exception e) { VaderPlugin.Log.LogError("HostBroadcastSettings (AssignRolesForTeam): " + e); }
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerJoined))]
    internal static class AmongUsClient_OnPlayerJoined_Patch
    {
        private static void Postfix(AmongUsClient __instance)
        {
            if (__instance == null || !__instance.AmHost) return;

            try { VaderPlugin.HostBroadcastSettings(); }
            catch (Exception e) { VaderPlugin.Log.LogError("HostBroadcastSettings (OnPlayerJoined): " + e); }
        }
    }

    [HarmonyPatch(typeof(PingTracker), nameof(PingTracker.Update))]
    internal static class PingTracker_Update_Patch
    {
        private static void Postfix(PingTracker __instance)
        {
            try
            {
                if (__instance != null && __instance.text != null)
                {
                    var t = __instance.text;
                    if (!t.Text.EndsWith("\nmod by Manu"))
                        t.Text += "\nmod by Manu";
                }
            }
            catch (Exception e)
            {
                VaderPlugin.Log.LogError("PingTracker patch: " + e);
            }
        }
    }

    internal static class VaderCleanup
    {
        public static void ResetMatchState(string reason)
        {
            try
            {
                VaderSaberSystem.ClearAll();
                VaderForceSystem.ClearAll();
                RoleRegistry.ClearRuntimeAssignments();
                VaderPlugin.Log.LogInfo("Vader runtime state cleared: " + reason + ".");
            }
            catch (Exception e)
            {
                VaderPlugin.Log.LogError("Vader cleanup (" + reason + "): " + e);
            }
        }
    }
}
