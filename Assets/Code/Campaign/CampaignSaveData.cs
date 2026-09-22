using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Campaign
{
    [Serializable]
    public sealed class CampaignSaveData
    {
        public int Version = 1;
        public int Gold = CampaignSaveFactory.StartingGold;
        public float[] ClearedChapterIds = Array.Empty<float>();
        public OwnedUnitSaveData[] OwnedUnits = Array.Empty<OwnedUnitSaveData>();
        public string[] DeploymentRosterUnitIds = Array.Empty<string>();
        public SavedInventoryEntryData[] StorageItems = Array.Empty<SavedInventoryEntryData>();
        public bool ShopStockInitialized;
        public ShopStockEntryData[] ShopStockItems = Array.Empty<ShopStockEntryData>();
    }

    [Serializable]
    public sealed class OwnedUnitSaveData
    {
        public string UnitId;
        public string VisualId;
        public string UnitName;
        public int Level = 1;
        public int Experience = 0;
        public string[] WeaponProficiencyIds = Array.Empty<string>();
        public UnitStatBlock BaseStats;
        public UnitGrowthRates GrowthRates;
        public SavedInventoryEntryData[] Inventory = Array.Empty<SavedInventoryEntryData>();
        public string[] SkillIds = Array.Empty<string>();
        public string[] ClassPassiveIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class SavedInventoryEntryData
    {
        public string ItemId;
        public int RemainingCharges = -1;
        public bool IsDroppable;
    }

    [Serializable]
    public sealed class ShopStockEntryData
    {
        public string ItemId;
        // -1 is unlimited stock; non-negative values are finite quantities.
        public int Quantity;
    }

    public static class ShopStockUtility
    {
        public static void AddStock(CampaignSaveData save, IEnumerable<ShopStockEntryData> additions)
        {
            if (save == null)
            {
                return;
            }

            List<ShopStockEntryData> stock = CloneAndMerge(save.ShopStockItems).ToList();
            foreach (ShopStockEntryData addition in additions ?? Array.Empty<ShopStockEntryData>())
            {
                if (addition == null || string.IsNullOrWhiteSpace(addition.ItemId) || addition.Quantity == 0)
                {
                    continue;
                }

                string itemId = addition.ItemId.Trim();
                ShopStockEntryData existing = stock.FirstOrDefault(entry =>
                    string.Equals(entry.ItemId, itemId, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    stock.Add(new ShopStockEntryData { ItemId = itemId, Quantity = Math.Max(-1, addition.Quantity) });
                }
                else if (existing.Quantity >= 0)
                {
                    existing.Quantity = addition.Quantity < 0
                        ? -1
                        : existing.Quantity + Math.Max(0, addition.Quantity);
                }
            }

            save.ShopStockItems = stock.ToArray();
        }

        public static ShopStockEntryData[] CloneAndMerge(IEnumerable<ShopStockEntryData> entries)
        {
            List<ShopStockEntryData> result = new List<ShopStockEntryData>();
            foreach (ShopStockEntryData entry in entries ?? Array.Empty<ShopStockEntryData>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId))
                {
                    continue;
                }

                AddOrMerge(result, entry.ItemId.Trim(), entry.Quantity);
            }

            return result.ToArray();
        }

        private static void AddOrMerge(List<ShopStockEntryData> stock, string itemId, int quantity)
        {
            ShopStockEntryData existing = stock.FirstOrDefault(entry =>
                string.Equals(entry.ItemId, itemId, StringComparison.OrdinalIgnoreCase));
            int normalizedQuantity = Math.Max(-1, quantity);
            if (existing == null)
            {
                stock.Add(new ShopStockEntryData { ItemId = itemId, Quantity = normalizedQuantity });
                return;
            }

            if (existing.Quantity < 0 || normalizedQuantity < 0)
            {
                existing.Quantity = -1;
            }
            else
            {
                existing.Quantity += normalizedQuantity;
            }
        }
    }

    public static class CampaignProgressUtility
    {
        private const float ChapterIdEpsilon = 0.0001f;

        public static bool IsChapterCleared(CampaignSaveData save, float chapterId)
        {
            return chapterId > 0
                && (save?.ClearedChapterIds ?? Array.Empty<float>()).Any(clearedChapterId => AreChapterIdsEqual(clearedChapterId, chapterId));
        }

        public static bool IsChapterUnlocked(CampaignSaveData save, float unlockRequiredChapterId)
        {
            return unlockRequiredChapterId <= 0 || IsChapterCleared(save, unlockRequiredChapterId);
        }

        public static bool CanEnterChapter(CampaignSaveData save, float chapterId, bool replayable, float unlockRequiredChapterId)
        {
            bool cleared = IsChapterCleared(save, chapterId);
            if (cleared && !replayable)
            {
                return false;
            }

            return IsChapterUnlocked(save, unlockRequiredChapterId);
        }

        public static void MarkChapterCleared(CampaignSaveData save, float chapterId)
        {
            if (save == null || chapterId <= 0 || IsChapterCleared(save, chapterId))
            {
                return;
            }

            save.ClearedChapterIds = NormalizeClearedChapterIds(save.ClearedChapterIds)
                .Append(chapterId)
                .OrderBy(id => id)
                .ToArray();
        }

        public static float[] NormalizeClearedChapterIds(IEnumerable<float> chapterIds)
        {
            List<float> normalizedChapterIds = new List<float>();
            IEnumerable<float> orderedChapterIds = chapterIds == null
                ? Array.Empty<float>()
                : chapterIds
                .Where(id => id > 0)
                .OrderBy(id => id);

            foreach (float chapterId in orderedChapterIds)
            {
                if (normalizedChapterIds.Any(existingId => AreChapterIdsEqual(existingId, chapterId)))
                {
                    continue;
                }

                normalizedChapterIds.Add(chapterId);
            }

            return normalizedChapterIds.ToArray();
        }

        private static bool AreChapterIdsEqual(float left, float right)
        {
            return Math.Abs(left - right) <= ChapterIdEpsilon;
        }
    }
}

