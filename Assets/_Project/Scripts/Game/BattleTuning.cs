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
        public float warningMinStrength;
        public int warningBlinks;
        public float warningBlinkDepth;

        [Header("Boss")]
        public int bossHealth;
        public float bossWidth;
        public float bossHeight;
        public Color bossColor;
        public Color bossBaseColor;
        public float bossTurnSpeed;
        public float bossHeadTurnSpeed;
        public Vector3 bossHeadSize;
        public Color bossHeadColor;
        public Color bossFlashColor;
        public float bossFlashDuration;
        public float bossBlinkPeriod;
        public float hitstopDuration;
        public float attackInterval;
        public float laserWarning;
        public int laserShotsMin;
        public int laserShotsMax;
        public float laserShotGap;
        public int biteWeight;
        public float meteorWarning;
        public int meteorRadius;
        public float biteWarning;
        public int biteDepth;
        public int biteHalfWidth;
        public int breathEvery;
        public float breathPrep;
        public float breathStepTime;
        public float breathWarning;
        public int breathBeamWidth;
        public int breathWalls;
        public int breathCoverHops;
        public int laserDamage;
        public int meteorDamage;
        public int biteDamage;
        public int breathDamage;
        public float wallHeight;
        public Color wallColor;

        [Header("Pawn")]
        public Vector2Int startCell;
        public float moveDuration;
        public float moveCooldown;
        public float jumpHeight;
        public float squash;
        public Color pawnColor;
        public int attackDamage;
        public float attackCooldown;
        public int playerHealth;
        public float invulnerableTime;
        public float burnDuration;
        public float burnTickTime;
        public int burnTickDamage;
    }
}
