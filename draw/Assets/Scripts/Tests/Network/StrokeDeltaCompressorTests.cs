using NUnit.Framework;
using System.Collections.Generic;
using Features.Drawing.Domain.ValueObject;
using Features.Drawing.Service.Network;

namespace Tests.Network
{
    public class StrokeDeltaCompressorTests
    {
        [Test]
        public void Compress_And_Decompress_ShouldMatchOriginal()
        {
            // Arrange
            var origin = new LogicPoint(100, 100, 128);
            var points = new List<LogicPoint>
            {
                new LogicPoint(105, 105, 130), // Small delta (+5, +5)
                new LogicPoint(200, 200, 140), // Large delta (Force escape)
                new LogicPoint(202, 202, 140)  // Small delta again
            };

            var buffer = new byte[1024];
            
            // Act
            int bytesWritten = StrokeDeltaCompressor.Compress(origin, points, buffer);
            
            // Assert Compression
            Assert.Greater(bytesWritten, 0);
            
            // Act Decompress
            var resultList = new List<LogicPoint>();
            // Use subarray to simulate network packet
            var packet = new byte[bytesWritten];
            System.Buffer.BlockCopy(buffer, 0, packet, 0, bytesWritten);
            
            StrokeDeltaCompressor.Decompress(origin, packet, resultList);

            // Assert
            Assert.AreEqual(points.Count, resultList.Count);
            
            for (int i = 0; i < points.Count; i++)
            {
                Assert.AreEqual(points[i].X, resultList[i].X, $"Point {i} X mismatch");
                Assert.AreEqual(points[i].Y, resultList[i].Y, $"Point {i} Y mismatch");
                Assert.AreEqual(points[i].Pressure, resultList[i].Pressure, $"Point {i} Pressure mismatch");
            }
        }
        
        [Test]
        public void Compress_Handles_NegativeDeltas()
        {
            // Arrange
            var origin = new LogicPoint(100, 100, 128);
            var points = new List<LogicPoint>
            {
                new LogicPoint(90, 90, 120), // -10, -10
            };
            var buffer = new byte[1024];

            // Act
            int bytes = StrokeDeltaCompressor.Compress(origin, points, buffer);
            var resultList = new List<LogicPoint>();
            StrokeDeltaCompressor.Decompress(origin, buffer[..bytes], resultList);

            // Assert
            Assert.AreEqual(90, resultList[0].X);
            Assert.AreEqual(90, resultList[0].Y);
        }

        [Test]
        public void Compress_Handles_BufferBoundaries()
        {
             // Test overflow protection logic
             var origin = new LogicPoint(0,0,0);
             var points = new List<LogicPoint>();
             for(int i=0; i<100; i++) points.Add(new LogicPoint((ushort)i, (ushort)i, 0));
             
             var smallBuffer = new byte[10]; // Too small for 100 points
             
             int bytes = StrokeDeltaCompressor.Compress(origin, points, smallBuffer);
             
             Assert.LessOrEqual(bytes, 10);
             // Should have written at least 1 point (3 bytes)
             Assert.Greater(bytes, 0);
        }
    }
}
