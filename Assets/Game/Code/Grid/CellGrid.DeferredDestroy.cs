using System.Collections.Generic;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CellGrid
    {
        private readonly List<Unit> deferredDestroyQueue = new List<Unit>();

        public void EnqueueDeferredDestroy(Unit unit)
        {
            if (unit == null || deferredDestroyQueue.Contains(unit))
            {
                return;
            }

            deferredDestroyQueue.Add(unit);
        }

        public void TryFlushDeferredDestroyQueue()
        {
            if (Unit.IsAnyCombatPresentationActive)
            {
                return;
            }

            FlushDeferredDestroyQueue();
        }

        private void FlushDeferredDestroyQueue()
        {
            for (int i = deferredDestroyQueue.Count - 1; i >= 0; i--)
            {
                Unit unit = deferredDestroyQueue[i];
                deferredDestroyQueue.RemoveAt(i);
                unit?.CompleteDeferredDestroy();
            }
        }

        private void ProcessDeferredDestroyQueue()
        {
            TryFlushDeferredDestroyQueue();
        }
    }
}

