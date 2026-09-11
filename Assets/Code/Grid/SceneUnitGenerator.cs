using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid
{
    [DisallowMultipleComponent]
    public sealed class SceneUnitGenerator : MonoBehaviour, IBattleSceneUnitSource
    {
        public Transform UnitsParent;
        public Transform CellsParent;
        [SerializeField] private Unit deploymentRosterUnitPrefab;

        public Unit DeploymentRosterUnitPrefab => deploymentRosterUnitPrefab;

        public void SetDeploymentRosterUnitPrefab(Unit prefab)
        {
            deploymentRosterUnitPrefab = prefab;
        }

        public List<Unit> GetSceneUnits(bool includeExcludedFromBattle = false)
        {
            List<Unit> units = new List<Unit>();
            if (UnitsParent == null)
            {
                return units;
            }

            for (int i = 0; i < UnitsParent.childCount; i++)
            {
                Transform child = UnitsParent.GetChild(i);
                Unit unit = child.GetComponent<Unit>();
                if (unit == null)
                {
                    Debug.LogError($"SceneUnitGenerator: '{child.name}' is missing a Unit component.", child);
                    continue;
                }

                if (!includeExcludedFromBattle && unit.ExcludedFromBattle)
                {
                    continue;
                }

                units.Add(unit);
            }

            return units;
        }

        public IReadOnlyList<Transform> GetInitialUnitTransforms(CellGrid grid)
        {
            if (UnitsParent == null)
            {
                Debug.LogError("SceneUnitGenerator: Units Parent is not assigned.", this);
                return new List<Transform>();
            }

            return GetSceneUnits(includeExcludedFromBattle: false)
                .Where(unit => unit != null)
                .Select(unit => unit.transform)
                .ToList();
        }

        public void SnapToGrid()
        {
        }
    }
}
