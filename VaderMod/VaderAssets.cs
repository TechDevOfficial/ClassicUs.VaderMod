using System.Reflection;
using ClassicUs.ManuAPI;
using UnityEngine;

namespace ClassicUs.VaderMod
{
    internal static class VaderAssets
    {
        private static readonly Assembly _assembly = typeof(VaderAssets).Assembly;

        private static readonly LoadableSprite _button =
            new(_assembly, "saber_button.png", 100f);

        private static readonly LoadableSprite _saber =
            new(_assembly, "Saber_sprite.png", 100f);

        private static readonly LoadableSprite _forceButton =
            new(_assembly, "force_button.png", 100f);

        public static Sprite LoadButton(Sprite original) => _button.Get() ?? original;
        public static Sprite LoadSaber() => _saber.Get();
        public static Sprite LoadForceButton(Sprite original) => _forceButton.Get() ?? original;
    }
}
