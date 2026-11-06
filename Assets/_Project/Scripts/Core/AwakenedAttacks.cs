using System.Collections.Generic;
using System.Linq;

namespace ChessBoss.Core
{
    public enum BishopMove
    {
        ForwardRight,
        ForwardLeft,
        BackRight,
        BackLeft,
    }

    public sealed class AwakenSettings
    {
        public int BaseDamage;
        public int DamagePerRing;
        public float AttackCooldown;
        public float CooldownPerRing;
        public int RookRingHitDamage;
        public float LandingInvulnerable;
    }

    /// <summary>What an awakened attack did. Path starts where the pawn stood and ends where it stands now.</summary>
    public sealed class AwakenedStrike
    {
        public bool Succeeded { get; }
        public IReadOnlyList<Cell> Path { get; }
        public int RingsMoved { get; }

        public AwakenedStrike(bool succeeded, IReadOnlyList<Cell> path, int ringsMoved)
        {
            Succeeded = succeeded;
            Path = path;
            RingsMoved = ringsMoved;
        }
    }

    /// <summary>The one attack an awakened piece gets before turning back into a pawn. It resolves at once; views animate the path.</summary>
    public sealed class AwakenedAttacks
    {
        readonly PawnMovement _pawn;
        readonly Health _boss;
        readonly PlayerVitals _vitals;
        readonly PawnActions _actions;
        readonly Awakening _awakening;
        readonly AwakenSettings _settings;

        public AwakenedAttacks(PawnMovement pawn, Health boss, PlayerVitals vitals, PawnActions actions, Awakening awakening, AwakenSettings settings)
        {
            _pawn = pawn;
            _boss = boss;
            _vitals = vitals;
            _actions = actions;
            _awakening = awakening;
            _settings = settings;
            _vitals.Hurt += _awakening.End;
        }

        /// <summary>Null when no rook is waiting or the shared cooldown still runs, so the key press did nothing.</summary>
        public AwakenedStrike Rook(PawnMove direction)
        {
            if (_awakening.Active != AwakenPiece.Rook || !_actions.IsReady) return null;

            _awakening.End();
            var start = _pawn.Position;
            if (direction == PawnMove.Forward || direction == PawnMove.Back)
            {
                var line = RookLine(start, direction == PawnMove.Back);
                var moved = line.Count - 1;
                return Finish(line, moved, _settings.BaseDamage + moved * _settings.DamagePerRing, moved);
            }

            if (start.Y != Board.BossRing + 1)
            {
                _actions.StartCooldown(_settings.AttackCooldown);
                return new AwakenedStrike(false, new[] { start }, 0);
            }

            var board = _pawn.Board;
            var way = direction == PawnMove.Left ? -1 : 1;
            var ring = Enumerable.Range(0, board.Sectors + 1).Select(i => board.Step(start, way * i, 0)).ToArray();
            return Finish(ring, 0, board.Sectors * _settings.RookRingHitDamage, 0);
        }

        /// <summary>Null when no bishop is waiting or the shared cooldown still runs, so the keys did nothing.</summary>
        public AwakenedStrike Bishop(BishopMove move)
        {
            if (_awakening.Active != AwakenPiece.Bishop || !_actions.IsReady) return null;

            _awakening.End();
            var way = move == BishopMove.ForwardRight || move == BishopMove.BackRight ? 1 : -1;
            var path = BishopLine(_pawn.Position, way, move == BishopMove.BackRight || move == BishopMove.BackLeft);
            var moved = path.Count - 1;

            // The beam ends on the boss one sector past the last cell. Turned 90 degrees right, every diagonal points
            // one sector toward D; the ring part is dropped, because the landing stays on the ring before the boss.
            path.Add(_pawn.Board.Step(path[moved], way + 1, 0));
            return Finish(path, moved, _settings.BaseDamage + moved * _settings.DamagePerRing, moved);
        }

        // Keeps going the same way around the board, bouncing off the outer edge when sent backward.
        List<Cell> BishopLine(Cell start, int way, bool backFirst)
        {
            var board = _pawn.Board;
            var line = new List<Cell> { start };
            if (backFirst)
                while (line[line.Count - 1].Y < board.Rings - 1)
                    line.Add(board.Step(line[line.Count - 1], way, 1));

            while (line[line.Count - 1].Y > Board.BossRing + 1)
                line.Add(board.Step(line[line.Count - 1], way, -1));

            return line;
        }

        // Straight along the sector to the ring before the boss, going out to the board's edge first when sent backward.
        List<Cell> RookLine(Cell start, bool backFirst)
        {
            var line = new List<Cell> { start };
            if (backFirst)
                for (var y = start.Y + 1; y < _pawn.Board.Rings; y++)
                    line.Add(new Cell(start.X, y));

            for (var y = line[line.Count - 1].Y - 1; y > Board.BossRing; y--)
                line.Add(new Cell(start.X, y));

            return line;
        }

        AwakenedStrike Finish(IReadOnlyList<Cell> path, int ringsMoved, int damage, int cooldownRings)
        {
            _pawn.Land(path[path.Count - 1]);
            _vitals.GrantInvulnerability(_settings.LandingInvulnerable);
            _boss.TakeDamage(damage);
            _actions.StartCooldown(_settings.AttackCooldown + cooldownRings * _settings.CooldownPerRing);
            return new AwakenedStrike(true, path, ringsMoved);
        }
    }
}
