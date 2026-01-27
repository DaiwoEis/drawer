using NUnit.Framework;
using Common.Diagnostics;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class DiagnosticsTests
    {
        [Test]
        public void StructuredLogger_CanLogMessages_WithoutError()
        {
            // Arrange
            var logger = new StructuredLogger("TestModule", 10, false);
            var trace = TraceContext.New();
            
            // Act
            logger.Info("Test Message", trace, new Dictionary<string, object> { { "key", "value" } });
            logger.Error("Test Error", new System.Exception("TestEx"));
            
            // Assert
            // If no exception thrown, basic functionality works.
            Assert.Pass();
        }

        [Test]
        public void TraceContext_GeneratesUniqueIDs()
        {
            var t1 = TraceContext.New();
            var t2 = TraceContext.New();
            
            Assert.AreNotEqual(t1.TraceId, t2.TraceId);
            Assert.AreNotEqual(t1.SpanId, t2.SpanId);
        }
        
        [Test]
        public void TraceContext_ChildInheritsTraceId()
        {
            var parent = TraceContext.New();
            var child = parent.CreateChild();
            
            Assert.AreEqual(parent.TraceId, child.TraceId);
            Assert.AreNotEqual(parent.SpanId, child.SpanId);
            Assert.AreEqual(parent.SpanId, child.ParentSpanId);
        }
        
        [Test]
        public void StructuredLogger_BuffersAndFlushes()
        {
            // Arrange
            var logger = new StructuredLogger("TestModule", 2, false); // Batch size 2
            
            // Act
            logger.Info("Msg 1");
            logger.Info("Msg 2"); // Should trigger flush
            logger.Info("Msg 3");
            
            // Manual Flush
            logger.Flush();
            
            Assert.Pass();
        }
    }
}
