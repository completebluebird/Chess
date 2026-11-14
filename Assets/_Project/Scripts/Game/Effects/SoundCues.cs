namespace ChessBoss.Game
{
    /// <summary>
    /// Names of the moments that make a sound. BattleEffects.Sound raises them; nothing plays yet.
    /// shortcut: no audio behind these, the M9 sound pass subscribes to BattleEffects.SoundCued and plays clips.
    /// </summary>
    public static class SoundCues
    {
        public const string Slash = "slash";
        public const string Awaken = "awaken";
        public const string AwakenedStrike = "awakened-strike";
        public const string Hurt = "hurt";
        public const string Bite = "bite";
        public const string Impact = "impact";
    }
}
