using System.Collections.Generic;
using Features.Drawing.Domain.Interface;
using Features.Drawing.Domain.ValueObject;

namespace Features.Drawing.Service
{
    /// <summary>
    /// Shared helper for drawing strokes to ensure consistent behavior between 
    /// real-time drawing (DrawingAppService) and history replay (DrawStrokeCommand).
    /// </summary>
    public static class StrokeDrawHelper
    {
        /// <summary>
        /// Draws a single step of a stroke based on the current point index.
        /// Handles smoothing window (4 points) or single point (Eraser).
        /// </summary>
        public static void DrawIncremental(
            IStrokeRenderer renderer,
            StrokeSmoothingService smoothingService,
            IList<LogicPoint> points,
            int currentIndex,
            bool isEraser,
            List<LogicPoint> singlePointBuffer)
        {
            int count = currentIndex + 1;

            if (count >= 4)
            {
                var input = smoothingService.ControlPoints;
                input.Clear();
                input.Add(points[currentIndex - 3]);
                input.Add(points[currentIndex - 2]);
                input.Add(points[currentIndex - 1]);
                input.Add(points[currentIndex]);

                smoothingService.SmoothPoints();
                renderer.DrawPoints(smoothingService.OutputBuffer);
            }
            else if (isEraser)
            {
                singlePointBuffer.Clear();
                singlePointBuffer.Add(points[currentIndex]);
                renderer.DrawPoints(singlePointBuffer);
            }
        }

        /// <summary>
        /// Draws a full stroke by iterating through all points and applying incremental logic.
        /// Handles the edge case of short strokes (< 4 points) for non-erasers.
        /// </summary>
        public static void DrawFullStroke(
            IStrokeRenderer renderer,
            StrokeSmoothingService smoothingService,
            List<LogicPoint> points,
            bool isEraser,
            List<LogicPoint> singlePointBuffer)
        {
            if (points == null || points.Count == 0) return;

            // Handle short strokes (dots) that wouldn't trigger the smoothing window
            if (!isEraser && points.Count < 4)
            {
                renderer.DrawPoints(points);
                return;
            }

            for (int i = 0; i < points.Count; i++)
            {
                DrawIncremental(
                    renderer, 
                    smoothingService, 
                    points, 
                    i, 
                    isEraser, 
                    singlePointBuffer
                );
            }
        }
    }
}
