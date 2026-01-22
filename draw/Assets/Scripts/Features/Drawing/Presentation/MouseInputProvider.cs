using UnityEngine;
using System.Collections.Generic;
using Features.Drawing.Domain.ValueObject;
using Features.Drawing.Service;
using Common.Constants;

namespace Features.Drawing.Presentation
{
    /// <summary>
    /// Simple Mouse Input Provider for testing in Editor.
    /// Captures Mouse position, converts to LogicPoints, smooths them, and sends to Renderer.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class MouseInputProvider : MonoBehaviour
    {
        [SerializeField] private RectTransform _inputArea; // The UI area receiving input
        
        private CanvasRenderer _renderer;
        private StrokeSmoothingService _smoothingService;
        
        private bool _isDrawing = false;
        private List<LogicPoint> _currentStrokeRaw = new List<LogicPoint>();
        
        // Emulate high-frequency input
        private Vector2 _lastPos;

        // GC Optimization
        private List<LogicPoint> _smoothingInputBuffer = new List<LogicPoint>(8);
        private List<LogicPoint> _smoothingOutputBuffer = new List<LogicPoint>(32);
        private List<LogicPoint> _singlePointBuffer = new List<LogicPoint>(1);

        private void Awake()
        {
            _renderer = GetComponent<CanvasRenderer>();
            _smoothingService = new StrokeSmoothingService();
        }

        private void Update()
        {
            // 1. Input Detection (Mouse)
            bool isDown = Input.GetMouseButtonDown(0);
            bool isUp = Input.GetMouseButtonUp(0);
            bool isHeld = Input.GetMouseButton(0);

            // Optimization: Skip everything if no input
            if (!isDown && !isUp && !isHeld) return;

            Vector2 screenPos = Input.mousePosition;
            
            // Convert Screen -> Local UI -> Normalized (0-1)
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _inputArea, screenPos, null, out Vector2 localPos))
            {
                return; // Outside?
            }

            // Local (-W/2, -H/2) -> Normalized (0, 1)
            Rect rect = _inputArea.rect;
            float u = (localPos.x - rect.x) / rect.width;
            float v = (localPos.y - rect.y) / rect.height;
            Vector2 normalizedPos = new Vector2(u, v);

            // Filter out of bounds
            if (u < 0 || u > 1 || v < 0 || v > 1)
            {
                if (_isDrawing) EndStroke();
                return;
            }

            // 2. State Machine
            if (isDown)
            {
                StartStroke(normalizedPos);
            }
            else if (isHeld && _isDrawing)
            {
                ContinueStroke(normalizedPos);
            }
            else if (isUp && _isDrawing)
            {
                EndStroke();
            }
        }

        private void StartStroke(Vector2 pos)
        {
            _isDrawing = true;
            _currentStrokeRaw.Clear();
            _lastPos = pos;
            
            AddPoint(pos);
        }

        private void ContinueStroke(Vector2 pos)
        {
            // Simple distance filter to avoid duplicates
            if (Vector2.Distance(pos, _lastPos) < 0.001f) return;
            
            AddPoint(pos);
            _lastPos = pos;
        }

        private void EndStroke()
        {
            _isDrawing = false;
            _currentStrokeRaw.Clear();
            _renderer.EndStrokeState();
        }

        private void AddPoint(Vector2 pos)
        {
            // Fake pressure for mouse: 1.0
            LogicPoint newPoint = LogicPoint.FromNormalized(pos, 1.0f);
            _currentStrokeRaw.Add(newPoint);

            // 3. Process & Draw
            // Use sliding window to smooth only the new segment
            
            if (_currentStrokeRaw.Count >= 4)
            {
                // Extract last 4 control points without allocating new list
                // We use a reusable buffer
                _smoothingInputBuffer.Clear();
                int count = _currentStrokeRaw.Count;
                _smoothingInputBuffer.Add(_currentStrokeRaw[count - 4]);
                _smoothingInputBuffer.Add(_currentStrokeRaw[count - 3]);
                _smoothingInputBuffer.Add(_currentStrokeRaw[count - 2]);
                _smoothingInputBuffer.Add(_currentStrokeRaw[count - 1]);

                // Smooth into output buffer
                _smoothingService.SmoothPoints(_smoothingInputBuffer, _smoothingOutputBuffer);
                
                // Draw the interpolated segment
                _renderer.DrawPoints(_smoothingOutputBuffer);
            }
            else
            {
                // Just draw the point itself if not enough for spline
                _singlePointBuffer.Clear();
                _singlePointBuffer.Add(newPoint);
                _renderer.DrawPoints(_singlePointBuffer);
            }
        }
    }
}
