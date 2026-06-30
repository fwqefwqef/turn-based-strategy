using System;
using System.Collections.Generic;
using System.IO;
using Windy.Srpg.Game.Buffs;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Passives;
using Windy.Srpg.Game.Skills;
using UnityEngine;

namespace Windy.Srpg.Game.Catalogs
{
    public static class CatalogResourceLoader
    {
        private const string UnifiedCatalogFileName = "gdata.json";
        private static readonly string[] CatalogSearchDirectories =
        {
            Path.Combine(Application.dataPath, "Game", "Data"),
            Application.streamingAssetsPath,
            Path.Combine(Application.dataPath, "StreamingAssets")
        };

        private static GameDataCatalogResource cachedGameDataCatalog;
        private static bool hasLoadedGameDataCatalog;

        public static T LoadResource<T>(string fileName) where T : class, new()
        {
            string fullPath = ResolveExistingPath(fileName);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"CatalogResourceLoader: Missing catalog file at '{fullPath}'.");
                return new T();
            }

            try
            {
                string json = File.ReadAllText(fullPath);
                T catalog = JsonUtility.FromJson<T>(json);
                if (catalog != null)
                {
                    return catalog;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"CatalogResourceLoader: Failed to parse '{fullPath}'. {ex.Message}");
            }

            Debug.LogError($"CatalogResourceLoader: Catalog file '{fullPath}' was empty or invalid.");
            return new T();
        }

        public static ItemCatalogResource LoadItemCatalog()
        {
            GameDataCatalogResource catalog = LoadGameDataCatalog();
            return catalog.Items ?? new ItemCatalogResource();
        }

        public static SkillCatalogResource LoadSkillCatalog()
        {
            GameDataCatalogResource catalog = LoadGameDataCatalog();
            return catalog.Skills ?? new SkillCatalogResource();
        }

        public static PassiveCatalogResource LoadPassiveCatalog()
        {
            GameDataCatalogResource catalog = LoadGameDataCatalog();
            return catalog.Passives ?? new PassiveCatalogResource();
        }

        public static BuffCatalogResource LoadBuffCatalog()
        {
            GameDataCatalogResource catalog = LoadGameDataCatalog();
            return catalog.Buffs ?? new BuffCatalogResource();
        }

        private static GameDataCatalogResource LoadGameDataCatalog()
        {
            if (!hasLoadedGameDataCatalog)
            {
                cachedGameDataCatalog = LoadResource<GameDataCatalogResource>(UnifiedCatalogFileName);
                hasLoadedGameDataCatalog = true;
            }

            return cachedGameDataCatalog ?? new GameDataCatalogResource();
        }

        public static TEnum ParseEnum<TEnum>(string rawValue, TEnum fallback) where TEnum : struct
        {
            if (!string.IsNullOrWhiteSpace(rawValue) && Enum.TryParse(rawValue, true, out TEnum parsed))
            {
                return parsed;
            }

            return fallback;
        }

        public static string[] NormalizeStrings(string[] values)
        {
            return values ?? Array.Empty<string>();
        }

        public static string NormalizeOptionalString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static string ResolveExistingPath(string fileName)
        {
            foreach (string directory in CatalogSearchDirectories)
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                string candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(CatalogSearchDirectories[0], fileName);
        }
    }

    [Serializable]
    public sealed class GameDataCatalogResource
    {
        public ItemCatalogResource Items = new ItemCatalogResource();
        public SkillCatalogResource Skills = new SkillCatalogResource();
        public PassiveCatalogResource Passives = new PassiveCatalogResource();
        public BuffCatalogResource Buffs = new BuffCatalogResource();
    }

    [Serializable]
    public sealed class ItemCatalogResource
    {
        public WeaponCatalogEntry[] Weapons = Array.Empty<WeaponCatalogEntry>();
        public AccessoryCatalogEntry[] Accessories = Array.Empty<AccessoryCatalogEntry>();
        public ConsumableCatalogEntry[] Consumables = Array.Empty<ConsumableCatalogEntry>();

        public IEnumerable<ItemData> ToRuntimeDefinitions()
        {
            foreach (WeaponCatalogEntry entry in Weapons ?? Array.Empty<WeaponCatalogEntry>())
            {
                if (entry != null)
                {
                    yield return entry.ToRuntime();
                }
            }

            foreach (AccessoryCatalogEntry entry in Accessories ?? Array.Empty<AccessoryCatalogEntry>())
            {
                if (entry != null)
                {
                    yield return entry.ToRuntime();
                }
            }

            foreach (ConsumableCatalogEntry entry in Consumables ?? Array.Empty<ConsumableCatalogEntry>())
            {
                if (entry != null)
                {
                    yield return entry.ToRuntime();
                }
            }
        }
    }

    [Serializable]
    public sealed class WeaponCatalogEntry
    {
        public string Id;
        public string Name = "item_name";
        public string Description = "item_desc";
        public int Value = 100;
        public string WeaponType = nameof(Windy.Srpg.Game.Inventory.WeaponType.Sword);
        public string DamageType = nameof(Windy.Srpg.Game.Inventory.DamageType.Physical);
        public int Might;
        public int MinRange = 1;
        public int MaxRange = 1;
        public int Accuracy = 100;
        public int Crit;
        public int NumHits = 1;
        public bool CanPursuitAttack = true;
        public bool CanCounterAttack = true;
        public bool PreventsCounterattack;
        public PrimaryStatModifiers StatModifiers;
        public string EffectId;
        public string[] GrantedSkillIds = Array.Empty<string>();

        public WeaponData ToRuntime()
        {
            return new WeaponData
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Value = Value,
                WeaponType = CatalogResourceLoader.ParseEnum(WeaponType, Windy.Srpg.Game.Inventory.WeaponType.Sword),
                DamageType = CatalogResourceLoader.ParseEnum(DamageType, Windy.Srpg.Game.Inventory.DamageType.Physical),
                Might = Might,
                MinRange = MinRange,
                MaxRange = MaxRange,
                Accuracy = Accuracy,
                Crit = Crit,
                NumHits = NumHits,
                CanPursuitAttack = CanPursuitAttack,
                CanCounterAttack = CanCounterAttack,
                PreventsCounterattack = PreventsCounterattack,
                StatModifiers = StatModifiers,
                EffectId = CatalogResourceLoader.NormalizeOptionalString(EffectId),
                GrantedSkillIds = CatalogResourceLoader.NormalizeStrings(GrantedSkillIds)
            };
        }
    }

    [Serializable]
    public sealed class AccessoryCatalogEntry
    {
        public string Id;
        public string Name = "item_name";
        public string Description = "item_desc";
        public int Value = 100;
        public PrimaryStatModifiers StatModifiers;
        public SecondaryStatModifiers SecondaryStatModifiers;
        public string EffectId;
        public string[] GrantedSkillIds = Array.Empty<string>();

        public AccessoryData ToRuntime()
        {
            return new AccessoryData
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Value = Value,
                StatModifiers = StatModifiers,
                SecondaryStatModifiers = SecondaryStatModifiers,
                EffectId = CatalogResourceLoader.NormalizeOptionalString(EffectId),
                GrantedSkillIds = CatalogResourceLoader.NormalizeStrings(GrantedSkillIds)
            };
        }
    }

    [Serializable]
    public sealed class ConsumableCatalogEntry
    {
        public string Id;
        public string Name = "item_name";
        public string Description = "item_desc";
        public int Value = 100;
        public int Charges = 3;
        public string EffectId;
        public string TargetType = nameof(ConsumableTargetType.Self);

        public ConsumableData ToRuntime()
        {
            return new ConsumableData
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Value = Value,
                Charges = Charges,
                EffectId = CatalogResourceLoader.NormalizeOptionalString(EffectId),
                TargetType = CatalogResourceLoader.ParseEnum(TargetType, ConsumableTargetType.Self)
            };
        }
    }

    [Serializable]
    public sealed class SkillCatalogResource
    {
        public SkillCatalogEntry[] Skills = Array.Empty<SkillCatalogEntry>();

        public IEnumerable<SkillData> ToRuntimeDefinitions()
        {
            foreach (SkillCatalogEntry entry in Skills ?? Array.Empty<SkillCatalogEntry>())
            {
                if (entry != null)
                {
                    yield return entry.ToRuntime();
                }
            }
        }
    }

    [Serializable]
    public sealed class SkillCatalogEntry
    {
        public string Id;
        public string Name = "skill_name";
        public string Description = "skill_desc";
        public string Category = nameof(SkillCategory.Misc);
        public string TargetingType = nameof(SkillTargetingType.None);
        public string RequiredWeaponType = nameof(CombatArtWeaponType.Any);
        public bool EndsTurn = true;
        public bool OncePerTurn = true;
        public bool SelfImmune;
        public int MpCost = 3;
        public string EffectId;
        public SkillAttackProfileCatalogEntry AttackProfile = new SkillAttackProfileCatalogEntry();
        public SkillAreaProfileCatalogEntry AreaProfile = new SkillAreaProfileCatalogEntry();

        public SkillData ToRuntime()
        {
            return new SkillData
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Category = CatalogResourceLoader.ParseEnum(Category, SkillCategory.Misc),
                TargetingType = CatalogResourceLoader.ParseEnum(TargetingType, SkillTargetingType.None),
                RequiredWeaponType = CatalogResourceLoader.ParseEnum(RequiredWeaponType, CombatArtWeaponType.Any),
                EndsTurn = EndsTurn,
                OncePerTurn = OncePerTurn,
                SelfImmune = SelfImmune,
                MpCost = MpCost,
                EffectId = CatalogResourceLoader.NormalizeOptionalString(EffectId),
                AttackProfile = (AttackProfile ?? new SkillAttackProfileCatalogEntry()).ToRuntime(),
                AreaProfile = (AreaProfile ?? new SkillAreaProfileCatalogEntry()).ToRuntime()
            };
        }
    }

    [Serializable]
    public sealed class SkillAttackProfileCatalogEntry
    {
        public bool Enabled;
        public bool IsMagic;
        public int Might;
        public int Accuracy = 100;
        public int Crit;
        public int MinRange;
        public int MaxRange;
        public int NumHits = 1;
        public bool PreventsCounterattack;

        public SkillAttackProfile ToRuntime()
        {
            return new SkillAttackProfile
            {
                Enabled = Enabled,
                IsMagic = IsMagic,
                Might = Might,
                Accuracy = Accuracy,
                Crit = Crit,
                MinRange = MinRange,
                MaxRange = MaxRange,
                NumHits = NumHits,
                PreventsCounterattack = PreventsCounterattack
            };
        }
    }

    [Serializable]
    public sealed class SkillAreaProfileCatalogEntry
    {
        public bool Enabled;
        public string Shape = nameof(SkillAreaShape.Centered);
        public int MinRange;
        public int MaxRange;
        public int Radius;
        public int Might;
        public bool IsMagic;
        public bool AffectsAllies;
        public bool AffectsEnemies;

        public SkillAreaProfile ToRuntime()
        {
            return new SkillAreaProfile
            {
                Enabled = Enabled,
                Shape = CatalogResourceLoader.ParseEnum(Shape, SkillAreaShape.Centered),
                MinRange = MinRange,
                MaxRange = MaxRange,
                Radius = Radius,
                Might = Might,
                IsMagic = IsMagic,
                AffectsAllies = AffectsAllies,
                AffectsEnemies = AffectsEnemies
            };
        }
    }

    [Serializable]
    public sealed class PassiveCatalogResource
    {
        public PassiveCatalogEntry[] Passives = Array.Empty<PassiveCatalogEntry>();

        public IEnumerable<PassiveData> ToRuntimeDefinitions()
        {
            foreach (PassiveCatalogEntry entry in Passives ?? Array.Empty<PassiveCatalogEntry>())
            {
                if (entry != null)
                {
                    yield return entry.ToRuntime();
                }
            }
        }
    }

    [Serializable]
    public sealed class PassiveCatalogEntry
    {
        public string Id;
        public string Name = "passive_name";
        public string Description = "passive_desc";
        public int Cost;
        public PrimaryStatModifiers PrimaryStatModifiers;
        public SecondaryStatModifiers SecondaryStatModifiers;
        public string EffectId;

        public PassiveData ToRuntime()
        {
            return new PassiveData
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Cost = Cost,
                PrimaryStatModifiers = PrimaryStatModifiers,
                SecondaryStatModifiers = SecondaryStatModifiers,
                EffectId = CatalogResourceLoader.NormalizeOptionalString(EffectId)
            };
        }
    }

    [Serializable]
    public sealed class BuffCatalogResource
    {
        public BuffCatalogEntry[] Buffs = Array.Empty<BuffCatalogEntry>();

        public IEnumerable<BuffData> ToRuntimeDefinitions()
        {
            foreach (BuffCatalogEntry entry in Buffs ?? Array.Empty<BuffCatalogEntry>())
            {
                if (entry != null)
                {
                    yield return entry.ToRuntime();
                }
            }
        }
    }

    [Serializable]
    public sealed class BuffCatalogEntry
    {
        public string Id;
        public string Name = "buff_name";
        public string Description = "buff_desc";
        public int Duration = 1;
        public PrimaryStatModifiers PrimaryStatModifiers;
        public SecondaryStatModifiers SecondaryStatModifiers;
        public string EffectId;

        public BuffData ToRuntime()
        {
            return new BuffData
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Duration = Duration,
                PrimaryStatModifiers = PrimaryStatModifiers,
                SecondaryStatModifiers = SecondaryStatModifiers,
                EffectId = CatalogResourceLoader.NormalizeOptionalString(EffectId)
            };
        }
    }
}

