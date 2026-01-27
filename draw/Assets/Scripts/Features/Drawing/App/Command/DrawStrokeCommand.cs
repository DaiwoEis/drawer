using UnityEngine;
using System.Collections.Generic;
using Features.Drawing.Domain;
using Features.Drawing.Domain.Interface;
using Features.Drawing.Domain.ValueObject;
using Features.Drawing.Service;

namespace Features.Drawing.App.Command
{
    public class DrawStrokeCommand : ICommand
    {
        public string Id { get; }
        private readonly List<LogicPoint> _points;
        private readonly BrushStrategy _strategy;
        private readonly Texture2D _runtimeTexture;
        private readonly Color _color;
        private readonly float _size;
        private readonly bool _isEraser;

        public DrawStrokeCommand(string id, List<LogicPoint> points, BrushStrategy strategy, Texture2D runtimeTexture, Color color, float size, bool isEraser)
        {
            Id = id;
            _points = new List<LogicPoint>(points); // Clone to ensure immutability
            _strategy = strategy;
            _runtimeTexture = runtimeTexture;
            _color = color;
            _size = size;
            _isEraser = isEraser;
        }

        public void Execute(IStrokeRenderer renderer, StrokeSmoothingService smoothingService)
        {
            if (_isEraser)
            {
                // Ensure eraser uses the correct brush strategy (usually Hard Brush)
                if (_strategy != null)
                {
                    renderer.ConfigureBrush(_strategy, _runtimeTexture);
                }
                renderer.SetEraser(true);
                renderer.SetBrushSize(_size);
            }
            else
            {
                renderer.ConfigureBrush(_strategy, _runtimeTexture);
                renderer.SetEraser(false);
                renderer.SetBrushColor(_color);
                renderer.SetBrushSize(_size);
            }

            DrawStrokePoints(renderer, smoothingService);
            renderer.EndStroke();
        }

        private void DrawStrokePoints(IStrokeRenderer renderer, StrokeSmoothingService smoothingService)
        {
            if (_points == null || _points.Count == 0) return;

            // Use local buffers to avoid memory overhead per command instance
            var smoothingInputBuffer = new List<LogicPoint>(4);
            var smoothingOutputBuffer = new List<LogicPoint>(64);
            var singlePointBuffer = new List<LogicPoint>(1);

            for (int i = 0; i < _points.Count; i++)
            {
                int count = i + 1;
                
                if (count >= 4)
                {
                     smoothingInputBuffer.Clear();
                     smoothingInputBuffer.Add(_points[i - 3]);
                     smoothingInputBuffer.Add(_points[i - 2]);
                     smoothingInputBuffer.Add(_points[i - 1]);
                     smoothingInputBuffer.Add(_points[i]);

                     smoothingService.SmoothPoints(smoothingInputBuffer, smoothingOutputBuffer);
                     renderer.DrawPoints(smoothingOutputBuffer);
                }
                else
                {
                     singlePointBuffer.Clear();
                     singlePointBuffer.Add(_points[i]);
                     renderer.DrawPoints(singlePointBuffer);
                }
            }
        }
    }
}
