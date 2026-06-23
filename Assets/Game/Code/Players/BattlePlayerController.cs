using UnityEngine;
using UnityEngine.Serialization;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Players
{
    public abstract class BattlePlayerController : MonoBehaviour, IBattleTurnPlayer
    {
        [FormerlySerializedAs("PlayerNumber")]
        [SerializeField] private int playerId;

        public int PlayerId => playerId;
        public int PlayerNumber => playerId;
        public abstract bool IsHumanControlled { get; }

        public virtual bool Owns(Unit unit)
        {
            return unit != null && unit.PlayerId == playerId;
        }

        public virtual void BindToGrid(CellGrid grid)
        {
        }

        public virtual void PlayTurn(CellGrid grid)
        {
        }
    }
}
