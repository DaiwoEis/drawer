using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Features.Drawing.Service;
using Features.Drawing.Domain.Entity;
using Features.Drawing.Domain.ValueObject;
using Common.Constants;

namespace Tests.Service
{
    public class StrokeCollisionServiceTests
    {
        private StrokeCollisionService _service;

        [SetUp]
        public void Setup()
        {
            _service = new StrokeCollisionService();
            _service.SetLogicToWorldRatio(1.0f); // Simplify math
        }

        [Test]
        public void IsEraserStrokeEffective_ShouldReturnFalse_WhenNoInk()
        {
            // Arrange
            var eraser = CreateStroke(1, DrawingConstants.ERASER_BRUSH_ID, new List<LogicPoint> { new LogicPoint(100, 100, 128) }, 10);
            var activeIds = new HashSet<string>();

            // Act
            bool result = _service.IsEraserStrokeEffective(eraser, activeIds);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void IsEraserStrokeEffective_ShouldReturnTrue_WhenTouchingInk()
        {
            // Arrange
            // Ink at (100, 100)
            var ink = CreateStroke(1, 1, new List<LogicPoint> { new LogicPoint(100, 100, 128), new LogicPoint(110, 110, 128) }, 10);
            _service.Insert(ink);
            var activeIds = new HashSet<string> { "1" };

            // Eraser at (105, 105) - Should intersect
            var eraser = CreateStroke(2, DrawingConstants.ERASER_BRUSH_ID, new List<LogicPoint> { new LogicPoint(105, 105, 128) }, 10);

            // Act
            bool result = _service.IsEraserStrokeEffective(eraser, activeIds);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void IsEraserStrokeEffective_ShouldReturnTrue_EvenIfTouchingInkIsObscured()
        {
            // FIX: We changed the behavior to ALWAYS return true if it touches ink,
            // regardless of whether that ink is technically covered by another eraser.
            // This is to prevent false negatives where the "point-based" obscurity check
            // fails to account for eraser radius/edges.
            
            // Arrange
            // Ink (ID 1, Seq 1)
            var ink = CreateStroke(1, 1, new List<LogicPoint> { new LogicPoint(100, 100, 128) }, 10, 1);
            _service.Insert(ink);

            // Previous Eraser (ID 2, Seq 2) covering the ink
            var prevEraser = CreateStroke(2, DrawingConstants.ERASER_BRUSH_ID, new List<LogicPoint> { new LogicPoint(100, 100, 128) }, 20, 2);
            _service.Insert(prevEraser);

            var activeIds = new HashSet<string> { "1", "2" };

            // New Eraser (ID 3, Seq 3) trying to erase the same spot
            var newEraser = CreateStroke(3, DrawingConstants.ERASER_BRUSH_ID, new List<LogicPoint> { new LogicPoint(100, 100, 128) }, 10, 3);

            // Act
            bool result = _service.IsEraserStrokeEffective(newEraser, activeIds);

            // Assert
            Assert.IsTrue(result, "Should be True because we removed the aggressive obscurity optimization");
        }

        [Test]
        public void IsEraserStrokeEffective_ShouldCatchFastStroke_WithSparsePoints()
        {
            // Test for Stride Optimization Bug (User Issue)
            // Scenario: Eraser has points P0..P10. Ink is only near P3.
            // If Stride is 5 (checking P0, P5, P10), it will miss P3.
            
            // Arrange
            // Ink at (100, 100)
            var ink = CreateStroke(1, 1, new List<LogicPoint> { new LogicPoint(100, 100, 128) }, 10, 1);
            _service.Insert(ink);
            var activeIds = new HashSet<string> { "1" };

            // Eraser points. P3 is at (100,100). Others are far away.
            var points = new List<LogicPoint>();
            for (int i = 0; i <= 10; i++)
            {
                if (i == 3) points.Add(new LogicPoint(100, 100, 128)); // Hit
                else points.Add(new LogicPoint((ushort)(200 + i * 10), 200, 128)); // Miss
            }
            
            var eraser = CreateStroke(2, DrawingConstants.ERASER_BRUSH_ID, points, 10, 2);

            // Act
            bool result = _service.IsEraserStrokeEffective(eraser, activeIds);

            // Assert
            Assert.IsTrue(result, "Eraser should detect ink even if the hit is at index 3 (skipped by stride 5)");
        }

        private StrokeEntity CreateStroke(uint id, ushort brushId, List<LogicPoint> points, float size, long seq = 0)
        {
            var stroke = new StrokeEntity(id, 0, brushId, 0, 0, size, seq);
            stroke.AddPoints(points);
            stroke.EndStroke();
            return stroke;
        }
    }
}
