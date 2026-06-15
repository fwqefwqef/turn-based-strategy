using System;
using System.Collections.Generic;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Players.AI
{
    public abstract class CustomUnitSelection : MonoBehaviour
    {
        public abstract IEnumerable<CustomUnit> SelectNext(Func<List<CustomUnit>> getUnits, CustomCellGrid cellGrid);
    }
}
