﻿using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

using Serilog.Sinks.GraylogGelf.Exceptions;
using Serilog.Sinks.GraylogGelf.Gelf;

namespace Serilog.Sinks.GraylogGelf.Transports
{
    internal sealed class TcpTransport : ITransport
    {
        const int ReconnectIntervalSec = 10;

        private readonly string _hostName;

        private readonly IGelfMessageSerializer _messageSerializer;

        private readonly int _port;

        private readonly bool _useNullByteDelimiter;

        private readonly bool _useSecureConnection;

        private TcpClient? _client;

        private Stream _clientStream;

        private DateTime _utcReconnectionTime;

        internal TcpTransport(
            string hostName,
            int port,
            bool useSecureConnection,
            bool useNullByteDelimiter,
            IGelfMessageSerializer messageSerializer)
        {
            _hostName = hostName;
            _port = port;
            _useSecureConnection = useSecureConnection;
            _useNullByteDelimiter = useNullByteDelimiter;
            _messageSerializer = messageSerializer;
        }

        public void Dispose()
        {
            Close();
        }

        public void Send(GelfMessage message)
        {
            EstablishConnection();
            var bytes = _messageSerializer.SerializeToStringBytes(message);
            _clientStream.Write(bytes, 0, bytes.Length);
            if (_useNullByteDelimiter)
            {
                // write final NULL byte indicating that one complete GELF message has been written
                _clientStream.WriteByte(0);
            }
        }

        private void Close()
        {
            if (_client == null)
            {
                return;
            }

            _clientStream.SafeDispose();
            _client.SafeDispose();
            _client = null;
        }

        private void EstablishConnection()
        {
            try
            {
                if (_client != null && _client.Connected && DateTime.UtcNow < _utcReconnectionTime)
                {
                    return;
                }

                Close();
                _client = new TcpClient();
                _client.Connect(_hostName, _port);
                if (_useSecureConnection)
                {
                    var sslStream = (SslStream)(_clientStream = new SslStream(_client.GetStream(), false, UserCertificateValidationCallback));
                    sslStream.AuthenticateAsClient(_hostName);
                }
                else
                {
                    _clientStream = _client.GetStream();
                }

                _utcReconnectionTime = DateTime.UtcNow + TimeSpan.FromSeconds(ReconnectIntervalSec);
            }
            catch (Exception exception)
            {
                Close();
                throw new GraylogConnectionException($"Cannot connect to Graylog end point {_hostName}:{_port}", exception);
            }
        }

        private bool UserCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
