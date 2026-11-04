using ChessBoss.Core;
using NUnit.Framework;

namespace ChessBoss.Tests
{
    public class CellTests
    {
        [Test]
        public void Cell_KeepsCoordinates()
        {
            var cell = new Cell(1, 2);

            Assert.AreEqual(1, cell.X);
            Assert.AreEqual(2, cell.Y);
        }
    }
}
