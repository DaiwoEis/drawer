using System.Collections.Generic;
using UnityEngine;
using Features.Drawing.Domain.ValueObject;

namespace Features.Drawing.Service
{
    /// <summary>
    /// Responsible for smoothing raw input points into high-quality curves.
    /// Uses Catmull-Rom spline interpolation.
    /// </summary>
    public class StrokeSmoothingService
    {
        private const int MinStepsPerSegment = 1;
        private const int MaxStepsPerSegment = 8;
        private const float StepsPerNormalizedUnit = 64f;

        private readonly List<LogicPoint> _controlPoints = new List<LogicPoint>(8);
        private readonly List<LogicPoint> _outputBuffer = new List<LogicPoint>(64);

        public List<LogicPoint> ControlPoints => _controlPoints;
        public List<LogicPoint> OutputBuffer => _outputBuffer;

        /// <summary>
        /// Generates smoothed LogicPoints from the internal control points buffer.
        /// Writes result into the internal output buffer to avoid GC.
        /// </summary>
        public void SmoothPoints()
        {
            _outputBuffer.Clear();

            if (_controlPoints.Count < 3)
            {
                _outputBuffer.AddRange(_controlPoints);
                return;
            }
            
            // We need at least 4 points for Catmull-Rom. 
            // If less, we might need to duplicate start/end points.
            // Simplified logic here for MVP.
            
            for (int i = 0; i < _controlPoints.Count - 3; i++)
            {
                LogicPoint p0 = _controlPoints[i];
                LogicPoint p1 = _controlPoints[i + 1];
                LogicPoint p2 = _controlPoints[i + 2];
                LogicPoint p3 = _controlPoints[i + 3];

                int steps = GetSteps(p1, p2);
                for (int t = 0; t < steps; t++)
                {
                    float tNorm = t / (float)steps;
                    LogicPoint interpolated = CatmullRom(p0, p1, p2, p3, tNorm);
                    _outputBuffer.Add(interpolated);
                }
            }
        }

        private LogicPoint CatmullRom(LogicPoint p0, LogicPoint p1, LogicPoint p2, LogicPoint p3, float t)
        {
            // Convert to float for calculation
            Vector2 v0 = p0.ToNormalized();
            Vector2 v1 = p1.ToNormalized();
            Vector2 v2 = p2.ToNormalized();
            Vector2 v3 = p3.ToNormalized();

            float t2 = t * t;
            float t3 = t2 * t;

            Vector2 pos = 0.5f * (
                (2f * v1) +
                (-v0 + v2) * t +
                (2f * v0 - 5f * v1 + 4f * v2 - v3) * t2 +
                (-v0 + 3f * v1 - 3f * v2 + v3) * t3
            );

            // Interpolate pressure linearly
            float pressure = Mathf.Lerp(p1.GetNormalizedPressure(), p2.GetNormalizedPressure(), t);

            return LogicPoint.FromNormalized(pos, pressure);
        }

        private int GetSteps(LogicPoint a, LogicPoint b)
        {
            Vector2 v1 = a.ToNormalized();
            Vector2 v2 = b.ToNormalized();
            float dist = Vector2.Distance(v1, v2);
            int steps = Mathf.CeilToInt(dist * StepsPerNormalizedUnit);
            return Mathf.Clamp(steps, MinStepsPerSegment, MaxStepsPerSegment);
        }
    }
}
