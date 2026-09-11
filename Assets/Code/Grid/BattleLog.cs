using System.Linq;
using UnityEngine;

namespace Windy.Srpg.Game.Grid
{
    public static class BattleLog
    {
        public static void Log(string tag, string message)
        {
            Debug.Log($"{BuildTurnPrefix()}[{tag}] {message}");
        }

        private static string BuildTurnPrefix()
        {
            CellGrid grid = Object.FindAnyObjectByType<CellGrid>();
            if (grid == null)
            {
                grid = Resources.FindObjectsOfTypeAll<CellGrid>()
                    .FirstOrDefault(candidate =>
                        candidate != null
                        && candidate.gameObject != null
                        && candidate.gameObject.scene.IsValid()
                        && candidate.gameObject.scene.isLoaded);
            }

            return grid == null ? "[Turn ?] " : $"[Turn {grid.RoundCount}] ";
        }
    }
}
