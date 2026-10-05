using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Units;
using UnityEngine;

namespace Windy.Srpg.Game.Passives
{
    [Serializable]
    public class Passive
    {
        [SerializeField]
        private string passiveId;

        [NonSerialized]
        private IP_PassiveEffect effectInstance;

        public string PassiveId => passiveId;
        public PassiveData Data => PassiveRegistry.Get(passiveId);
        public IP_PassiveEffect EffectInstance => effectInstance;

        public Passive()
        {
        }

        public Passive(string passiveId)
        {
            this.passiveId = passiveId;
            TryCreateEffectInstance();
        }

        public Passive(PassiveData data)
        {
            if (data == null)
            {
                return;
            }

            passiveId = data.Id;
            TryCreateEffectInstance();
        }

        private void TryCreateEffectInstance()
        {
            if (Data == null)
            {
                effectInstance = null;
                return;
            }

            PassiveEffectRegistry.TryCreate(Data.EffectId, out effectInstance);
        }
    }

    public sealed class UnitPassiveList
    {
        private readonly Unit owner;
        private readonly List<Passive> entries = new List<Passive>();
        private readonly Dictionary<string, Passive> equipmentGrantedEntries = new Dictionary<string, Passive>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Passive> combinedEntries = new List<Passive>();
        private bool combinedEntriesDirty = true;

        public IReadOnlyList<Passive> ClassEntries => entries;
        public IReadOnlyList<Passive> Entries
        {
            get
            {
                RebuildCombinedEntriesIfNeeded();
                return combinedEntries;
            }
        }

        public UnitPassiveList(Unit owner)
        {
            this.owner = owner;
        }

        public void LoadStartingPassives(IEnumerable<StartingPassiveEntry> classPassives)
        {
            ClearInternal();

            foreach (StartingPassiveEntry entry in classPassives ?? Array.Empty<StartingPassiveEntry>())
            {
                AddPassiveById(entry.PassiveId, notifyOwner: false);
            }

            RefreshEquipmentGrantedPassives(notifyOwner: false);
            NotifyOwnerChanged();
        }

