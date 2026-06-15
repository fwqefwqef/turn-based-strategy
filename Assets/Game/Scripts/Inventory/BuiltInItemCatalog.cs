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

            isRegistered = true;
        }

        private sealed class HealConsumableEffect : IConsumableEffect
        {
            private readonly int amount;

            public HealConsumableEffect(int amount)
            {
                this.amount = amount;
            }

            public bool CanUse(CustomUnit user, CustomUnit target)
            {
                return user != null && target != null && target.HitPoints > 0 && target.HitPoints < target.ComputedTotalHitPoints;
            }

            public void Use(CustomUnit user, CustomUnit target)
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

            public bool CanUse(CustomUnit user, CustomUnit target)
            {
                return user != null && target != null && target.HitPoints > 0 && target == user;
            }

            public void Use(CustomUnit user, CustomUnit target)
            {
                target.AddBuffById(buffId);
            }
        }
    }
}


