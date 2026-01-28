using NUnit.Framework;
using System.Collections.Generic;
using Features.Drawing.Domain.ValueObject;
using Features.Drawing.Service.Network;

namespace Tests.Network
{
    public class StrokeChecksumTests
    {
        [Test]
        public void ComputeStrokeChecksum_IsDeterministic()
        {
            var points = new List<LogicPoint>
            {
                new LogicPoint(1, 2, 3),
                new LogicPoint(10, 20, 30),
                new LogicPoint(65535, 65535, 255)
            };

            uint a = DrawingNetworkService.ComputeStrokeChecksum(points);
            uint b = DrawingNetworkService.ComputeStrokeChecksum(points);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void ComputeStrokeChecksum_ChangesWithData()
        {
            var pointsA = new List<LogicPoint>
            {
                new LogicPoint(1, 2, 3),
                new LogicPoint(10, 20, 30)
            };

            var pointsB = new List<LogicPoint>
            {
                new LogicPoint(1, 2, 3),
                new LogicPoint(10, 20, 31)
            };

            uint a = DrawingNetworkService.ComputeStrokeChecksum(pointsA);
            uint b = DrawingNetworkService.ComputeStrokeChecksum(pointsB);

            Assert.AreNotEqual(a, b);
        }
    }
}
