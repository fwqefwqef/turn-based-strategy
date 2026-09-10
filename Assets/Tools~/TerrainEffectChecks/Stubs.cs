namespace Windy.Srpg.Game.Inventory
{
    public struct PrimaryStatModifiers
    {
        public int Defense;
        public int Magic;
        public static PrimaryStatModifiers operator +(PrimaryStatModifiers a, PrimaryStatModifiers b) =>
            new() { Defense = a.Defense + b.Defense, Magic = a.Magic + b.Magic };
    }

    public struct SecondaryStatModifiers
    {
        public int Evade;
        public static SecondaryStatModifiers operator +(SecondaryStatModifiers a, SecondaryStatModifiers b) =>
            new() { Evade = a.Evade + b.Evade };
    }
}

namespace Windy.Srpg.Game.Buffs
{
    public sealed class Buff
    {
        public string Id;
    }

    public sealed class UnitBuffList
    {
        private readonly Dictionary<string, Buff> entries = new(StringComparer.OrdinalIgnoreCase);
        public Buff GetBuff(string id) => entries.TryGetValue(id, out Buff buff) ? buff : null;
        public Buff Add(string id) => entries[id] = new Buff { Id = id };
        public bool Remove(Buff buff) => buff != null && entries.Remove(buff.Id);
    }
}

namespace Windy.Srpg.Game.Units
{
    using Windy.Srpg.Game.Buffs;
    using Windy.Srpg.Game.Grid;
    using Windy.Srpg.Game.Inventory;

    public sealed class Unit
    {
        public int PlayerNumber;
        public bool ExcludedFromBattle;
        public bool IsAliveForBattle = true;
        public Cell Cell;
        public List<Cell> FootprintCells = new();
        public int FootprintTileCount => FootprintCells.Count > 0 ? FootprintCells.Count : 1;
        public UnitBuffList BuffList = new();
        public PrimaryStatModifiers TerrainPrimary;
        public SecondaryStatModifiers TerrainSecondary;

        public Buff AddBuffById(string id) => BuffList.Add(id);
        public IReadOnlyList<Cell> GetFootprintCells(Cell anchor, CellGrid grid) =>
            FootprintCells.Count > 0 ? FootprintCells : anchor != null ? new[] { anchor } : Array.Empty<Cell>();
        public bool RemoveBuff(Buff buff) => BuffList.Remove(buff);
        public void SetTerrainStatModifiers(PrimaryStatModifiers primary, SecondaryStatModifiers secondary)
        {
            TerrainPrimary = primary;
            TerrainSecondary = secondary;
        }
    }
}

namespace Windy.Srpg.Game.Grid
{
    using Windy.Srpg.Game.Units;

    public sealed class CellTilePreset
    {
        public List<string> StartingTerrainEffectIds = new();
    }

    public sealed class Cell
    {
        private readonly List<TerrainEffectInstance> effects = new();
        public CellTilePreset TilePreset;
        public List<Unit> CurrentUnits = new();
        public IReadOnlyList<TerrainEffectInstance> TerrainEffects => effects;
        public TerrainEffectInstance GetTerrainEffect(string id) => effects.FirstOrDefault(e => string.Equals(e.Data.Id, id, StringComparison.OrdinalIgnoreCase));
        public void AddTerrainEffect(TerrainEffectInstance effect) => effects.Add(effect);
        public bool RemoveTerrainEffect(string id)
        {
            TerrainEffectInstance effect = GetTerrainEffect(id);
            return effect != null && effects.Remove(effect);
        }
        public void ClearTerrainEffects() => effects.Clear();
        public void NotifyTerrainEffectChanged() { }
    }

    public sealed class CellGrid
    {
        public readonly List<Cell> Cells = new();
        public readonly List<Unit> Units = new();
        public List<Cell> GetAllCells() => Cells;
        public List<Unit> GetAllUnits() => Units;
        public Cell ResolveCanonicalCell(Cell cell) => cell;
    }

    public static class TerrainEffectRegistry
    {
        private static readonly Dictionary<string, TerrainEffectData> Definitions = new(StringComparer.OrdinalIgnoreCase);
        public static IEnumerable<TerrainEffectData> Entries => Definitions.Values;
        public static void EnsureRegistered() { }
        public static void Register(TerrainEffectData data) => Definitions[data.Id] = data;
        public static bool TryGet(string id, out TerrainEffectData data) => Definitions.TryGetValue(id, out data);
    }
}
