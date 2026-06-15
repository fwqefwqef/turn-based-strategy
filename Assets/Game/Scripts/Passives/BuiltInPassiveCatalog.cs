using System;
using System.Collections.Generic;
using Windy.Srpg.Game.Catalogs;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Units;
using UnityEngine;

namespace Windy.Srpg.Game.Passives
{
    public static class BuiltInPassiveCatalog
    {
        private static bool isRegistered;

        public static void EnsureRegistered()
        {
            if (isRegistered)
            {
                return;
            }

            PassiveCatalogResource catalog = CatalogResourceLoader.LoadPassiveCatalog();
            PassiveRegistry.RegisterRange(catalog.ToRuntimeDefinitions());

            PassiveEffectRegistry.Register("restore_hp_3_turn_start", () => new RestoreHitPointsOnTurnStartEffect(3));
            PassiveEffectRegistry.Register("exp_x2", () => new MultiplyExperienceGainEffect(2f));
            PassiveEffectRegistry.Register("exp_set_100", () => new SetExperienceGainEffect(100));
            PassiveEffectRegistry.Register("prevent_exp_to_attackers", () => new PreventExperienceToAttackersEffect());
            isRegistered = true;
        }

        private sealed class RestoreHitPointsOnTurnStartEffect : PassiveEffectBase
        {
            private readonly int amount;

            public RestoreHitPointsOnTurnStartEffect(int amount)
            {
                this.amount = amount;
            }

            public override void OnTurnStart(CustomUnit unit, Passive entry)
            {
                unit?.RestoreHitPoints(amount, unit);
            }
        }

        private sealed class MultiplyExperienceGainEffect : PassiveEffectBase, IP_ModifyExperienceGain
        {
            private readonly float multiplier;

            public MultiplyExperienceGainEffect(float multiplier)
            {
                this.multiplier = multiplier;
            }

            public void ModifyExperienceGain(ExperienceGainContext context)
            {
                if (context == null || context.Recipient != Owner || context.Amount <= 0)
                {
                    return;
                }

                context.Amount = Mathf.FloorToInt(context.Amount * multiplier);
            }
        }

        private sealed class PreventExperienceToAttackersEffect : PassiveEffectBase, IP_PreventExperienceGain
        {
            public void PreventExperienceGain(ExperienceGainContext context)
            {
                if (context == null)
                {
                    return;
                }

                context.Prevented = true;
                context.Amount = 0;
            }
        }

        private sealed class SetExperienceGainEffect : PassiveEffectBase, IP_ModifyExperienceGain
        {
            private readonly int amount;

            public SetExperienceGainEffect(int amount)
            {
                this.amount = amount;
            }

            public void ModifyExperienceGain(ExperienceGainContext context)
            {
                if (context == null || context.Recipient != Owner || context.Amount <= 0)
                {
                    return;
                }

                context.Amount = amount;
            }
        }
    }
}
