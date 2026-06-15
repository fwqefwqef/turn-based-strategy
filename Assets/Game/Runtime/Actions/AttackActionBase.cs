using UnityEngine;

namespace Windy.Srpg.Runtime.Actions
{
    public abstract class AttackActionBase : BattleAction
    {
        [SerializeField] private int minRange = 1;
        [SerializeField] private int maxRange = 1;

        public int MinRange => Mathf.Max(0, minRange);
        public int MaxRange => Mathf.Max(MinRange, maxRange);
    }
}
