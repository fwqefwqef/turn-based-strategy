using Windy.Srpg.Game.Catalogs;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Buffs
{
    public static class BuiltInBuffCatalog
    {
        private static bool isRegistered;

        public static void EnsureRegistered()
        {
            if (isRegistered)
            {
                return;
            }

            BuffCatalogResource catalog = CatalogResourceLoader.LoadBuffCatalog();
            BuffRegistry.RegisterRange(catalog.ToRuntimeDefinitions());

            BuffEffectRegistry.Register("damage_to_one", () => new DamageToOneBuffEffect());
            BuffEffectRegistry.Register("ignore_def_res", () => new IgnoreDefRes());
            isRegistered = true;
        }

        private sealed class DamageToOneBuffEffect : BuffEffectBase, IP_TakeDamageChange
        {
            public void TakeDamageChange(DamageChangeContext context)
            {
                if (context.Phase != DamageChangePhase.Damage)
                {
                    return;
                }

                context.Damage = context.Damage <= 0 ? 0 : 1;
            }
        }

        private sealed class IgnoreDefRes : BuffEffectBase, IP_DamageChange, IP_AfterCombat_Attacker
        {
            public void AfterCombatSequenceAsAttacker(CombatSequenceContext context)
            {
                this.SelfRemove();
            }
            
            public void DamageChange(DamageChangeContext context)
            {
                if (context.Phase != DamageChangePhase.Damage || !context.IsHit)
                {
                    return;
                }

                if (context.IsMagicAttack)
                {
                    context.Damage += context.Defender.Resistance;
                }
                else
                {
                    context.Damage += context.Defender.Defense;
                }
            }

        }
    }
}

