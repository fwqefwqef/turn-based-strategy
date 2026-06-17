using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Units;
using UnityEngine;

namespace Windy.Srpg.Game.Skills
{
    [Serializable]
    public class Skill
    {
        [SerializeField]
        private string skillId;

        [NonSerialized]
        private bool usedThisTurn;

        public string SkillId => skillId;
        public SkillData Data => SkillRegistry.Get(skillId);
        public bool UsedThisTurn => usedThisTurn;

        public Skill()
        {
        }

        public Skill(string skillId)
        {
            this.skillId = skillId;
        }

        public Skill(SkillData data)
        {
            if (data == null)
            {
                return;
            }

            skillId = data.Id;
        }

        public void MarkUsedThisTurn()
        {
            usedThisTurn = true;
        }

        public void ResetTurnUsage()
        {
            usedThisTurn = false;
        }
    }

    public sealed class UnitSkillList
    {
        private readonly Unit owner;
        private readonly List<Skill> entries = new List<Skill>();
        private readonly Dictionary<string, Skill> equipmentGrantedEntries = new Dictionary<string, Skill>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Skill> combinedEntries = new List<Skill>();
        private bool combinedEntriesDirty = true;

        public IReadOnlyList<Skill> Entries
        {
            get
            {
                RebuildCombinedEntriesIfNeeded();
                return combinedEntries;
            }
        }
        public IReadOnlyList<Skill> LearnedEntries => entries;

        public UnitSkillList(Unit owner)
        {
            this.owner = owner;
        }

        public void LoadStartingSkills(IEnumerable<StartingSkillEntry> startingSkills)
        {
            entries.Clear();
            MarkEntriesDirty();

            if (startingSkills == null)
            {
                return;
            }

            foreach (var skill in startingSkills)
            {
                AddSkillById(skill.SkillId);
            }
        }

        public Skill AddSkill(SkillData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.Id))
            {
                return null;
            }

            SkillRegistry.Register(data);
            if (entries.Any(entry => string.Equals(entry.SkillId, data.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return entries.First(entry => string.Equals(entry.SkillId, data.Id, StringComparison.OrdinalIgnoreCase));
            }

            var entry = new Skill(data);
            entries.Add(entry);
            equipmentGrantedEntries.Remove(data.Id);
            MarkEntriesDirty();
            return entry;
        }

        public Skill AddSkillById(string skillId)
        {
            if (!SkillRegistry.TryGet(skillId, out var data))
            {
                Debug.LogWarning($"UnitSkillList: Skill id '{skillId}' is not registered.");
                return null;
            }

            return AddSkill(data);
        }

        public bool RemoveSkill(Skill entry)
        {
            if (entry == null || !entries.Remove(entry))
            {
                return false;
            }

            RefreshEquipmentGrantedSkills();
            MarkEntriesDirty();
            return true;
        }

        public IEnumerable<SkillData> GetKnownSkills()
        {
            return entries
                .Concat(equipmentGrantedEntries.Values)
                .Select(entry => entry?.Data)
                .Where(data => data != null);
        }

        public bool CanUse(Skill entry)
        {
            if (entry == null || !ContainsEntry(entry) || owner == null || !owner.CanStartActionThisTurn)
            {
                return false;
            }

            var data = entry.Data;
            if (data == null)
            {
                return false;
            }

            if (owner.CurrentManaPoints < Mathf.Max(0, data.MpCost))
            {
                return false;
            }

            return !data.OncePerTurn || !entry.UsedThisTurn;
        }

        public bool MarkUsed(Skill entry)
        {
            if (!CanUse(entry))
            {
                return false;
            }

            if (!owner.TrySpendManaPoints(Mathf.Max(0, entry.Data.MpCost)))
            {
                return false;
            }

            if (entry.Data.OncePerTurn)
            {
                entry.MarkUsedThisTurn();
            }

            return true;
        }

        public void ResetTurnUsage()
        {
            foreach (var entry in entries)
            {
                entry?.ResetTurnUsage();
            }

            foreach (var entry in equipmentGrantedEntries.Values)
            {
                entry?.ResetTurnUsage();
            }
        }

        public void Clear()
        {
            entries.Clear();
            equipmentGrantedEntries.Clear();
            MarkEntriesDirty();
        }

        public void RefreshEquipmentGrantedSkills()
        {
            equipmentGrantedEntries.Clear();

            foreach (var skillId in GetEquipmentGrantedSkillIds())
            {
                if (string.IsNullOrWhiteSpace(skillId))
                {
                    continue;
                }

                if (!SkillRegistry.TryGet(skillId, out _))
                {
                    Debug.LogWarning($"UnitSkillList: Equipment-granted skill id '{skillId}' is not registered.");
                    continue;
                }

                if (entries.Any(entry => string.Equals(entry.SkillId, skillId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                equipmentGrantedEntries[skillId] = new Skill(skillId);
            }

            MarkEntriesDirty();
        }

        private IEnumerable<string> GetEquipmentGrantedSkillIds()
        {
            if (owner?.Inventory?.EquippedWeapon?.GrantedSkillIds != null)
            {
                foreach (var skillId in owner.Inventory.EquippedWeapon.GrantedSkillIds)
                {
                    yield return skillId;
                }
            }

            if (owner?.Inventory?.EquippedAccessory?.GrantedSkillIds != null)
            {
                foreach (var skillId in owner.Inventory.EquippedAccessory.GrantedSkillIds)
                {
                    yield return skillId;
                }
            }
        }

        private bool ContainsEntry(Skill entry)
        {
            return entries.Contains(entry) || equipmentGrantedEntries.Values.Contains(entry);
        }

        private void RebuildCombinedEntriesIfNeeded()
        {
            if (!combinedEntriesDirty)
            {
                return;
            }

            combinedEntries.Clear();
            combinedEntries.AddRange(entries);
            combinedEntries.AddRange(equipmentGrantedEntries.Values);
            combinedEntriesDirty = false;
        }

        private void MarkEntriesDirty()
        {
            combinedEntriesDirty = true;
        }
    }
}

