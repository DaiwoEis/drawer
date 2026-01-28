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
        public void IsEraserStrokeEffective_ShouldReturnFalse_WhenTouchingInk_ButObscuredByLaterEraser()
        {
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
            // Should be false because ink is already covered by Eraser 2 (Seq 2 > Ink Seq 1)
            // Wait, the logic is: Is THIS eraser stroke effective?
            // Yes, if it touches ink that is NOT covered by a *newer* eraser.
            // But here the existing eraser (2) is *older* than the current eraser (3) but *newer* than the ink (1).
            // Logic in Service:
            // "Check if point touches ink... then check if it is obscured by any EXISTING eraser that is NEWER than the ink."
            // Existing eraser 2 is newer than ink 1. So ink 1 is obscured at this point.
            // So current eraser 3 should be redundant (effective=false).
            
            bool result = _service.IsEraserStrokeEffective(newEraser, activeIds);

            // Assert
            Assert.IsFalse(result);
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
