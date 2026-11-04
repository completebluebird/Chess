using System;
using System.Collections.Generic;
using System.Linq;
using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class AttackTimelineTests
    {
        static readonly Cell A = new Cell(0, 1);
        static readonly Cell B = new Cell(1, 1);

        static AttackStep Step(float warningStart, float strikeTime, params Cell[] cells) =>
            new AttackStep(cells, warningStart, strikeTime);

        [TestCase(1f, 1f)]
        [TestCase(2f, 1f)]
        public void Step_WithoutWarningTime_Throws(float warningStart, float strikeTime)
        {
            Assert.Throws<ArgumentException>(() => Step(warningStart, strikeTime, A));
        }

        [Test]
        public void Step_WithoutCells_Throws()
        {
            Assert.Throws<ArgumentException>(() => Step(0, 1));
        }

        [Test]
        public void Warning_ShowsOnlyBetweenWarningStartAndStrike()
        {
            var timeline = new AttackTimeline();
            timeline.Start(new[] { Step(1, 2, A) });

            CollectionAssert.IsEmpty(timeline.WarningCells);
            timeline.Tick(1);
            CollectionAssert.AreEqual(new[] { A }, timeline.WarningCells);
            timeline.Tick(1);
            CollectionAssert.IsEmpty(timeline.WarningCells);
        }

        [Test]
        public void WarningProgress_RisesFromZeroToOne()
        {
            var timeline = new AttackTimeline();
            timeline.Start(new[] { Step(1, 3, A) });

            Assert.AreEqual(0f, timeline.WarningProgress(A));
            timeline.Tick(2);
            Assert.AreEqual(0.5f, timeline.WarningProgress(A), 1e-5f);
            Assert.AreEqual(0f, timeline.WarningProgress(B));
        }

        [Test]
        public void WarningProgress_UsesTheStepClosestToStriking()
        {
            var timeline = new AttackTimeline();
            timeline.Start(new[] { Step(0, 2, A), Step(1, 5, A) });

            timeline.Tick(1);

            Assert.AreEqual(0.5f, timeline.WarningProgress(A), 1e-5f);
        }

        [Test]
        public void Strike_FiresOnceWithTheWarnedCells()
        {
            var timeline = new AttackTimeline();
            var strikes = new List<IReadOnlyList<Cell>>();
            timeline.Struck += strikes.Add;
            timeline.Start(new[] { Step(0, 1, A, B) });

            timeline.Tick(0.5f);
            CollectionAssert.IsEmpty(strikes);
            var warned = timeline.WarningCells.ToArray();

            timeline.Tick(0.5f);
            timeline.Tick(10);

            Assert.AreEqual(1, strikes.Count);
            CollectionAssert.AreEqual(warned, strikes[0]);
            Assert.IsFalse(timeline.IsRunning);
        }

        [Test]
        public void LargeTick_FiresEveryPassedStepInStrikeOrder()
        {
            var timeline = new AttackTimeline();
            var strikes = new List<Cell>();
            timeline.Struck += cells => strikes.Add(cells[0]);
            timeline.Start(new[] { Step(1, 3, B), Step(0, 1, A) });

            timeline.Tick(10);

            CollectionAssert.AreEqual(new[] { A, B }, strikes);
        }

        [Test]
        public void Start_WhileRunning_Throws()
        {
            var timeline = new AttackTimeline();
            timeline.Start(new[] { Step(0, 1, A) });

            Assert.Throws<InvalidOperationException>(() => timeline.Start(new[] { Step(0, 1, B) }));
        }

        [Test]
        public void Stop_ClearsWarningsWithoutStriking()
        {
            var timeline = new AttackTimeline();
            var strikes = 0;
            timeline.Struck += _ => strikes++;
            timeline.Start(new[] { Step(0, 1, A) });
            timeline.Tick(0.5f);

            timeline.Stop();
            timeline.Tick(10);

            CollectionAssert.IsEmpty(timeline.WarningCells);
            Assert.AreEqual(0, strikes);
            Assert.IsFalse(timeline.IsRunning);
        }
    }
}
