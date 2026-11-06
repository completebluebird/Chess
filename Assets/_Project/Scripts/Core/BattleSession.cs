namespace ChessBoss.Core
{
    public enum BattleOutcome
    {
        Fighting,
        Victory,
        Defeat,
    }

    /// <summary>How the fight ended, how long it took, and when to show the result. The boss falling comes first.</summary>
    public sealed class BattleSession
    {
        readonly Health _boss;
        readonly Health _player;
        readonly float _resultDelay;
        float _sinceEnd;

        public BattleOutcome Outcome { get; private set; }
        public float Elapsed { get; private set; }
        public bool ResultReady => Outcome != BattleOutcome.Fighting && _sinceEnd >= _resultDelay;

        public BattleSession(Health boss, Health player, float resultDelay)
        {
            _boss = boss;
            _player = player;
            _resultDelay = resultDelay;
        }

        public void Tick(float deltaTime)
        {
            if (Outcome != BattleOutcome.Fighting)
            {
                _sinceEnd += deltaTime;
                return;
            }

            if (_boss.IsDead) Outcome = BattleOutcome.Victory;
            else if (_player.IsDead) Outcome = BattleOutcome.Defeat;
            else Elapsed += deltaTime;
        }
    }
}
