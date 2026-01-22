using System.Collections.Generic;
using UnityEngine;
using Features.Drawing.Domain.Entity;
using Common.Constants;

namespace Features.Drawing.Domain.Algorithm
{
    public class StrokeSpatialIndex
    {
        private readonly QuadTree<StrokeEntity> _tree;
        
        // Bounds for the entire logical space (0-65535)
        private static readonly Rect WorldBounds = new Rect(0, 0, DrawingConstants.LOGICAL_RESOLUTION, DrawingConstants.LOGICAL_RESOLUTION);

        public StrokeSpatialIndex()
        {
            _tree = new QuadTree<StrokeEntity>(WorldBounds);
        }

        public void Insert(StrokeEntity stroke)
        {
            if (stroke == null || stroke.Points == null || stroke.Points.Count == 0) return;

            Rect bounds = CalculateBounds(stroke);
            // Inflate bounds slightly to account for brush thickness?
            // LogicPoint doesn't have thickness info directly (only pressure).
            // Brush size is in UI pixels, mapping to logical space depends on zoom/pan (which is View).
            // But LogicPoint pressure is 0-255.
            // Let's just index the center-line bounds for now. 
            // Querying should account for thickness.
            
            _tree.Insert(stroke, bounds);
        }

        public HashSet<StrokeEntity> Query(Rect area)
        {
            var results = new HashSet<StrokeEntity>();
            _tree.Query(area, results);
            return results;
        }
        
        public void Clear()
        {
            _tree.Clear();
        }

        private Rect CalculateBounds(StrokeEntity stroke)
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

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
