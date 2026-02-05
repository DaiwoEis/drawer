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
        private const int MIN_POINTS_FOR_SMOOTHING = 4;

        /// <summary>
        /// Draws a single step of a stroke based on the current point index.
        /// Handles smoothing window (4 points) or single point (Eraser).
        /// </summary>
        public static void DrawIncremental(
            StrokeDrawContext context,
            IList<LogicPoint> points,
            int currentIndex,
            bool isEraser)
        {
            int count = currentIndex + 1;

            if (count >= MIN_POINTS_FOR_SMOOTHING)
            {
                var input = context.SmoothingService.ControlPoints;
                input.Clear();
                input.Add(points[currentIndex - 3]);
                input.Add(points[currentIndex - 2]);
                input.Add(points[currentIndex - 1]);
                input.Add(points[currentIndex]);

                context.SmoothingService.SmoothPoints();
                context.Renderer.DrawPoints(context.SmoothingService.OutputBuffer);
            }
            else if (isEraser)
            {
                var buffer = SharedDrawBuffers.SinglePointBuffer;
                buffer.Clear();
                buffer.Add(points[currentIndex]);
                context.Renderer.DrawPoints(buffer);
            }
        }

        /// <summary>
        /// Draws a full stroke by iterating through all points and applying incremental logic.
        /// Handles the edge case of short strokes (< 4 points) for non-erasers.
        /// </summary>
        public static void DrawFullStroke(
            StrokeDrawContext context,
            List<LogicPoint> points,
            bool isEraser)
        {
            if (points == null || points.Count == 0) return;

            // Handle short strokes (dots) that wouldn't trigger the smoothing window
            if (!isEraser && points.Count < MIN_POINTS_FOR_SMOOTHING)
            {
                context.Renderer.DrawPoints(points);
                return;
            }

            for (int i = 0; i < points.Count; i++)
            {
                DrawIncremental(
                    context, 
                    points, 
                    i, 
                    isEraser
                );
            }
        }
    }
}
