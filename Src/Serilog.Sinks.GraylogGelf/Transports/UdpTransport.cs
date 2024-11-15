﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

using Serilog.Sinks.GraylogGelf.Exceptions;
using Serilog.Sinks.GraylogGelf.Gelf;

using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;

namespace Serilog.Sinks.GraylogGelf.Transports
{
    /// <summary>
    /// 
    /// </summary>
     internal sealed class UdpTransport : ITransport
    {
        private readonly string _host;
        private readonly int _port;
        private IPEndPoint _endPoint;
        private DateTime _nextResolveTimeUtc;
        private readonly IGelfMessageSerializer _messageSerializer;
        private readonly GelfChunkEncoder _chunkEncoder;
        /// <summary>
        /// 
        /// </summary>
        public int MinMessageSizeBeforeCompressing { get; } = 512;

        /// <summary>
        /// Initializes a new instance of the <see cref="UdpTransport"/> class.
        /// </summary>
        /// <param name="host">The host.</param>
        /// <param name="port">The port.</param>
        /// <param name="messageSerializer">The message serializer.</param>
        /// <param name="chunkEncoder">The chunk encoder.</param>
        /// <param name="minMessageSizeBeforeCompressing">The minimum message size before compressing.</param>
        /// <exception cref="System.ArgumentNullException">messageSerializer</exception>
        /// <exception cref="System.ArgumentNullException">chunkEncoder</exception>
        public UdpTransport(string host, int port, IGelfMessageSerializer messageSerializer, GelfChunkEncoder chunkEncoder, int minMessageSizeBeforeCompressing)
        {
            _host = host;
            _port = port;
            _messageSerializer = messageSerializer ?? throw new ArgumentNullException(nameof(messageSerializer));
            _chunkEncoder = chunkEncoder ?? throw new ArgumentNullException(nameof(chunkEncoder));
            MinMessageSizeBeforeCompressing = minMessageSizeBeforeCompressing < 0
                                              ? 0 // always compress
                                              : minMessageSizeBeforeCompressing;
        }

        #region ----------------------- Public Methods --------------------------------------------

        /// <summary>
        /// Sends a single <see cref="GelfMessage" /> to GrayLog.
        /// </summary>
        /// <param name="message">The <see cref="GelfMessage" /> to send.</param>
        public void Send(GelfMessage message)
        {
            var msgBytes = _messageSerializer.SerializeToStringBytes(message);
            if (msgBytes.Length > MinMessageSizeBeforeCompressing)
            {
                using (var inputStream = new MemoryStream(msgBytes))
                using (var outputStream = new MemoryStream(msgBytes.Length))
                using (var writer = new GZipWriter(outputStream, new GZipWriterOptions() {CompressionType = CompressionType.GZip, LeaveStreamOpen = false}))
                {
                    writer.Write(string.Empty, inputStream);
                    msgBytes = outputStream.GetBuffer();
                }
            }
            using (var udpClient = new UdpClient())
                foreach (var bytes in _chunkEncoder.Encode(msgBytes))
                    udpClient.Send(bytes, bytes.Length, GetEndPoint());
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }

        #endregion

        private IPEndPoint GetEndPoint()
        {
            // we must use such logic to have no problems if DNS names will get redirected to other IP addresses
            if (_endPoint != null && DateTime.UtcNow <= _nextResolveTimeUtc)
            {
                return _endPoint;
            }
            var ipAdr = Dns.GetHostAddresses(_host).FirstOrDefault();
            if (ipAdr == null) throw new GraylogConnectionException($"Cannot resolve IP adress from hostname '{_host}'.");
            _nextResolveTimeUtc = DateTime.UtcNow.AddMinutes(1);
            return _endPoint = new IPEndPoint(ipAdr, _port);
        }
    }
}