        public Passive AddPassive(PassiveData data, bool notifyOwner = true)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.Id))
            {
                return null;
            }

            PassiveRegistry.Register(data);

            if (ContainsPassiveId(data.Id))
            {
                return entries.FirstOrDefault(entry => string.Equals(entry.PassiveId, data.Id, StringComparison.OrdinalIgnoreCase));
            }

            Passive passive = new Passive(data);
            entries.Add(passive);
            combinedEntriesDirty = true;
            if (equipmentGrantedEntries.Remove(data.Id, out Passive equipmentPassive))
                equipmentPassive.EffectInstance?.OnRemove(owner, equipmentPassive);
            passive.EffectInstance?.OnApply(owner, passive);

            if (notifyOwner)
            {
                NotifyOwnerChanged();
            }

            return passive;
        }

        public Passive AddPassiveById(string passiveId, bool notifyOwner = true)
        {
            if (string.IsNullOrWhiteSpace(passiveId))
            {
                return null;
            }

            if (!PassiveRegistry.TryGet(passiveId, out PassiveData data))
            {
                Debug.LogWarning($"UnitPassiveList: Passive id '{passiveId}' is not registered.");
                return null;
            }

            return AddPassive(data, notifyOwner);
        }

        public Passive AddPassiveByIdFirst(string passiveId, bool notifyOwner = true)
        {
            Passive passive = AddPassiveById(passiveId, notifyOwner: false);
            if (passive == null)
            {
                return null;
            }

            if (entries.Remove(passive))
            {
                entries.Insert(0, passive);
                combinedEntriesDirty = true;
            }

            if (notifyOwner)
            {
                NotifyOwnerChanged();
            }

            return passive;
        }

        public bool RemovePassive(Passive entry, bool notifyOwner = true)
        {
            if (entry == null || !entries.Remove(entry))
            {
                return false;
            }

            entry.EffectInstance?.OnRemove(owner, entry);
            RefreshEquipmentGrantedPassives(notifyOwner: false);
            combinedEntriesDirty = true;
            if (notifyOwner)
            {
                NotifyOwnerChanged();
            }

            return true;
        }

        public void OnTurnStart()
        {
            foreach (Passive entry in Entries)
            {
                entry?.EffectInstance?.OnTurnStart(owner, entry);
            }
        }

        public void OnTurnEnd()
        {
            foreach (Passive entry in Entries)
            {
                entry?.EffectInstance?.OnTurnEnd(owner, entry);
            }
        }

        public IEnumerable<IP_PassiveEffect> GetActiveEffects()
        {
            return Entries
                .Select(entry => entry?.EffectInstance)
                .Where(effect => effect != null)
                .ToList();
        }

        public PrimaryStatModifiers GetPrimaryStatModifiers()
        {
            PrimaryStatModifiers modifiers = default;
            foreach (Passive entry in Entries)
            {
                if (entry?.Data == null)
                {
                    continue;
                }

                modifiers += entry.Data.PrimaryStatModifiers;
                if (entry.EffectInstance is IP_DynamicPrimaryStatModifier dynamicModifier)
                {
                    modifiers += dynamicModifier.GetPrimaryStatModifiers(owner);
                }
            }

            return modifiers;
        }

        public SecondaryStatModifiers GetSecondaryStatModifiers()
        {
            SecondaryStatModifiers modifiers = default;
            foreach (Passive entry in Entries)
            {
                if (entry?.Data == null)
                {
                    continue;
                }

                modifiers += entry.Data.SecondaryStatModifiers;
                if (entry.EffectInstance is IP_DynamicSecondaryStatModifier dynamicModifier)
                {
                    modifiers += dynamicModifier.GetSecondaryStatModifiers(owner);
                }
            }

            return modifiers;
        }

        public float GetMovementPointModifier()
        {
            float modifier = 0f;
            foreach (Passive entry in Entries)
            {
                if (entry?.EffectInstance is IP_MovementPointModifier movementModifier)
                {
                    modifier += movementModifier.GetMovementPointModifier(owner);
                }
            }

            return modifier;
        }

        public float GetPostActionMovementPoints()
        {
            float movementPoints = 0f;
            foreach (Passive entry in Entries)
            {
                if (entry?.EffectInstance is IP_PostActionMovement postActionMovement)
                {
                    movementPoints = Mathf.Max(movementPoints, postActionMovement.GetPostActionMovementPoints(owner));
                }
            }

            return movementPoints;
        }

        public bool IgnoresTerrainMovementCost => Entries.Any(entry =>
            entry?.EffectInstance is IP_IgnoreTerrainMovementCost);

        public bool CanTraverseUntraversableTerrain => Entries.Any(entry =>
            entry?.EffectInstance is IP_TraverseUntraversableTerrain);

        public bool IsImmuneToCrowdControl => Entries.Any(entry =>
            entry?.EffectInstance is IP_CrowdControlImmunity);

        public int GetSpellMaxRangeModifier(SkillData skill)
        {
            if (skill == null) return 0;

            int modifier = 0;
            foreach (Passive entry in Entries)
            {
                if (entry?.EffectInstance is IP_SpellMaxRangeModifier rangeModifier)
                    modifier += rangeModifier.GetSpellMaxRangeModifier(owner, skill);
            }
            return modifier;
        }

        public void NotifyHealingPerformed(Unit target, int actualAmount)
        {
            if (target == null || target.PlayerNumber != owner.PlayerNumber || actualAmount <= 0) return;
            foreach (Passive entry in Entries.ToList())
            {
                if (entry?.EffectInstance is IP_HealingPerformed healingEffect)
                    healingEffect.OnHealingPerformed(owner, target, actualAmount);
            }
        }

        public void Clear()
        {
            ClearInternal();
            NotifyOwnerChanged();
        }

        public void RefreshEquipmentGrantedPassives(bool notifyOwner = true)
        {
            string[] desiredIds = GetEquipmentGrantedPassiveIds()
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var desiredSet = new HashSet<string>(desiredIds, StringComparer.OrdinalIgnoreCase);
            combinedEntriesDirty = true;

            foreach (string id in equipmentGrantedEntries.Keys.ToList())
            {
                if (desiredSet.Contains(id) && !ContainsPassiveId(id)) continue;
                Passive previous = equipmentGrantedEntries[id];
                previous.EffectInstance?.OnRemove(owner, previous);
                equipmentGrantedEntries.Remove(id);
            }

            foreach (string id in desiredIds)
            {
                if (ContainsPassiveId(id) || equipmentGrantedEntries.ContainsKey(id)) continue;
                if (!PassiveRegistry.TryGet(id, out _))
                {
                    Debug.LogWarning($"UnitPassiveList: Equipment-granted passive id '{id}' is not registered.");
                    continue;
                }

                var passive = new Passive(id);
                equipmentGrantedEntries[id] = passive;
                passive.EffectInstance?.OnApply(owner, passive);
            }

            if (notifyOwner) NotifyOwnerChanged();
        }

        private IEnumerable<string> GetEquipmentGrantedPassiveIds()
        {
            if (owner?.Inventory?.EquippedWeapon?.GrantedPassiveIds != null)
                foreach (string id in owner.Inventory.EquippedWeapon.GrantedPassiveIds) yield return id;

            if (owner?.Inventory?.EquippedAccessory?.GrantedPassiveIds != null)
                foreach (string id in owner.Inventory.EquippedAccessory.GrantedPassiveIds) yield return id;
        }

        private bool ContainsPassiveId(string passiveId)
        {
            return entries.Any(entry => string.Equals(entry?.PassiveId, passiveId, StringComparison.OrdinalIgnoreCase));
        }

        private void ClearInternal()
        {
            foreach (Passive entry in entries.ToList())
            {
                entry?.EffectInstance?.OnRemove(owner, entry);
            }

            entries.Clear();
            foreach (Passive entry in equipmentGrantedEntries.Values)
                entry?.EffectInstance?.OnRemove(owner, entry);
            equipmentGrantedEntries.Clear();
            combinedEntriesDirty = true;
        }

        private void RebuildCombinedEntriesIfNeeded()
        {
            if (!combinedEntriesDirty) return;
            combinedEntries.Clear();
            combinedEntries.AddRange(entries);
            combinedEntries.AddRange(equipmentGrantedEntries.Values);
            combinedEntriesDirty = false;
        }

        private void NotifyOwnerChanged()
        {
            owner?.OnPassivesChanged();
        }
    }
}
