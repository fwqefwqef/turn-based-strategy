using System;
using Windy.Srpg.Game.Inventory;

namespace Windy.Srpg.Game.Grid
{
    public enum TerrainEffectOverlayStyle
    {
        None,
        BlackFog,
        Burning
    }

    public enum TerrainEffectTargeting
    {
        AllUnits,
        PlayerSide
    }

    [Serializable]
    public sealed class TerrainEffectData
    {
        public string Id;
        public string Name = "Terrain Effect";
        public string Description = string.Empty;
        public PrimaryStatModifiers PrimaryStatModifiers;
        public SecondaryStatModifiers SecondaryStatModifiers;
        public string OccupantBuffId;
        public bool RemoveOccupantBuffOnExit;
        public TerrainEffectOverlayStyle OverlayStyle;
        public TerrainEffectTargeting Targeting;
        public int TargetPlayerNumber;
    }

    public sealed class TerrainEffectInstance
    {
        public TerrainEffectInstance(TerrainEffectData data, int remainingRounds, int intensity, int sourcePlayerNumber)
        {
            Data = data;
            RemainingRounds = Math.Max(0, remainingRounds);
            Intensity = Math.Max(0, intensity);
            SourcePlayerNumber = sourcePlayerNumber;
        }

        public TerrainEffectData Data { get; }
        public int RemainingRounds { get; internal set; }
        public int Intensity { get; internal set; }
        public int SourcePlayerNumber { get; internal set; }
        public bool IsPermanent => RemainingRounds == 0;
    }
}
