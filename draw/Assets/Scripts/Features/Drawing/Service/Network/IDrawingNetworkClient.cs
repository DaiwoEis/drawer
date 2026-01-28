using Features.Drawing.Domain.Network;

namespace Features.Drawing.Service.Network
{
    /// <summary>
    /// Contract for sending drawing packets to the network layer.
    /// </summary>
    public interface IDrawingNetworkClient
    {
        void SendBeginStroke(BeginStrokePacket packet);
        void SendUpdateStroke(UpdateStrokePacket packet);
        void SendEndStroke(EndStrokePacket packet);
        void SendAbortStroke(AbortStrokePacket packet);
        
        // Event for receiving packets (Mocking network callback)
        // In a real netcode, this would be handled by a NetworkManager callback
        event System.Action<object> OnPacketReceived;
    }
}
