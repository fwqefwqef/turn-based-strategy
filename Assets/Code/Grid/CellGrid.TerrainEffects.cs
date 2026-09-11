using System.Collections.Generic;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CellGrid
    {
        private TerrainEffectSystem terrainEffectSystem;
        private TerrainEffectSystem TerrainEffects => terrainEffectSystem ??= new TerrainEffectSystem(this);

        internal void InitializeTerrainEffectsForBattle() => TerrainEffects.InitializeForBattle();
        internal void AdvanceTerrainEffectRound() => terrainEffectSystem?.AdvanceRound();
        internal void RefreshTerrainEffectsForOccupancyChange() => terrainEffectSystem?.RefreshAllUnitEffects();
        internal void ClearTerrainEffectBattleState() => terrainEffectSystem?.ClearBattleState();

        public bool ApplyTerrainEffect(Cell cell, string effectId, int durationRounds = 0, int intensity = 0, int sourcePlayerNumber = -1)
        {
            return TerrainEffects.SetEffect(cell, effectId, durationRounds, intensity, sourcePlayerNumber);
        }

        public bool RemoveTerrainEffect(Cell cell, string effectId)
        {
            return terrainEffectSystem != null && terrainEffectSystem.RemoveEffect(cell, effectId);
        }

        public IReadOnlyList<TerrainEffectInstance> GetTerrainEffects(Cell cell)
        {
            Cell canonicalCell = ResolveCanonicalCell(cell);
            return canonicalCell?.TerrainEffects ?? System.Array.Empty<TerrainEffectInstance>();
        }

        public bool TryGetTerrainEffectIntensity(Unit unit, string effectId, out int intensity)
        {
            intensity = 0;
            return terrainEffectSystem != null && terrainEffectSystem.TryGetIntensity(unit, effectId, out intensity);
        }

        internal bool SetTerrainEffectWithoutRefresh(Cell cell, string effectId, int durationRounds, int intensity, int sourcePlayerNumber = -1)
        {
            return TerrainEffects.SetEffect(cell, effectId, durationRounds, intensity, sourcePlayerNumber, refreshUnits: false);
        }

        internal bool RemoveTerrainEffectWithoutRefresh(Cell cell, string effectId)
        {
            return terrainEffectSystem != null && terrainEffectSystem.RemoveEffect(cell, effectId, refreshUnits: false);
        }
    }
}
