using UnityEngine;
using Features.Drawing.Domain;
using Features.Drawing.Domain.ValueObject;
using Features.Drawing.Domain.Data;

namespace Features.Drawing.Service.Input
{
    /// <summary>
    /// Handles raw input processing including stabilization, spacing, and deduplication.
    /// Reduces complexity in the main AppService.
    /// </summary>
    public class StrokeInputProcessor
    {
        private LogicPoint _lastAddedPoint;
        private Vector2 _currentStabilizedPos;

        public void Reset(LogicPoint startPoint)
        {
            _lastAddedPoint = startPoint;
            _currentStabilizedPos = startPoint.ToNormalized();
        }

        public struct ProcessResult
        {
            public bool ShouldAdd;
            public LogicPoint PointToAdd;
        }

        public ProcessResult Process(
            LogicPoint inputPoint, 
            bool isEraser, 
            float currentSize, 
            BrushStrategy strategy, 
            float logicToWorldRatio)
        {
            // 1. Eraser Deduplication
            if (isEraser)
            {
                float scale = logicToWorldRatio;
                float threshold = (currentSize * 0.1f) * scale;
                float sqrDist = LogicPoint.SqrDistance(_lastAddedPoint, inputPoint);
                
                if (sqrDist < threshold * threshold)
                {
                    return new ProcessResult { ShouldAdd = false };
                }
            }

            LogicPoint pointToAdd = inputPoint;

            // 2. Stabilization (Anti-Shake)
            if (!isEraser && strategy != null && strategy.StabilizationFactor > 0.001f)
            {
                Vector2 target = inputPoint.ToNormalized();
                float dist = Vector2.Distance(target, _currentStabilizedPos);
                
                const float MIN_SPEED_THRESHOLD = 0.002f; 
                const float MAX_SPEED_THRESHOLD = 0.05f;

                float speedT = Mathf.InverseLerp(MIN_SPEED_THRESHOLD, MAX_SPEED_THRESHOLD, dist);
                float dynamicFactor = Mathf.Lerp(strategy.StabilizationFactor, strategy.StabilizationFactor * 0.2f, speedT);
                
                float pressure = Mathf.Clamp01(inputPoint.GetNormalizedPressure());
                float pressureWeight = Mathf.Lerp(1.1f, 0.7f, pressure);
                dynamicFactor *= pressureWeight;
                
                float t = Mathf.Clamp01(1.0f - dynamicFactor);
                _currentStabilizedPos = Vector2.Lerp(_currentStabilizedPos, target, t);
                
                pointToAdd = LogicPoint.FromNormalized(_currentStabilizedPos, inputPoint.GetNormalizedPressure());
            }
            else
            {
                _currentStabilizedPos = inputPoint.ToNormalized();
            }

            // 3. Spacing Check (Only for Brushes)
            if (!isEraser)
            {
                float spacingRatio = strategy != null ? strategy.SpacingRatio : 0.15f;
                float minPixelSpacing = currentSize * spacingRatio;
                if (minPixelSpacing < 1f) minPixelSpacing = 1f;
                float minLogical = minPixelSpacing * logicToWorldRatio;
                
                float sqrDist = LogicPoint.SqrDistance(_lastAddedPoint, pointToAdd);
                if (sqrDist < minLogical * minLogical)
                {
                    return new ProcessResult { ShouldAdd = false };
                }
            }

            // Update State
            _lastAddedPoint = pointToAdd;

            return new ProcessResult { ShouldAdd = true, PointToAdd = pointToAdd };
        }
    }
}
