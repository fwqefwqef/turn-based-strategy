namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        /// <summary>
        /// Initializes scene registries (players, cells, units) for pre-battle or battle.
        /// </summary>
        public void InitializeBattle() => InitializeBattleScene();

        /// <summary>
        /// Legacy framework start path. Prefer <see cref="RequestFrameworkBattleStart"/> for runtime-led battles.
        /// </summary>
        public void StartLegacyFrameworkBattle() => StartLegacyBattle();
    }
}
