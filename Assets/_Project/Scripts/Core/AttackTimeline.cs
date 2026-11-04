using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessBoss.Core
{
    /// <summary>Which cells to strike and when. A strike can't exist without time to warn the player first.</summary>
    public sealed class AttackStep
    {
        public IReadOnlyList<Cell> Cells { get; }
        public float WarningStart { get; }
        public float StrikeTime { get; }

        public AttackStep(IReadOnlyList<Cell> cells, float warningStart, float strikeTime)
        {
            if (cells.Count == 0)
                throw new ArgumentException("An attack step needs at least one cell.", nameof(cells));
            if (strikeTime <= warningStart)
                throw new ArgumentException($"Strike at {strikeTime} leaves no warning time after {warningStart}.", nameof(strikeTime));

            Cells = cells;
            WarningStart = warningStart;
            StrikeTime = strikeTime;
        }
    }

    /// <summary>The shared warning-then-strike flow that every attack pattern runs through.</summary>
    public sealed class AttackTimeline
    {
        readonly List<AttackStep> _steps = new List<AttackStep>();
        float _time;
        int _next;

        public event Action<IReadOnlyList<Cell>> Struck;

        public bool IsRunning => _next < _steps.Count;

        public IEnumerable<Cell> WarningCells => ActiveSteps.SelectMany(step => step.Cells);

        IEnumerable<AttackStep> ActiveSteps => _steps.Skip(_next).Where(step => step.WarningStart <= _time);

        public void Start(IEnumerable<AttackStep> pattern)
        {
            if (IsRunning) throw new InvalidOperationException("An attack is already running.");

            Stop();
            _steps.AddRange(pattern.OrderBy(step => step.StrikeTime));
        }

        public void Stop()
        {
            _steps.Clear();
            _time = 0;
            _next = 0;
        }

        public void Tick(float deltaTime)
        {
            _time += deltaTime;
            while (IsRunning && _steps[_next].StrikeTime <= _time)
            {
                // Advance outside Invoke: ?. skips its arguments when nobody is subscribed.
                var step = _steps[_next++];
                Struck?.Invoke(step.Cells);
            }
        }

        /// <summary>0 when the warning appears, rising to 1 at the strike. A cell in several steps follows the most urgent one.</summary>
        public float WarningProgress(Cell cell)
        {
            var progress = 0f;
            foreach (var step in ActiveSteps)
                if (step.Cells.Contains(cell))
                    progress = Math.Max(progress, (_time - step.WarningStart) / (step.StrikeTime - step.WarningStart));
            return progress;
        }
    }
}
