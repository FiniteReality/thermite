using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Thermite.Discord.Models;
using Thermite.Utilities;

namespace Thermite.Discord
{
    internal partial class VoiceGatewayClient : IAsyncDisposable
    {
        private static readonly byte[] DiscoveryPacket = new byte[70];
        private readonly byte[] _discoveryPacketResponse = new byte[70];

        private async Task<bool> PerformDiscoveryAndSelectProtocolAsync(
            VoiceGatewayReady ready,
            CancellationToken cancellationToken = default)
        {
            Debug.Assert(EndPoint != null);

            var bytesSent = await _discoverySocket.SendToAsync(
                DiscoveryPacket, SocketFlags.None, EndPoint);

            if (bytesSent != 70)
                return false;

            var result = await _discoverySocket.ReceiveFromAsync(
                _discoveryPacketResponse, SocketFlags.None, EndPoint);

            if (result.ReceivedBytes != 70)
                return false;

            if (!TryGetLocalEndPoint(
                _discoveryPacketResponse, out var endpoint))
                return false;

            ClientEndPointUpdated?.Invoke(this, endpoint);
            ClientEndPoint = endpoint;

            return await SendSelectProtocolAsync(ready, cancellationToken);

            static bool TryGetLocalEndPoint(Span<byte> buffer,
                out IPEndPoint endPoint)
            {
                endPoint = default!;

                // split ip and port into separate spans
                var addressBuffer = buffer.Slice(4, 64)
                    .TrimEnd((byte)0);
                var portBuffer = buffer.Slice(buffer.Length - 2);

                if (!IPUtilities.TryParseAddress(
                    addressBuffer, out var address))
                    return false;

                if (!BinaryPrimitives.TryReadUInt16LittleEndian(portBuffer,
                    out ushort port))
                    return false;

                endPoint = new IPEndPoint(address, port);
                return true;
            }
        }
    }
}
