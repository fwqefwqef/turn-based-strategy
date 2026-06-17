using System;
using System.Collections.Generic;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Players.AI
{
    public abstract class UnitSelection : MonoBehaviour
    {
        public abstract IEnumerable<Unit> SelectNext(Func<List<Unit>> getUnits, CellGrid cellGrid);
    }
}

