using System.Collections.Generic;
using Features.Drawing.Domain.Interface;
using Features.Drawing.Domain.ValueObject;

namespace Features.Drawing.Service
{
    /// <summary>
    /// Encapsulates the dependencies required for drawing strokes.
    /// Reduces parameter clutter in StrokeDrawHelper methods.
    /// </summary>
    public readonly struct StrokeDrawContext
    {
        public readonly IStrokeRenderer Renderer;
        public readonly StrokeSmoothingService SmoothingService;

        public StrokeDrawContext(
            IStrokeRenderer renderer, 
            StrokeSmoothingService smoothingService)
        {
            Renderer = renderer;
            SmoothingService = smoothingService;
        }
    }
}
