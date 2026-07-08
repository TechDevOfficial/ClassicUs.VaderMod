using ClassicUs.ManuAPI;
using UnityEngine;

namespace ClassicUs.VaderMod
{
    internal class SaberAbility : CustomAbility
    {
        protected override string Name => "SaberButton";
        protected override float Cooldown => VaderPlugin.ActiveCooldown;
        protected override AspectPosition.EdgeAlignments Alignment => AspectPosition.EdgeAlignments.LeftBottom;
        protected override Vector3 DistanceFromEdge => AbilityButtonGrid.SlotB;

        protected override Sprite CreateIcon(Sprite original) => VaderAssets.LoadButton(original);

        protected override bool IsVisible()
        {
            var local = PlayerControl.LocalPlayer;
            return VaderPlugin.IsVader(local) && local.Data != null && !local.Data.IsDead;
        }

        protected override bool CanActivate()
        {
            var local = PlayerControl.LocalPlayer;
            return VaderPlugin.IsVader(local) && local.Data != null && !local.Data.IsDead &&
                   !VaderSaberSystem.IsActive(local.Data.PlayerId);
        }

        protected override void OnActivate()
        {
            var local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || !VaderPlugin.IsVader(local) || local.Data.IsDead) return;

            VaderSaberSystem.RequestStart(local.Data.PlayerId);
            StartEffect(VaderPlugin.ActiveDuration);
        }
    }

    internal static class SaberAbilityHolder
    {
        private static readonly SaberAbility _ability = new();

        public static void Tick(HudManager hud) => _ability.Tick(hud);
    }
}
