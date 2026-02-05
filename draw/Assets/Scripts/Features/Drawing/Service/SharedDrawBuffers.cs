using System.Collections.Generic;
using Features.Drawing.Domain.ValueObject;

namespace Features.Drawing.Service
{
    /// <summary>
    /// Holds shared buffers to minimize GC allocations across the drawing system.
    /// Not thread-safe; assumes single-threaded Unity main loop execution.
    /// </summary>
    public static class SharedDrawBuffers
    {
        /// <summary>
        /// A shared buffer of size 1 for wrapping a single point into a list
        /// without allocating a new List instance.
        /// </summary>
        public static readonly List<LogicPoint> SinglePointBuffer = new List<LogicPoint>(1);
    }
}
