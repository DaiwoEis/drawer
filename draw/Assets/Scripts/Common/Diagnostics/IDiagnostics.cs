using System;
using System.Collections.Generic;

namespace Common.Diagnostics
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }

    public struct TraceContext
    {
        public string TraceId;
        public string SpanId;
        public string ParentSpanId;

        public static TraceContext New()
        {
            return new TraceContext
            {
                TraceId = Guid.NewGuid().ToString("N"),
                SpanId = Guid.NewGuid().ToString("N"),
                ParentSpanId = null
            };
        }

        public TraceContext CreateChild()
        {
            return new TraceContext
            {
                TraceId = this.TraceId,
                SpanId = Guid.NewGuid().ToString("N"),
                ParentSpanId = this.SpanId
            };
        }
    }

    public interface IStructuredLogger
    {
        void Log(LogLevel level, string message, TraceContext trace = default, Dictionary<string, object> metadata = null);
        void Debug(string message, TraceContext trace = default, Dictionary<string, object> metadata = null);
        void Info(string message, TraceContext trace = default, Dictionary<string, object> metadata = null);
        void Warn(string message, TraceContext trace = default, Dictionary<string, object> metadata = null);
        void Error(string message, Exception ex = null, TraceContext trace = default, Dictionary<string, object> metadata = null);
    }
}
