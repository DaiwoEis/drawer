using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using Features.Drawing.Domain.ValueObject;
using Common.Constants;
using System.Linq;

namespace Features.Drawing.Presentation
{
    public class GpuStrokeStampGenerator : System.IDisposable
    {
        private ComputeShader _shader;
        private int _kernelIndex;
        
        // Buffers
        private ComputeBuffer _inputBuffer;
        private ComputeBuffer _distBuffer;
        private ComputeBuffer _outputBuffer;
        private ComputeBuffer _argsBuffer; // For append buffer count

        // Configuration
        public float SpacingRatio { get; set; } = 0.15f;
        public float AngleJitter { get; set; } = 0f;
        
        private float _scaleX = 1f;
        private float _scaleY = 1f;
        private float _sizeScale = 1f;

        // Pooled Buffers (Zero-GC)
        private GpuLogicPoint[] _gpuPointBuffer;
        private float[] _distanceBuffer;
        private int[] _argsBufferData = new int[1];
        private StampData[] _stampReadbackBuffer;
        private List<LogicPoint> _pointListBuffer = new List<LogicPoint>(1024);

        // Struct matching HLSL
        [StructLayout(LayoutKind.Sequential)]
        private struct GpuLogicPoint
        {
            public float x;
            public float y;
            public float pressure;
            public float padding; // Added for alignment (16 bytes)
        }

        public GpuStrokeStampGenerator(ComputeShader shader)
        {
            _shader = shader;
            _kernelIndex = _shader.FindKernel("GenerateStamps");
            _argsBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);
        }

        public void SetCanvasResolution(Vector2Int resolution)
        {
            _scaleX = resolution.x / (float)DrawingConstants.LOGICAL_RESOLUTION;
            _scaleY = resolution.y / (float)DrawingConstants.LOGICAL_RESOLUTION;
            Debug.Log($"[GpuGen] Resolution Set: {resolution}, Scale: {_scaleX}, {_scaleY}");
        }

        public void SetSizeScale(float sizeScale)
        {
            _sizeScale = Mathf.Max(0.0001f, sizeScale);
        }

        public void Reset()
        {
            // Reset internal state if any
        }

