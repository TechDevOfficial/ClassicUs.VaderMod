using ClassicUs.ManuAPI;
using UnityEngine;

namespace ClassicUs.VaderMod
{
    internal class ForceAbility : CustomAbility
    {
        protected override string Name => "ForceButton";
        protected override float Cooldown => VaderPlugin.ActiveForceCooldown;
        protected override AspectPosition.EdgeAlignments Alignment => AspectPosition.EdgeAlignments.LeftBottom;
        protected override Vector3 DistanceFromEdge => AbilityButtonGrid.SlotA;

        protected override Sprite CreateIcon(Sprite original) => VaderAssets.LoadForceButton(original);

        protected override bool IsVisible()
        {
            var local = PlayerControl.LocalPlayer;
            return VaderPlugin.IsVader(local) && local.Data != null && !local.Data.IsDead;
        }

        protected override bool CanActivate()
        {
            var local = PlayerControl.LocalPlayer;
            if (!VaderPlugin.IsVader(local) || local.Data == null || local.Data.IsDead) return false;
            if (VaderForceSystem.IsForcing(local.Data.PlayerId)) return false;

            return VaderForceSystem.FindNearbyTarget(local, VaderPlugin.ActiveForceRange) != null;
        }

        protected override void OnActivate()
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || !VaderPlugin.IsVader(local) || local.Data.IsDead) return;

            var target = VaderForceSystem.FindNearbyTarget(local, VaderPlugin.ActiveForceRange);
            if (target == null || target.Data == null) return;

            VaderForceSystem.RequestStart(local.Data.PlayerId, target.Data.PlayerId, 5f);
            StartEffect(5f);
        }
    }

    internal static class ForceAbilityHolder
    {
        private static readonly ForceAbility _ability = new();

        public static void Tick(HudManager hud) => _ability.Tick(hud);
    }
}
