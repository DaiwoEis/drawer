using System.Collections.Generic;
using UnityEngine;
using Features.Drawing.Domain.ValueObject;

namespace Features.Drawing.Domain.Interface
{
    public interface IStrokeRenderer
    {
        void ConfigureBrush(BrushStrategy strategy, Texture2D runtimeTexture = null);
        void SetBrushSize(float size);
        void SetBrushColor(Color color);
        void SetEraser(bool isEraser);
        void DrawStroke(List<LogicPoint> points, bool isEraser);
        void DrawIncremental(List<LogicPoint> points, int index, bool isEraser);
        void DrawPoints(IEnumerable<LogicPoint> points);
        void EndStroke();
        void ClearCanvas();
        
        // History/Snapshot Support
        void SetBakingMode(bool enabled);
        void RestoreFromBackBuffer();
    }
}
