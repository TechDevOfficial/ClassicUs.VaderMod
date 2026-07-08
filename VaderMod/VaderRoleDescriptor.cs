using ClassicUs.ManuAPI;
using UnityEngine;

namespace ClassicUs.VaderMod
{
    internal class VaderRoleDescriptor : CustomImpostorRole
    {
        public override string DisplayName => "Darth Vader";
        public override string RoleTypeName => "DarthVaderRole";
        public override int Count => VaderPlugin.ActiveEnabled ? VaderPlugin.ActiveCount : 0;
        public override float RoleChancePercent => VaderPlugin.ActiveRoleChance;
        public override Color TeamColor => new(0.9f, 0.05f, 0.05f, 1f);
        public override string Description => "You are Darth Vader. Spin your saber and cut down anyone who touches it.";
        public override string DescriptionShort => "Use the saber around you.";
        public override string EjectionText(string playerName) => $"{playerName} was Darth Vader.";
    }
}
