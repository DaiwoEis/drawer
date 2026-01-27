using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Common.Diagnostics
{
    /// <summary>
    /// A robust, structured logger that supports batching and context injection.
    /// Designed for low allocation in critical paths.
    /// </summary>
    public class StructuredLogger : IStructuredLogger
    {
        private readonly Queue<string> _logBuffer = new Queue<string>(100);
        private readonly int _batchSize;
        private readonly bool _enableConsoleEcho;
        private readonly string _moduleName;

        private StringBuilder _sb = new StringBuilder(1024);

        public StructuredLogger(string moduleName = "App", int batchSize = 50, bool enableConsoleEcho = true)
        {
            _moduleName = moduleName;
            _batchSize = batchSize;
            _enableConsoleEcho = enableConsoleEcho;
        }

        public void Debug(string message, TraceContext trace = default, Dictionary<string, object> metadata = null)
        {
            Log(LogLevel.Debug, message, trace, metadata);
        }

        public void Info(string message, TraceContext trace = default, Dictionary<string, object> metadata = null)
        {
            Log(LogLevel.Info, message, trace, metadata);
        }

        public void Warn(string message, TraceContext trace = default, Dictionary<string, object> metadata = null)
        {
            Log(LogLevel.Warn, message, trace, metadata);
        }

        public void Error(string message, Exception ex = null, TraceContext trace = default, Dictionary<string, object> metadata = null)
        {
            if (ex != null)
            {
                if (metadata == null) metadata = new Dictionary<string, object>();
                metadata["exception"] = ex.ToString();
            }
            Log(LogLevel.Error, message, trace, metadata);
        }

        public void Log(LogLevel level, string message, TraceContext trace = default, Dictionary<string, object> metadata = null)
        {
            // 1. Format Log Entry (JSON-like structure)
            // Reuse StringBuilder to minimize GC
            lock (_sb)
            {
                _sb.Clear();
                _sb.Append("{");
                
                _sb.Append($"\"ts\":\"{DateTime.UtcNow:O}\",");
                _sb.Append($"\"lvl\":\"{level}\",");
                _sb.Append($"\"mod\":\"{_moduleName}\",");
                
                if (!string.IsNullOrEmpty(trace.TraceId))
                {
                    _sb.Append($"\"tid\":\"{trace.TraceId}\",");
                    _sb.Append($"\"sid\":\"{trace.SpanId}\",");
                }

                _sb.Append($"\"msg\":\"{Escape(message)}\"");

                if (metadata != null && metadata.Count > 0)
                {
                    _sb.Append(",\"ctx\":{");
                    int i = 0;
                    foreach (var kvp in metadata)
                    {
                        _sb.Append($"\"{kvp.Key}\":\"{Escape(kvp.Value?.ToString() ?? "null")}\"");
                        if (i < metadata.Count - 1) _sb.Append(",");
                        i++;
                    }
                    _sb.Append("}");
                }

                _sb.Append("}");

                string finalLog = _sb.ToString();

                // 2. Buffer for batching
                lock (_logBuffer)
                {
                    _logBuffer.Enqueue(finalLog);
                    if (_logBuffer.Count >= _batchSize)
                    {
                        Flush();
                    }
                }

                // 3. Console Echo (for development)
                if (_enableConsoleEcho)
                {
                    switch (level)
                    {
                        case LogLevel.Error: UnityEngine.Debug.LogError(finalLog); break;
                        case LogLevel.Warn: UnityEngine.Debug.LogWarning(finalLog); break;
                        default: UnityEngine.Debug.Log(finalLog); break;
                    }
                }
            }
        }

        public void Flush()
        {
            lock (_logBuffer)
            {
                if (_logBuffer.Count == 0) return;

                // In a real scenario, this would send to an HTTP endpoint (ELK/Splunk)
                // For now, we simulate "Efficient Transmission" by clearing the buffer
                // or writing to a local file in a separate thread.
                
                // Example: NetworkClient.SendBatch(_logBuffer.ToArray());
                _logBuffer.Clear();
            }
        }

        private string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
