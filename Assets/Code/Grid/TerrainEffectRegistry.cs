using System;
using System.Collections.Generic;
using Windy.Srpg.Game.Catalogs;

namespace Windy.Srpg.Game.Grid
{
    public static class TerrainEffectRegistry
    {
        private static readonly Dictionary<string, TerrainEffectData> Definitions =
            new Dictionary<string, TerrainEffectData>(StringComparer.OrdinalIgnoreCase);
        private static bool isRegistered;

        public static IEnumerable<TerrainEffectData> Entries
        {
            get
            {
                EnsureRegistered();
                return Definitions.Values;
            }
        }

        public static void EnsureRegistered()
        {
            if (isRegistered)
            {
                return;
            }

            isRegistered = true;
            RegisterRange(CatalogResourceLoader.LoadTerrainEffectCatalog().ToRuntimeDefinitions());
        }

        public static void Register(TerrainEffectData definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                return;
            }

            Definitions[definition.Id] = definition;
        }

        public static void RegisterRange(IEnumerable<TerrainEffectData> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            foreach (TerrainEffectData definition in definitions)
            {
                Register(definition);
            }
        }

        public static bool TryGet(string id, out TerrainEffectData definition)
        {
            EnsureRegistered();
            if (string.IsNullOrWhiteSpace(id))
            {
                definition = null;
                return false;
            }

            return Definitions.TryGetValue(id, out definition);
        }

        public static void Clear()
        {
            Definitions.Clear();
            isRegistered = false;
        }
    }
}
