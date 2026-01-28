using Features.Drawing.Domain.Network;

namespace Features.Drawing.Service.Network
{
    /// <summary>
    /// Contract for sending drawing packets to the network layer.
    /// </summary>
    public interface IDrawingNetworkClient
    {
        /// <summary>
        /// Begin/End/Abort packets are treated as value types and can be sent synchronously.
        /// </summary>
        void SendBeginStroke(BeginStrokePacket packet);
        void SendUpdateStroke(UpdateStrokePacket packet);
        void SendEndStroke(EndStrokePacket packet);
        void SendAbortStroke(AbortStrokePacket packet);

        /// <summary>
        /// IMPORTANT: UpdateStrokePacket payload buffers are transient.
        /// Implementations must copy payload data if they need to access it asynchronously
        /// after SendUpdateStroke returns. Callers may recycle pooled buffers immediately.
        /// If loopback/echo is implemented, it MUST clone payloads and clear pooled flags
        /// before invoking OnPacketReceived to avoid double-return of pooled buffers.
        /// </summary>
        
        // Event for receiving packets (Mocking network callback)
        // In a real netcode, this would be handled by a NetworkManager callback
        event System.Action<object> OnPacketReceived;
    }
}
