using UnityEngine;

namespace ChessBoss.Game
{
    /// <summary>Values from docs/design/tuning.md. They live in the BattleTuning asset, not in code.</summary>
    [CreateAssetMenu(menuName = "ChessBoss/Battle Tuning")]
    public sealed class BattleTuning : ScriptableObject
    {
        [Header("Board")]
        public int boardSectors;
        public int boardRings;
        public float boardInnerRadius;
        public float cellSize;
        public float tileThickness;
        public float tileScale;
        public Material prototypeMaterial;
        public Color tileLight;
        public Color tileDark;
        public Color attackableColor;
        public Color warningColor;

        [Header("Boss")]
        public int bossHealth;
        public float bossWidth;
        public float bossHeight;
        public Color bossColor;
        public Color bossBaseColor;
        public Color bossFlashColor;
        public float bossFlashDuration;
        public float bossBlinkPeriod;
        public float hitstopDuration;

        [Header("Pawn")]
        public Vector2Int startCell;
        public float moveDuration;
        public float jumpHeight;
        public float squash;
        public Color pawnColor;
        public int attackDamage;
        public float attackCooldown;
    }
}
