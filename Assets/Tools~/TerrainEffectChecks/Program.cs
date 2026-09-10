using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Units;

int checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks++;
}

TerrainEffectRegistry.Register(new TerrainEffectData
{
    Id = "throne",
    PrimaryStatModifiers = new PrimaryStatModifiers { Defense = 5 },
    SecondaryStatModifiers = new SecondaryStatModifiers { Evade = 20 }
});
TerrainEffectRegistry.Register(new TerrainEffectData
{
    Id = "burning_terrain",
    OccupantBuffId = "burn",
    RemoveOccupantBuffOnExit = false,
    OverlayStyle = TerrainEffectOverlayStyle.Burning
});
TerrainEffectRegistry.Register(new TerrainEffectData
{
    Id = "black_fog",
    OccupantBuffId = "black_fog",
    RemoveOccupantBuffOnExit = true,
    OverlayStyle = TerrainEffectOverlayStyle.BlackFog,
    Targeting = TerrainEffectTargeting.PlayerSide,
    TargetPlayerNumber = 0
});

Cell throneCell = new() { TilePreset = new CellTilePreset { StartingTerrainEffectIds = new List<string> { "throne" } } };
Cell plainCell = new() { TilePreset = new CellTilePreset() };
Unit player = new() { PlayerNumber = 0, Cell = throneCell };
throneCell.CurrentUnits.Add(player);
CellGrid grid = new();
grid.Cells.AddRange(new[] { throneCell, plainCell });
grid.Units.Add(player);
TerrainEffectSystem system = new(grid);
system.InitializeForBattle();

Check(player.TerrainPrimary.Defense == 5 && player.TerrainSecondary.Evade == 20,
    "Starting Throne terrain applies Defense and Evade bonuses before battle start");
system.SetEffect(throneCell, "throne", 2, 0, -1);
Check(throneCell.GetTerrainEffect("throne")?.IsPermanent == true,
    "Refreshing permanent starting terrain does not make it temporary");

throneCell.CurrentUnits.Remove(player);
plainCell.CurrentUnits.Add(player);
player.Cell = plainCell;
system.RefreshAllUnitEffects();
Check(player.TerrainPrimary.Defense == 0 && player.TerrainSecondary.Evade == 0,
    "Leaving static terrain removes its bonuses immediately");

Check(system.SetEffect(plainCell, "burning_terrain", 2, 0, 0), "Burning Terrain can be created dynamically");
Check(player.BuffList.GetBuff("burn") != null, "Standing on Burning Terrain applies Burn");
system.AdvanceRound();
Check(plainCell.GetTerrainEffect("burning_terrain")?.RemainingRounds == 1, "Burning Terrain loses one round per round transition");
system.SetEffect(plainCell, "burning_terrain", 2, 0, 0);
Check(plainCell.GetTerrainEffect("burning_terrain")?.RemainingRounds == 2, "Reapplication refreshes Burning Terrain duration");
system.AdvanceRound();
system.AdvanceRound();
Check(plainCell.GetTerrainEffect("burning_terrain") == null, "Burning Terrain expires after two rounds");
Check(player.BuffList.GetBuff("burn") != null, "Burn persists after Burning Terrain expires");

Unit enemy = new() { PlayerNumber = 1, Cell = plainCell };
plainCell.CurrentUnits.Add(enemy);
grid.Units.Add(enemy);
system.SetEffect(plainCell, "black_fog", 0, 2, -1);
Check(player.BuffList.GetBuff("black_fog") != null && enemy.BuffList.GetBuff("black_fog") == null,
    "Black Fog exposure targets only the configured player side");
Check(system.TryGetIntensity(player, "black_fog", out int depth) && depth == 2,
    "Black Fog depth is stored as terrain intensity");

plainCell.CurrentUnits.Remove(player);
player.Cell = throneCell;
throneCell.CurrentUnits.Add(player);
system.RefreshAllUnitEffects();
Check(player.BuffList.GetBuff("black_fog") == null, "Leaving Black Fog removes its exposure status");

Console.WriteLine($"Terrain effect checks passed: {checks}");
