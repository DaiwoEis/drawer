using UnityEngine;
using System.Collections.Generic;
using Features.Drawing.Domain;
using Features.Drawing.Domain.Command;
using Features.Drawing.Domain.Interface;
using Features.Drawing.Domain.ValueObject;
using Features.Drawing.Service;
using Features.Drawing.App.Interface;

namespace Features.Drawing.App.Command
{
    public class DrawStrokeCommand : DrawStrokeData, ICommand
    {
        private readonly BrushStrategy _strategy;
        private readonly Texture2D _runtimeTexture;

        public DrawStrokeCommand(string id, long sequenceId, List<LogicPoint> points, BrushStrategy strategy, Texture2D runtimeTexture, Color color, float size, bool isEraser)
            : base(id, sequenceId, new List<LogicPoint>(points), strategy?.name ?? "Default", color, size, isEraser)
        {
            _strategy = strategy;
            _runtimeTexture = runtimeTexture;
        }

        public void Execute(IStrokeRenderer renderer, StrokeSmoothingService smoothingService)
        {
            if (IsEraser)
            {
                // Ensure eraser uses the correct brush strategy (usually Hard Brush)
                if (_strategy != null)
                {
                    renderer.ConfigureBrush(_strategy, _runtimeTexture);
                }
                renderer.SetEraser(true);
                renderer.SetBrushSize(Size);
            }
            else
            {
                renderer.ConfigureBrush(_strategy, _runtimeTexture);
                renderer.SetEraser(false);
                renderer.SetBrushColor(Color);
                renderer.SetBrushSize(Size);
            }

            DrawStrokePoints(renderer, smoothingService);
            renderer.EndStroke();
        }

        private void DrawStrokePoints(IStrokeRenderer renderer, StrokeSmoothingService smoothingService)
        {
            StrokeDrawHelper.DrawFullStroke(
                new StrokeDrawContext(renderer, smoothingService),
                Points,
                IsEraser
            );
        }
    }
}
