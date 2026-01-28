using System.Collections.Generic;
using UnityEngine;
using Features.Drawing.Domain.Network;

namespace Features.Drawing.Service.Network
{
    public class MockNetworkClient : MonoBehaviour, IDrawingNetworkClient
    {
        public event System.Action<object> OnPacketReceived;

        [Header("Simulation Settings")]
        [SerializeField] private bool _loopback = true;
        [SerializeField] private float _latency = 0.05f; // 50ms

        public void SendBeginStroke(BeginStrokePacket packet)
        {
            if (_loopback) StartCoroutine(SimulateReceive(packet));
        }

        public void SendUpdateStroke(UpdateStrokePacket packet)
        {
            if (_loopback) StartCoroutine(SimulateReceive(packet));
        }

        public void SendEndStroke(EndStrokePacket packet)
        {
            if (_loopback) StartCoroutine(SimulateReceive(packet));
        }

        public void SendAbortStroke(AbortStrokePacket packet)
        {
            if (_loopback) StartCoroutine(SimulateReceive(packet));
        }

        private System.Collections.IEnumerator SimulateReceive(object packet)
        {
            yield return new WaitForSeconds(_latency);
            
            // In loopback, we need to modify the ID so it's treated as "Remote"
            // But our NetworkService distinguishes local/remote by whether it originated from OnLocal... or OnPacketReceived.
            // If we receive a packet with same ID as local stroke, logic might get confused if we shared ID space.
            // But here we are just calling OnPacketReceived.
            // DrawingNetworkService.HandleBeginStroke checks _activeRemoteStrokes.
            // It does NOT check against _currentLocalStrokeId.
            // So if we loopback, we will see a "Ghost" of our own stroke lagging behind us.
            // This is actually a great test for the Ghost layer!
            
            // If we want to simulate a "different user", we should offset the ID.
            if (packet is BeginStrokePacket begin) { begin.StrokeId += 10000; packet = begin; }
            if (packet is UpdateStrokePacket update) { update.StrokeId += 10000; packet = update; }
            if (packet is EndStrokePacket end) { end.StrokeId += 10000; packet = end; }
            
            OnPacketReceived?.Invoke(packet);
        }
    }
}