        public void ProcessPoints(IEnumerable<LogicPoint> points, float brushSize, List<StampData> outputBuffer)
        {
            if (outputBuffer == null) return;
            outputBuffer.Clear();

            // Avoid ToList() allocation if possible
            IList<LogicPoint> pointList = points as IList<LogicPoint>;
            if (pointList == null)
            {
                _pointListBuffer.Clear();
                _pointListBuffer.AddRange(points);
                pointList = _pointListBuffer;
            }

            int count = pointList.Count;
            if (count < 2) return;

            // 1. Prepare Data
            if (_gpuPointBuffer == null || _gpuPointBuffer.Length < count)
            {
                // Resize buffer (power of 2 or exact fit)
                int newSize = Mathf.NextPowerOfTwo(count);
                _gpuPointBuffer = new GpuLogicPoint[newSize];
                _distanceBuffer = new float[newSize];
            }

            var gpuPoints = _gpuPointBuffer;
            var distances = _distanceBuffer;
            float totalDist = 0f;

            // Pre-calculate distances (Prefix Sum on CPU for now)
            gpuPoints[0] = new GpuLogicPoint { 
                x = pointList[0].X, 
                y = pointList[0].Y, 
                pressure = pointList[0].GetNormalizedPressure(),
                padding = 0
            };
            distances[0] = 0f;

            for (int i = 1; i < count; i++)
            {
                var pPrev = pointList[i - 1];
                var pCurr = pointList[i];
                
                // Distance in Render Space (approximate or Logical Space?)
                // The shader uses ScaleX/ScaleY to convert to Render Space.
                // Distance should be in Render Space to match Spacing (which is in pixels).
                // So convert to pixels first.
                
                Vector2 posPrev = new Vector2(pPrev.X * _scaleX, pPrev.Y * _scaleY);
                Vector2 posCurr = new Vector2(pCurr.X * _scaleX, pCurr.Y * _scaleY);
                
                float d = Vector2.Distance(posPrev, posCurr);
                totalDist += d;
                
                distances[i] = totalDist;
                
                gpuPoints[i] = new GpuLogicPoint { 
                    x = pCurr.X, 
                    y = pCurr.Y, 
                    pressure = pCurr.GetNormalizedPressure(),
                    padding = 0
                };
            }

            // 2. Setup Buffers
            if (_inputBuffer == null || _inputBuffer.count < count)
            {
                _inputBuffer?.Release();
                _inputBuffer = new ComputeBuffer(count, sizeof(float) * 4); // 16 bytes stride
            }
            if (_distBuffer == null || _distBuffer.count < count)
            {
                _distBuffer?.Release();
                _distBuffer = new ComputeBuffer(count, sizeof(float));
            }

            // Only set valid data range
            _inputBuffer.SetData(gpuPoints, 0, 0, count);
            _distBuffer.SetData(distances, 0, 0, count);

            // Estimate max output size
            // FIX: Include _sizeScale in spacing calculation to match Shader logic
            float spacing = Mathf.Max(1.0f, brushSize * _sizeScale * SpacingRatio);
            int estimatedStamps = Mathf.CeilToInt(totalDist / spacing) + count * 2; // + margin
            
            if (_outputBuffer == null || _outputBuffer.count < estimatedStamps)
            {
                _outputBuffer?.Release();
                _outputBuffer = new ComputeBuffer(estimatedStamps, sizeof(float) * 4, ComputeBufferType.Append); // StampData is 16 bytes (float4 size)
            }
            
            _outputBuffer.SetCounterValue(0);

            // 3. Dispatch
            _shader.SetBuffer(_kernelIndex, "InputPoints", _inputBuffer);
            _shader.SetBuffer(_kernelIndex, "PathDistances", _distBuffer);
            _shader.SetBuffer(_kernelIndex, "OutputStamps", _outputBuffer);
            
            _shader.SetFloat("Spacing", spacing); // Use pre-calculated consistent spacing
            _shader.SetFloat("SizeScale", _sizeScale);
            _shader.SetFloat("BrushSize", brushSize);
            _shader.SetFloat("ScaleX", _scaleX);
            _shader.SetFloat("ScaleY", _scaleY);
            _shader.SetInt("PointsCount", count);
            _shader.SetFloat("AngleJitter", AngleJitter);

            int threadGroups = Mathf.CeilToInt(count / 64f);
            _shader.Dispatch(_kernelIndex, threadGroups, 1, 1);

            // 4. Readback
            // Get count
            ComputeBuffer.CopyCount(_outputBuffer, _argsBuffer, 0);
            _argsBuffer.GetData(_argsBufferData);
            int stampCount = _argsBufferData[0];

            if (stampCount > 0)
            {
                if (_stampReadbackBuffer == null || _stampReadbackBuffer.Length < stampCount)
                {
                    int newSize = Mathf.NextPowerOfTwo(stampCount);
                    _stampReadbackBuffer = new StampData[newSize];
                }
                
                _outputBuffer.GetData(_stampReadbackBuffer, 0, 0, stampCount);
                
                // Avoid AddRange(array) which might iterate fully or copy inefficiently? 
                // List.AddRange is usually optimized for arrays.
                // But we are reading from a larger buffer.
                
                if (outputBuffer.Capacity < outputBuffer.Count + stampCount)
                {
                    outputBuffer.Capacity = outputBuffer.Count + stampCount;
                }
                
                for (int i = 0; i < stampCount; i++)
                {
                    outputBuffer.Add(_stampReadbackBuffer[i]);
                }
            }
        }

        public void Dispose()
        {
            if (_inputBuffer != null) { _inputBuffer.Release(); _inputBuffer = null; }
            if (_distBuffer != null) { _distBuffer.Release(); _distBuffer = null; }
            if (_outputBuffer != null) { _outputBuffer.Release(); _outputBuffer = null; }
            if (_argsBuffer != null) { _argsBuffer.Release(); _argsBuffer = null; }
        }
    }
}
