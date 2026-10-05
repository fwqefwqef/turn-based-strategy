using System;
using System.Collections.Generic;
using Windy.Srpg.Game.Catalogs;
using Windy.Srpg.Game.Units;
using UnityEngine;

namespace Windy.Srpg.Game.Inventory
{
    public static class BuiltInItemCatalog
    {
        private static bool isRegistered;

        public static void EnsureRegistered()
        {
            if (isRegistered)
            {
                return;
            }

            ItemCatalogResource catalog = CatalogResourceLoader.LoadItemCatalog();
            ItemRegistry.RegisterRange(catalog.ToRuntimeDefinitions());

            ConsumableEffectRegistry.Register("heal_10", () => new HealConsumableEffect(10));
            ConsumableEffectRegistry.Register("heal_20", () => new HealConsumableEffect(20));
            ConsumableEffectRegistry.Register("heal_50", () => new HealConsumableEffect(50));
            ConsumableEffectRegistry.Register("restore_mp_10", () => new RestoreManaConsumableEffect(10));
            ConsumableEffectRegistry.Register("restore_mp_25", () => new RestoreManaConsumableEffect(25));
            ConsumableEffectRegistry.Register("apply_invulnerable_buff", () => new ApplyBuffConsumableEffect("invulnerable"));
            ConsumableEffectRegistry.Register("increase_strength_2", () => new PermanentStatConsumableEffect(PermanentStatKind.Strength, 2));
            ConsumableEffectRegistry.Register("increase_magic_2", () => new PermanentStatConsumableEffect(PermanentStatKind.Magic, 2));
            ConsumableEffectRegistry.Register("increase_defense_2", () => new PermanentStatConsumableEffect(PermanentStatKind.Defense, 2));
            ConsumableEffectRegistry.Register("increase_speed_2", () => new PermanentStatConsumableEffect(PermanentStatKind.Speed, 2));
            ConsumableEffectRegistry.Register("increase_luck_2", () => new PermanentStatConsumableEffect(PermanentStatKind.Luck, 2));
            ConsumableEffectRegistry.Register("increase_movement_2", () => new PermanentStatConsumableEffect(PermanentStatKind.Movement, 2));
            UnitPassiveRegistry.Register("apply_toxic", () => new ApplyDebuffOnHit("toxic"));
            UnitPassiveRegistry.Register("apply_stun", () => new ApplyDebuffOnHit("stun"));
            UnitPassiveRegistry.Register("apply_weakening", () => new ApplyDebuffOnHit("weakening"));
            UnitPassiveRegistry.Register("apply_burn", () => new ApplyDebuffOnHit("burn"));

            isRegistered = true;
        }

        private sealed class ApplyDebuffOnHit : IWeaponHitEffect
        {
            private readonly string buffId;
            public ApplyDebuffOnHit(string buffId) { this.buffId = buffId; }
            public void OnWeaponHit(Unit attacker, Unit target)
            {
                if (target != null && target.IsAliveForBattle)
                    target.AddBuffById(buffId, attacker);
            }
        }

        private sealed class HealConsumableEffect : IConsumableEffect
        {
            private readonly int amount;

            public HealConsumableEffect(int amount)
            {
                this.amount = amount;
            }

            public bool CanUse(Unit user, Unit target)
            {
                return user != null && target != null && target.IsAliveForBattle && target.HitPoints < target.ComputedTotalHitPoints;
            }

            public void Use(Unit user, Unit target)
            {
                target.RestoreHitPoints(amount, user);
            }
        }

        private sealed class ApplyBuffConsumableEffect : IConsumableEffect
        {
            private readonly string buffId;

            public ApplyBuffConsumableEffect(string buffId)
            {
                this.buffId = buffId;
            }

            public bool CanUse(Unit user, Unit target)
            {
                return user != null && target != null && target.IsAliveForBattle && target == user;
            }

            public void Use(Unit user, Unit target)
            {
                target.AddBuffById(buffId);
            }
        }

        private sealed class RestoreManaConsumableEffect : IConsumableEffect
        {
            private readonly int amount;

            public RestoreManaConsumableEffect(int amount)
            {
                this.amount = amount;
            }

            public bool CanUse(Unit user, Unit target)
            {
                return user != null && target != null && target.IsAliveForBattle
                    && target.CurrentManaPoints < target.ComputedTotalManaPoints;
            }

            public void Use(Unit user, Unit target)
            {
                target.RestoreManaPoints(amount);
            }
        }

        private sealed class PermanentStatConsumableEffect : IConsumableEffect, IPreBattleConsumableEffect
        {
            private readonly PermanentStatKind stat;
            private readonly int amount;

            public PermanentStatConsumableEffect(PermanentStatKind stat, int amount)
            {
                this.stat = stat;
                this.amount = amount;
            }

            public bool CanUse(Unit user, Unit target)
            {
                return user != null && target == user && user.IsAliveForBattle;
            }

            public void Use(Unit user, Unit target)
            {
                target.ApplyPermanentStatIncrease(stat, amount);
            }

            public bool CanUsePreBattle(Campaign.OwnedUnitSaveData target)
            {
                return target != null;
            }

            public void UsePreBattle(Campaign.OwnedUnitSaveData target)
            {
                UnitStatBlock stats = target.BaseStats;
                switch (stat)
                {
                    case PermanentStatKind.Strength: stats.Strength += amount; break;
                    case PermanentStatKind.Magic: stats.Magic += amount; break;
                    case PermanentStatKind.Defense: stats.Defense += amount; break;
                    case PermanentStatKind.Speed: stats.Speed += amount; break;
                    case PermanentStatKind.Luck: stats.Luck += amount; break;
                    case PermanentStatKind.Movement: stats.MovementPoints += amount; break;
                }
                target.BaseStats = stats;
            }
        }
    }
}



