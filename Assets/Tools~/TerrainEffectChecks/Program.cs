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
    OccupantBuffId = "throne",
    RemoveOccupantBuffOnExit = true
});
TerrainEffectRegistry.Register(new TerrainEffectData
{
    Id = "burning_terrain",
    OccupantBuffId = "burning_terrain",
    RemoveOccupantBuffOnExit = true,
    AppliedBuffId = "burn",
    RemoveAppliedBuffOnExit = false,
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

Check(player.BuffList.GetBuff("throne") != null,
    "Starting Throne terrain applies its authoritative stat buff before battle start");
Check(player.TerrainPrimary.Defense == 0 && player.TerrainSecondary.Evade == 0,
    "Terrain does not apply legacy direct stat modifiers alongside its buff");
system.SetEffect(throneCell, "throne", 2, 0, -1);
Check(throneCell.GetTerrainEffect("throne")?.IsPermanent == true,
    "Refreshing permanent starting terrain does not make it temporary");

throneCell.CurrentUnits.Remove(player);
plainCell.CurrentUnits.Add(player);
player.Cell = plainCell;
system.RefreshAllUnitEffects();
Check(player.BuffList.GetBuff("throne") == null,
    "Leaving static terrain removes its stat buff immediately");

Check(system.SetEffect(plainCell, "burning_terrain", 2, 0, 0), "Burning Terrain can be created dynamically");
Check(player.BuffList.GetBuff("burning_terrain") != null, "Standing on Burning Terrain adds its terrain occupancy buff");
Check(player.BuffList.GetBuff("burn") != null, "Standing on Burning Terrain applies Burn");
plainCell.CurrentUnits.Remove(player);
throneCell.CurrentUnits.Add(player);
player.Cell = throneCell;
system.RefreshAllUnitEffects();
Check(player.BuffList.GetBuff("burning_terrain") == null, "Leaving Burning Terrain removes only its occupancy buff");
Check(player.BuffList.GetBuff("burn") != null, "Leaving Burning Terrain does not remove its separate Burn debuff");
throneCell.CurrentUnits.Remove(player);
plainCell.CurrentUnits.Add(player);
player.Cell = plainCell;
system.RefreshAllUnitEffects();
system.AdvanceRound();
Check(plainCell.GetTerrainEffect("burning_terrain")?.RemainingRounds == 1, "Burning Terrain loses one round per round transition");
system.SetEffect(plainCell, "burning_terrain", 2, 0, 0);
Check(plainCell.GetTerrainEffect("burning_terrain")?.RemainingRounds == 2, "Reapplication refreshes Burning Terrain duration");
system.AdvanceRound();
system.AdvanceRound();
Check(plainCell.GetTerrainEffect("burning_terrain") == null, "Burning Terrain expires after two rounds");
Check(player.BuffList.GetBuff("burning_terrain") == null, "Burning Terrain occupancy buff is removed when the terrain expires");
Check(player.BuffList.GetBuff("burn") != null, "The separate Burn debuff persists for its own duration after terrain exit");

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

Cell giantAnchor = new();
Cell giantEdge = new();
Unit giant = new() { PlayerNumber = 0, Cell = giantAnchor };
giant.FootprintCells.AddRange(new[] { giantAnchor, giantEdge });
giantAnchor.CurrentUnits.Add(giant);
giantEdge.CurrentUnits.Add(giant);
grid.Cells.AddRange(new[] { giantAnchor, giantEdge });
grid.Units.Add(giant);
system.SetEffect(giantAnchor, "throne", 0, 0, -1);
system.SetEffect(giantEdge, "throne", 0, 0, -1);
Check(giant.BuffList.GetBuff("throne") != null,
    "A repeated terrain effect ID maintains one buff across a multi-tile footprint");
system.SetEffect(giantAnchor, "black_fog", 0, 1, -1);
system.SetEffect(giantEdge, "black_fog", 0, 3, -1);
Check(system.TryGetIntensity(giant, "black_fog", out int deepestDepth) && deepestDepth == 3,
    "Multi-tile Black Fog exposure uses the deepest touched tile");

Console.WriteLine($"Terrain effect checks passed: {checks}");
