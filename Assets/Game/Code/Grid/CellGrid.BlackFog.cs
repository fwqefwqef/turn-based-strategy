using Windy.Srpg.Game.Chapters;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CellGrid
    {
        private BlackFogSystem blackFogSystem;

        private BlackFogSystem BlackFog => blackFogSystem ??= new BlackFogSystem(this);

        internal void InitializeBlackFogForBattle()
        {
            BlackFog.InitializeForBattle(ChapterData.FindForGrid(this));
            if (CurrentPlayerNumber == 0)
            {
                BlackFog.PrepareBattleStart(RoundCount);
            }
        }

        internal void PrepareBlackFogBeforeIncomingTurnDotPhase()
        {
            BlackFog.BeforeIncomingTurnDotPhase(CurrentPlayerNumber);
        }

        internal void CompleteBlackFogAfterIncomingTurnDotPhase()
        {
            BlackFog.AfterIncomingTurnDotPhase(CurrentPlayerNumber, RoundCount);
        }

        internal void RefreshBlackFogDebuffsForOccupancyChange()
        {
            blackFogSystem?.RefreshFogDebuffs();
        }

        internal void ClearBlackFogBattleState()
        {
            blackFogSystem?.ClearBattleState();
        }

        public bool TryGetBlackFogDepth(Unit unit, out int depth)
        {
            depth = 0;
            return blackFogSystem != null && blackFogSystem.TryGetDepth(unit, out depth);
        }
    }
}
