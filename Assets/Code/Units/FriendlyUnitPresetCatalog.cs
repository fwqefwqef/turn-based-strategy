using System;
using UnityEngine;

namespace Windy.Srpg.Game.Units
{
    [CreateAssetMenu(fileName = "FriendlyUnitPresetCatalog", menuName = "TBS/Units/Friendly Unit Preset Catalog")]
    public sealed class FriendlyUnitPresetCatalog : ScriptableObject
    {
        public UnitPreset[] Presets = Array.Empty<UnitPreset>();
    }
}
