using System.Collections.Generic;
using UnityEngine;
using Features.Drawing.Domain;
using Features.Drawing.Domain.Entity;
using Features.Drawing.Domain.ValueObject;
using Features.Drawing.Domain.Interface;
using Features.Drawing.Domain.Data;
using Features.Drawing.App.Command;
using Features.Drawing.App.Interface;
using Features.Drawing.Service;
using Common.Constants;

namespace Features.Drawing.Service.Network
{
    /// <summary>
    /// Handles the processing and committing of remote strokes from the network.
    /// Encapsulates the logic for replaying, rendering, and saving remote actions.
    /// </summary>
    public class RemoteStrokeHandler
    {
        private readonly IStrokeRenderer _renderer;
        private readonly VisualDrawingHistoryManager _historyManager;
        private readonly StrokeCollisionService _collisionService;
        private readonly IBrushRegistry _brushRegistry;
        private readonly BrushStrategy _eraserStrategy;

        public RemoteStrokeHandler(
            IStrokeRenderer renderer,
            VisualDrawingHistoryManager historyManager,
            StrokeCollisionService collisionService,
            IBrushRegistry brushRegistry,
            BrushStrategy eraserStrategy)
        {
            _renderer = renderer;
            _historyManager = historyManager;
            _collisionService = collisionService;
            _brushRegistry = brushRegistry;
            _eraserStrategy = eraserStrategy;
        }

        public void CommitRemoteStroke(StrokeEntity stroke)
        {
            if (stroke == null || stroke.Points.Count == 0) return;

            // 1. Setup Renderer State for this stroke
            bool isEraser = stroke.BrushId == DrawingConstants.ERASER_BRUSH_ID;
            
            BrushStrategy strategy = null;
            if (isEraser)
            {
                strategy = _eraserStrategy;
                if (_renderer != null)
                {
                    if (strategy != null) _renderer.ConfigureBrush(strategy);
                    _renderer.SetEraser(true);
                    _renderer.SetBrushSize(stroke.Size);
                }
            }
            else
            {
                // Lookup strategy by ID
                strategy = _brushRegistry.GetBrushStrategy(stroke.BrushId);
                
                if (_renderer != null)
                {
                    Texture2D tex = strategy?.MainTexture;
                    if (strategy != null) _renderer.ConfigureBrush(strategy, tex);
                    
                    _renderer.SetEraser(false);
                    Color c = UIntToColor(stroke.ColorRGBA);
                    _renderer.SetBrushColor(c);
                    _renderer.SetBrushSize(stroke.Size);
                }
            }

            // 2. Create Command & Add to History
            // Note: We don't need to explicitly "Draw" it again if we rely on the Command to execute.
            // But the Command.Execute typically draws it.
            
            var cmd = new DrawStrokeCommand(
                stroke.Id.ToString(),
                stroke.SequenceId,
                new List<LogicPoint>(stroke.Points),
                strategy,
                null, // Runtime texture usually not synced perfectly, use default
                UIntToColor(stroke.ColorRGBA),
                stroke.Size,
                isEraser
            );
            
            // Execute (Draws it)
            if (_historyManager != null)
            {
                cmd.Execute(_renderer, _historyManager.SmoothingService);
                _historyManager.AddCommand(cmd);
            }
            
            // Spatial Index
            if (_collisionService != null)
            {
                _collisionService.Insert(stroke);
            }
        }

        public void ReceiveRemoteStroke(StrokeEntity stroke)
        {
            if (stroke == null) return;

            bool isEraser = stroke.BrushId == DrawingConstants.ERASER_BRUSH_ID;
            
            if (isEraser)
            {
                _renderer.SetEraser(true);
                if (_eraserStrategy != null) _renderer.ConfigureBrush(_eraserStrategy);
            }
            else
            {
                _renderer.SetEraser(false);
                var strategy = _brushRegistry.GetBrushStrategy(stroke.BrushId);
                if (strategy != null) _renderer.ConfigureBrush(strategy, strategy.MainTexture);
            }
            
            _renderer.SetBrushSize(stroke.Size);
            
            // Draw points
            _renderer.DrawPoints(stroke.Points);
            
            _renderer.EndStroke();
        }

        private Color UIntToColor(uint color)
        {
            byte r = (byte)((color >> 24) & 0xFF);
            byte g = (byte)((color >> 16) & 0xFF);
            byte b = (byte)((color >> 8) & 0xFF);
            byte a = (byte)(color & 0xFF);
            return new Color32(r, g, b, a);
        }
    }
}
