using UnityEngine;
using UnityEngine.Serialization;
using Windy.Srpg.Runtime.Grid;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Players
{
    public abstract class BattlePlayerController : MonoBehaviour, IBattleTurnPlayer
    {
        [FormerlySerializedAs("PlayerNumber")]
        [SerializeField] private int playerId;

        public int PlayerId => playerId;
        public int PlayerNumber => playerId;
        public abstract bool IsHumanControlled { get; }

        public virtual bool Owns(IGridUnit unit)
        {
            return unit != null && unit.PlayerId == playerId;
        }

        public virtual void BindToGrid(IGridContext grid)
        {
        }

        public virtual void PlayTurn(IGridContext grid)
        {
        }
    }
}

