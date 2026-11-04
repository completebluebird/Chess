using UnityEngine;

namespace ChessBoss.Game
{
    /// <summary>Values from docs/design/tuning.md. They live in the BattleTuning asset, not in code.</summary>
    [CreateAssetMenu(menuName = "ChessBoss/Battle Tuning")]
    public sealed class BattleTuning : ScriptableObject
    {
        [Header("Board")]
        public int boardSize;
        public int cornerCut;
        public float cellSize;
        public float tileThickness;
        public float tileScale;
        public Material prototypeMaterial;
        public Color tileLight;
        public Color tileDark;

        [Header("Boss")]
        public float bossHeight;
        public Color bossColor;

        [Header("Pawn")]
        public Vector2Int startCell;
        public int forwardHysteresis;
        public float moveDuration;
        public float jumpHeight;
        public float squash;
        public Color pawnColor;
    }
}
