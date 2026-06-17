using UnityEngine;

namespace Windy.Srpg.Game.AI
{
    public sealed class AiDebugInfo
    {
        public AiDebugInfo(string metadata, Color color)
        {
            Metadata = metadata;
            Color = color;
        }

        public string Metadata { get; set; }
        public Color Color { get; set; }
    }
}

