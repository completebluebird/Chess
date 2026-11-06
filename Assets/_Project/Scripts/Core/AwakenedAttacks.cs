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
        public int KnightDamage;
        public int KnightRadius;
        public float LandingInvulnerable;
    }

    /// <summary>What an awakened attack did. Path starts where the pawn stood and ends where it stands now.</summary>
    public sealed class AwakenedStrike
    {
        public bool Succeeded { get; }
        public IReadOnlyList<Cell> Path { get; }
        public int RingsMoved { get; }
        public int Damage { get; }

        public AwakenedStrike(bool succeeded, IReadOnlyList<Cell> path, int ringsMoved, int damage)
        {
            Damage = damage;
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
        PawnMove? _knightFirst;

        public AwakenedAttacks(PawnMovement pawn, Health boss, PlayerVitals vitals, PawnActions actions, Awakening awakening, AwakenSettings settings)
        {
            _pawn = pawn;
            _boss = boss;
            _vitals = vitals;
            _actions = actions;
            _awakening = awakening;
            _settings = settings;
            _vitals.Hurt += _awakening.End;
            _awakening.Ended += () => _knightFirst = null;
        }

        /// <summary>The cells the knight's first key leaves to choose from. Off-board ones aren't offered.</summary>
        public IReadOnlyList<Cell> KnightCandidates => _knightFirst == null ? new Cell[0] : CandidatesOf(_knightFirst.Value);

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
                return new AwakenedStrike(false, new[] { start }, 0, 0);
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

        /// <summary>Feed it each key. Null until a key completes a jump: a key that makes no valid choice changes nothing.</summary>
        public AwakenedStrike Knight(PawnMove key)
        {
            if (_awakening.Active != AwakenPiece.Knight) return null;

            if (_knightFirst == null)
            {
                // A key with nothing to choose from would lock the piece until its time ran out.
                if (CandidatesOf(key).Count > 0) _knightFirst = key;
                return null;
            }

            if (IsSideways(key) == IsSideways(_knightFirst.Value) || !_actions.IsReady) return null;

            var board = _pawn.Board;
            var target = KnightLanding(_knightFirst.Value, key);
            if (!board.Contains(target)) return null;

            _awakening.End();
            var start = _pawn.Position;
            var landsOnBoss = board.IsBossCell(target);
            var landing = landsOnBoss ? start : board.NearestWalkable(target);

            // The boss fills its whole ring, so an area around the landing reaches it exactly when the ring is that close.
            var damage = landing.Y <= _settings.KnightRadius || landsOnBoss ? _settings.KnightDamage : 0;
            return Finish(landsOnBoss ? new[] { start, target, start } : new[] { start, landing }, 0, damage, 0);
        }

        List<Cell> CandidatesOf(PawnMove first)
        {
            var sides = IsSideways(first) ? new[] { PawnMove.Forward, PawnMove.Back } : new[] { PawnMove.Left, PawnMove.Right };
            return sides.Select(side => KnightLanding(first, side)).Where(_pawn.Board.Contains).ToList();
        }

        Cell KnightLanding(PawnMove first, PawnMove second)
        {
            var (firstSectors, firstRings) = Offset(first);
            var (secondSectors, secondRings) = Offset(second);
            return _pawn.Board.Step(_pawn.Position, firstSectors * 2 + secondSectors, firstRings * 2 + secondRings);
        }

        static (int sectors, int rings) Offset(PawnMove key)
        {
            switch (key)
            {
                case PawnMove.Forward: return (0, -1);
                case PawnMove.Back: return (0, 1);
                case PawnMove.Left: return (-1, 0);
                default: return (1, 0);
            }
        }

        static bool IsSideways(PawnMove key) => key == PawnMove.Left || key == PawnMove.Right;

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
            return new AwakenedStrike(true, path, ringsMoved, damage);
        }
    }
}
