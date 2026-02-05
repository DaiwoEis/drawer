using System.Collections.Generic;
using UnityEngine;
using Features.Drawing.Domain.Entity;
using Features.Drawing.Domain.Algorithm;
using Features.Drawing.Domain.ValueObject;
using Common.Constants;

namespace Features.Drawing.Service
{
    public class StrokeCollisionService
    {
        private readonly StrokeSpatialIndex _spatialIndex;

        // Dynamic ratio, initialized to default but updateable via SetLogicToWorldRatio
        private float _logicToWorldRatio = DrawingConstants.LOGIC_TO_WORLD_RATIO;

        public StrokeCollisionService()
        {
            _spatialIndex = new StrokeSpatialIndex();
        }

        public void SetLogicToWorldRatio(float ratio)
        {
            _logicToWorldRatio = ratio;
        }

        public void Insert(StrokeEntity stroke)
        {
            _spatialIndex.Insert(stroke);
        }

        public void Clear()
        {
            _spatialIndex.Clear();
        }

        /// <summary>
        /// Checks if an eraser stroke actually intersects with any active ink strokes.
        /// Returns true if it touches at least one ink stroke that is not covered by a later eraser.
        /// </summary>
        public bool IsEraserStrokeEffective(StrokeEntity eraserStroke, HashSet<string> activeStrokeIds)
        {
            if (eraserStroke == null || eraserStroke.Points.Count == 0) return false;

            Rect bounds = CalculateStrokeBounds(eraserStroke);
            var candidates = _spatialIndex.Query(bounds);

            // Separate candidates into Inks and Erasers
            var inks = new List<StrokeEntity>();
            var erasers = new List<StrokeEntity>();

            foreach (var s in candidates)
            {
                if (!activeStrokeIds.Contains(s.Id.ToString())) continue;

                if (s.BrushId == DrawingConstants.ERASER_BRUSH_ID)
                {
                    erasers.Add(s);
                }
                else
                {
                    inks.Add(s);
                }
            }

            // If no ink at all, discard immediately
            if (inks.Count == 0)
            {
                return false;
            }

            float scale = _logicToWorldRatio;
            float eraserRadius = eraserStroke.Size * 0.5f;

            // Iterate over sample points of the current eraser
            int stride = 1; // FIX: Check every point to avoid false negatives (eraser visual but not logical)
            for (int i = 0; i < eraserStroke.Points.Count; i += stride)
            {
                var p = eraserStroke.Points[i];

                // Check against inks
                foreach (var ink in inks)
                {
                    // 1. Check distance to ink (using the relaxed threshold)
                    float inkThreshold = (eraserRadius + ink.Size * 0.5f) * scale * 1.2f;
                    float distToInkSqr = SqrDistancePointStroke(p, ink);

                    if (distToInkSqr < inkThreshold * inkThreshold)
                    {
                        // This point touches this ink.
                        // FIX: Removed obscurity check. 
                        // The previous logic checked if the eraser center point was covered by a newer eraser.
                        // However, this caused false positives (discarding valid strokes) because:
                        // 1. The eraser has a radius, so even if the center is covered, the edges might hit ink.
                        // 2. Visual rendering is continuous, but logic is discrete points.
                        // It is safer to allow potentially redundant eraser strokes than to discard valid ones (which breaks Undo).
                        return true;
                    }
                }
            }

            return false;
        }

        private float SqrDistancePointStroke(LogicPoint pE, StrokeEntity candidateStroke)
        {
            // Convert LogicPoint to Vector2 for math
            Vector2 vE = new Vector2(pE.X, pE.Y);

            float minSqrDist = float.MaxValue;
            var candidatePoints = candidateStroke.Points;

            for (int j = 0; j < candidatePoints.Count - 1; j++)
            {
                var pA = candidatePoints[j];
                var pB = candidatePoints[j + 1];
                Vector2 vA = new Vector2(pA.X, pA.Y);
                Vector2 vB = new Vector2(pB.X, pB.Y);

                float sqrDist = SqrDistancePointSegment(vE, vA, vB);
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                }
            }
            return minSqrDist;
        }

        private float SqrDistancePointSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            Vector2 ap = p - a;
            float t = Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab);
            t = Mathf.Clamp01(t);
            Vector2 closest = a + ab * t;
            return (p - closest).sqrMagnitude;
        }

        private Rect CalculateStrokeBounds(StrokeEntity stroke)
        {
            if (stroke.Points.Count == 0) return Rect.zero;

            ushort minX = ushort.MaxValue;
            ushort minY = ushort.MaxValue;
            ushort maxX = ushort.MinValue;
            ushort maxY = ushort.MinValue;

            foreach (var p in stroke.Points)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }

            // Return with some padding? LogicPoint is 0-65535.
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
