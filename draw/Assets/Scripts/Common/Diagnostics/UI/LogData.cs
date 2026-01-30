using System;

namespace Common.Diagnostics.UI
{
    public struct LogData
    {
        public DateTime Timestamp;
        public LogLevel Level;
        public string Message;
        public string StackTrace;
    }
}
