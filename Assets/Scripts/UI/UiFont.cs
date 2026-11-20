using UnityEngine;

namespace SlotGame.UI
{
    /// <summary>Shared access to Unity's built-in font so no font asset (or TextMeshPro import) is needed.</summary>
    public static class UiFont
    {
        private static Font cached;

        public static Font Default
        {
            get
            {
                if (cached == null) cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return cached;
            }
        }
    }
}
