using UnityEngine;

namespace ChessBoss.Game
{
    /// <summary>Values from docs/design/tuning.md. They live in the BattleTuning asset, not in code.</summary>
    [CreateAssetMenu(menuName = "ChessBoss/Battle Tuning")]
    public sealed class BattleTuning : ScriptableObject
    {
        [Header("Board")]
        public int boardSectors;
        [Min(4)] public int boardRings;
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
        public int laserWeight;
        public int meteorWeight;
        public int slowBreathWeight;
        public int biteWeight;
        public float meteorWarning;
        public int meteorRadius;
        public float biteWarning;
        public int biteDepth;
        public int biteHalfWidth;
        public float[] stunHealthFractions;
        public float stunDuration;
        public float stunSlowTimeScale;
        public float stunSlowTime;
        public Color bossStunColor;
        public float breathPrep;
        public float breathStepTime;
        public float breathWarning;
        public int breathBeamWidth;
        public int breathWalls;
        public int breathCoverHops;
        public float slowBreathPrep;
        public float slowBreathLapTime;
        public float slamAirTime;
        public float slamRingGap;
        public float slamWarning;
        public float slamJumpHeight;
        [Range(0.01f, 0.5f)] public float slamRiseFraction;
        [Range(0.01f, 0.5f)] public float slamDropFraction;
        public int slamFlaps;
        public float slamFlapDepth;
        public int laserDamage;
        public int meteorDamage;
        public int biteDamage;
        public int breathDamage;
        public int slamDamage;
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

        [Header("Awakening")]
        public float awakenChargeTime;
        public float awakenInputWindow;
        public float awakenTimeScale;
        public float awakenTravelTime;
        public Color awakenedColor;
        public int awakenBaseDamage;
        public int awakenDamagePerRing;
        public float awakenCooldownPerRing;
        public float rookRingHitDamage;
        public float bishopInputBuffer;
        public int knightDamage;
        public int knightAttackRadius;
        public float landingInvulnerable;

        [Header("Flow")]
        public float battleResultDelay;
    }
}
