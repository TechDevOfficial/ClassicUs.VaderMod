using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using ClassicUs.Manactor;
using ClassicUs.ManuAPI;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace ClassicUs.VaderMod
{
    [BepInPlugin(Guid, "Classic Us Vader Mod", Version)]
    [BepInDependency(ManactorPlugin.Guid)]
    [BepInDependency(ManuAPIPlugin.Guid)]
    public class VaderPlugin : BasePlugin
    {
        public const string Guid = "classicus.vadermod";
        public const string Version = "1.0.0";
        public const string ModName = "ClassicUsVaderMod";

        public const string RpcSyncSettingsKey = "classicus.vadermod.SyncSettings";

        public static ManualLogSource Log;

        private static ConfigEntry<bool> _cfgEnabled;
        private static ConfigEntry<int> _cfgCount;
        private static ConfigEntry<float> _cfgRoleChance;
        private static ConfigEntry<float> _cfgCooldown;
        private static ConfigEntry<float> _cfgDuration;
        private static ConfigEntry<float> _cfgRange;
        private static ConfigEntry<float> _cfgSpinSpeed;
        private static ConfigEntry<float> _cfgForceCooldown;
        private static ConfigEntry<float> _cfgForceRange;

        public static bool ActiveEnabled = true;
        public static int ActiveCount = 1;
        public static float ActiveRoleChance = 100f;
        public static float ActiveCooldown = 20f;
        public static float ActiveDuration = 8f;
        public static float ActiveRange = 1.25f;
        public static float ActiveSpinSpeed = 360f;
        public static float ActiveForceCooldown = 25f;
        public static float ActiveForceRange = 2.5f;

        public static bool IsTypeReady;
        private static bool _classInjectorAttempted;

        public override void Load()
        {
            Log = base.Log;

            _cfgEnabled = Config.Bind("Game", "EnableDarthVader", true, "Enables Darth Vader.");
            _cfgCount = Config.Bind("Game", "DarthVaderCount", 1,
                new ConfigDescription("How many Darth Vaders to assign per match.", new AcceptableValueRange<int>(0, 3)));
            _cfgRoleChance = Config.Bind("Game", "DarthVaderRoleChance", 100f,
                new ConfigDescription("Chance that a selected candidate becomes Darth Vader.", new AcceptableValueRange<float>(0f, 100f)));
            _cfgCooldown = Config.Bind("Game", "SaberCooldown", 20f,
                new ConfigDescription("Cooldown of the saber button.", new AcceptableValueRange<float>(5f, 90f)));
            _cfgDuration = Config.Bind("Game", "SaberDuration", 8f,
                new ConfigDescription("How long the saber stays active.", new AcceptableValueRange<float>(1f, 20f)));
            _cfgRange = Config.Bind("Game", "SaberRange", 1.25f,
                new ConfigDescription("Distance from Vader to the saber hit path.", new AcceptableValueRange<float>(0.5f, 2.5f)));
            _cfgSpinSpeed = Config.Bind("Game", "SaberSpinSpeed", 360f,
                new ConfigDescription("Saber spin speed in degrees per second.", new AcceptableValueRange<float>(90f, 1080f)));
            _cfgForceCooldown = Config.Bind("Game", "ForceCooldown", 25f,
                new ConfigDescription("Cooldown of the Force button.", new AcceptableValueRange<float>(5f, 90f)));
            _cfgForceRange = Config.Bind("Game", "ForceRange", 2.5f,
                new ConfigDescription("Max distance to a player to be able to Force them.", new AcceptableValueRange<float>(1f, 5f)));

            ManactorAPI.Register(ModName, Version);
            ManactorAPI.RegisterRpcMethods(this);
            ManactorAPI.RegisterRpcMethods(typeof(VaderSaberSystem));
            ManactorAPI.RegisterRpcMethods(typeof(VaderForceSystem));

            RoleRegistry.Register(new VaderRoleDescriptor(), () => IsTypeReady, EnsureIl2CppTypeRegistered,
                () => Il2CppType.Of<DarthVaderRole>());

            SettingsMenuAPI.Register(7, builder =>
            {
                builder.AddToggle("VaderAPIToggle", "Enable Darth Vader",
                    () => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost ? _cfgEnabled.Value : ActiveEnabled,
                    val => { _cfgEnabled.Value = val; Save(); });
                builder.AddNumeric("VaderAPICount", "Darth Vader Count", 1f, 0f, 3f, "0",
                    () => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost ? _cfgCount.Value : ActiveCount,
                    val => { _cfgCount.Value = (int)val; Save(); });
                builder.AddNumeric("VaderAPIChance", "Darth Vader Chance", 5f, 0f, 100f, "0\\%",
                    () => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost ? _cfgRoleChance.Value : ActiveRoleChance,
                    val => { _cfgRoleChance.Value = val; Save(); });
                builder.AddNumeric("VaderAPICooldown", "Saber Cooldown", 5f, 5f, 90f, "0s",
                    () => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost ? _cfgCooldown.Value : ActiveCooldown,
                    val => { _cfgCooldown.Value = val; Save(); });
                builder.AddNumeric("VaderAPIDuration", "Saber Duration", 1f, 1f, 20f, "0s",
                    () => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost ? _cfgDuration.Value : ActiveDuration,
                    val => { _cfgDuration.Value = val; Save(); });
                builder.AddNumeric("VaderAPIRange", "Saber Range", 0.1f, 0.5f, 2.5f, "0.0",
                    () => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost ? _cfgRange.Value : ActiveRange,
                    val => { _cfgRange.Value = val; Save(); });
                builder.AddNumeric("VaderAPISpinSpeed", "Saber Spin Speed", 45f, 90f, 1080f, "0",
                    () => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost ? _cfgSpinSpeed.Value : ActiveSpinSpeed,
                    val => { _cfgSpinSpeed.Value = val; Save(); });
                builder.AddNumeric("VaderAPIForceCooldown", "Force Cooldown", 5f, 5f, 90f, "0s",
                    () => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost ? _cfgForceCooldown.Value : ActiveForceCooldown,
                    val => { _cfgForceCooldown.Value = val; Save(); });
                builder.AddNumeric("VaderAPIForceRange", "Force Range", 0.5f, 1f, 5f, "0.0",
                    () => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost ? _cfgForceRange.Value : ActiveForceRange,
                    val => { _cfgForceRange.Value = val; Save(); });
                builder.ExpandScroller(9f);
            });

            ModBadgeAPI.RegisterLoadedModBadge("VaderMod", Version, new Color(0.9f, 0.05f, 0.05f, 1f));
            ModBadgeAPI.RegisterPrelobbyTag("Vader Mod", "#E60D0D");

            new Harmony(Guid).PatchAll();
            Log.LogInfo("Classic Us Vader Mod loaded.");
        }

        private static void Save()
        {
            _cfgEnabled.ConfigFile.Save();
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                HostBroadcastSettings();
        }

        public static void EnsureIl2CppTypeRegistered()
        {
            if (_classInjectorAttempted) return;
            _classInjectorAttempted = true;

            ManactorAPI.RegisterIl2CppType(() =>
            {
                try
                {
                    ClassInjector.RegisterTypeInIl2Cpp<DarthVaderRole>();
                    IsTypeReady = true;
                    Log.LogInfo("DarthVaderRole type registered in IL2CPP.");
                }
                catch (Exception e)
                {
                    Log.LogError("DarthVaderRole registration failed: " + e);
                }
            });
        }

        public static void HostBroadcastSettings()
        {
            ActiveEnabled = _cfgEnabled.Value;
            ActiveCount = _cfgCount.Value;
            ActiveRoleChance = _cfgRoleChance.Value;
            ActiveCooldown = _cfgCooldown.Value;
            ActiveDuration = _cfgDuration.Value;
            ActiveRange = _cfgRange.Value;
            ActiveSpinSpeed = _cfgSpinSpeed.Value;
            ActiveForceCooldown = _cfgForceCooldown.Value;
            ActiveForceRange = _cfgForceRange.Value;

            ManactorAPI.SendRpcMethod(RpcSyncSettingsKey, ActiveEnabled, (byte)ActiveCount, ActiveRoleChance,
                ActiveCooldown, ActiveDuration, ActiveRange, ActiveSpinSpeed, ActiveForceCooldown, ActiveForceRange);
            Log.LogInfo($"Vader settings sent: enabled={ActiveEnabled} count={ActiveCount} chance={ActiveRoleChance} cooldown={ActiveCooldown} duration={ActiveDuration} range={ActiveRange} spin={ActiveSpinSpeed} forceCooldown={ActiveForceCooldown} forceRange={ActiveForceRange}");
        }

        [ManactorRpc(RpcSyncSettingsKey)]
        private static void OnSyncSettingsRpc(byte senderId, bool enabled, byte count, float chance, float cooldown, float duration, float range, float spinSpeed, float forceCooldown, float forceRange)
        {
            ActiveEnabled = enabled;
            ActiveCount = count;
            ActiveRoleChance = chance;
            ActiveCooldown = cooldown;
            ActiveDuration = duration;
            ActiveRange = range;
            ActiveSpinSpeed = spinSpeed;
            ActiveForceCooldown = forceCooldown;
            ActiveForceRange = forceRange;
            Log.LogInfo($"Vader settings received: enabled={ActiveEnabled} count={ActiveCount} chance={ActiveRoleChance} cooldown={ActiveCooldown} duration={ActiveDuration} range={ActiveRange} spin={ActiveSpinSpeed} forceCooldown={ActiveForceCooldown} forceRange={ActiveForceRange}");
        }

        public static bool IsVader(PlayerControl p)
        {
            if (p == null || p.Data == null || p.Data.myRole == null) return false;
            try { return p.Data.myRole.GetIl2CppType().Name == "DarthVaderRole"; }
            catch { return false; }
        }
    }
}
