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
            ConsumableEffectRegistry.Register("apply_invulnerable_buff", () => new ApplyBuffConsumableEffect("invulnerable"));
            UnitPassiveRegistry.Register("apply_toxic", () => new ApplyDebuffOnHit("toxic"));
            UnitPassiveRegistry.Register("apply_stun", () => new ApplyDebuffOnHit("stun"));
            UnitPassiveRegistry.Register("apply_weakening", () => new ApplyDebuffOnHit("weakening"));

            isRegistered = true;
        }

        private sealed class ApplyDebuffOnHit : IWeaponHitEffect
        {
            private readonly string buffId;
            public ApplyDebuffOnHit(string buffId) { this.buffId = buffId; }
            public void OnWeaponHit(Unit attacker, Unit target)
            {
                if (target != null && target.HitPoints > 0)
                    target.AddBuffById(buffId);
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
                return user != null && target != null && target.HitPoints > 0 && target.HitPoints < target.ComputedTotalHitPoints;
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
                return user != null && target != null && target.HitPoints > 0 && target == user;
            }

            public void Use(Unit user, Unit target)
            {
                target.AddBuffById(buffId);
            }
        }
    }
}



