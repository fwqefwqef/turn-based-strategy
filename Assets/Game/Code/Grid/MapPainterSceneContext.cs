using UnityEngine;

namespace Windy.Srpg.Game.Grid
{
    /// <summary>
    /// Scene-owned metadata for the editor map painter. Keeps the authored map bounds and
    /// root references in one place so painted scenes can be reopened and edited later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapPainterSceneContext : MonoBehaviour
    {
        [SerializeField] private CellGrid cellGrid;
        [SerializeField] private SceneUnitGenerator sceneUnitGenerator;
        [SerializeField] private Transform deploymentSlotsParent;
        [SerializeField] private int mapWidth = 20;
        [SerializeField] private int mapHeight = 20;

        public CellGrid CellGrid
        {
            get => cellGrid;
            set => cellGrid = value;
        }

        public SceneUnitGenerator SceneUnitGenerator
        {
            get => sceneUnitGenerator;
            set => sceneUnitGenerator = value;
        }

        public Transform DeploymentSlotsParent
        {
            get => deploymentSlotsParent;
            set => deploymentSlotsParent = value;
        }

        public int MapWidth
        {
            get => Mathf.Max(1, mapWidth);
            set => mapWidth = Mathf.Max(1, value);
        }

        public int MapHeight
        {
            get => Mathf.Max(1, mapHeight);
            set => mapHeight = Mathf.Max(1, value);
        }
    }
}
