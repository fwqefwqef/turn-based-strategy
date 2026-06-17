using System.Collections.Generic;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        private readonly List<CustomUnit> deferredDestroyQueue = new List<CustomUnit>();

        public void EnqueueDeferredDestroy(CustomUnit unit)
        {
            if (unit == null || deferredDestroyQueue.Contains(unit))
            {
                return;
            }

            deferredDestroyQueue.Add(unit);
        }

        public void TryFlushDeferredDestroyQueue()
        {
            if (CustomUnit.IsAnyCombatPresentationActive)
            {
                return;
            }

            FlushDeferredDestroyQueue();
        }

        private void FlushDeferredDestroyQueue()
        {
            for (int i = deferredDestroyQueue.Count - 1; i >= 0; i--)
            {
                CustomUnit unit = deferredDestroyQueue[i];
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
