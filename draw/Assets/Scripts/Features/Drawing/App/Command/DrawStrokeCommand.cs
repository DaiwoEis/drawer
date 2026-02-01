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
        public long SequenceId { get; }
        private readonly List<LogicPoint> _points;
        private readonly BrushStrategy _strategy;
        private readonly Texture2D _runtimeTexture;
        private readonly Color _color;
        private readonly float _size;
        private readonly bool _isEraser;
        private readonly List<LogicPoint> _smoothingInputBuffer;
        private readonly List<LogicPoint> _smoothingOutputBuffer;
        private readonly List<LogicPoint> _singlePointBuffer;

        public DrawStrokeCommand(string id, long sequenceId, List<LogicPoint> points, BrushStrategy strategy, Texture2D runtimeTexture, Color color, float size, bool isEraser)
        {
            Id = id;
            SequenceId = sequenceId;
            _points = new List<LogicPoint>(points); // Clone to ensure immutability
            _strategy = strategy;
            _runtimeTexture = runtimeTexture;
            _color = color;
            _size = size;
            _isEraser = isEraser;
            _smoothingInputBuffer = new List<LogicPoint>(4);
            _smoothingOutputBuffer = new List<LogicPoint>(64);
            _singlePointBuffer = new List<LogicPoint>(1);
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

            for (int i = 0; i < _points.Count; i++)
            {
                int count = i + 1;
                
                if (count >= 4)
                {
                     _smoothingInputBuffer.Clear();
                     _smoothingInputBuffer.Add(_points[i - 3]);
                     _smoothingInputBuffer.Add(_points[i - 2]);
                     _smoothingInputBuffer.Add(_points[i - 1]);
                     _smoothingInputBuffer.Add(_points[i]);

                     smoothingService.SmoothPoints(_smoothingInputBuffer, _smoothingOutputBuffer);
                     renderer.DrawPoints(_smoothingOutputBuffer);
                }
                else if (_isEraser)
                {
                    _singlePointBuffer.Clear();
                    _singlePointBuffer.Add(_points[i]);
                    renderer.DrawPoints(_singlePointBuffer);
                }
            }
        }
    }
}
