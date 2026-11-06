using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace BalboaSpa.Protocol
{
    /// <summary>
    /// Long-lived BWA TCP session. Status is pushed by the spa; callers should not
    /// poll every second. The owner controls reconnect policy and disposal.
    /// </summary>
    public sealed class BalboaTcpClient : IDisposable
    {
        private readonly BalboaConnectionOptions _options;
        private readonly object _sendLock = new object();
        private TcpClient _client;
        private NetworkStream _stream;
        private Thread _readerThread;
        private volatile bool _running;

        public event EventHandler<BalboaFrameEventArgs> FrameReceived;
        public event EventHandler<BalboaConnectionEventArgs> ConnectionChanged;

        public BalboaTcpClient(BalboaConnectionOptions options)
        {
            _options = options ?? throw new ArgumentNullException("options");
        }

        public bool IsConnected { get { return _client != null && _client.Connected && _running; } }

        public void Connect(int timeoutMilliseconds)
        {
            if (timeoutMilliseconds < 1) throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            if (_running) return;
            var client = new TcpClient();
            var result = client.BeginConnect(_options.Host, _options.Port, null, null);
            try
            {
                if (!result.AsyncWaitHandle.WaitOne(timeoutMilliseconds))
                    throw new TimeoutException("Timed out connecting to Balboa BWA.");
                client.EndConnect(result);
            }
            catch { client.Close(); throw; }

            _client = client;
            _stream = client.GetStream();
            _running = true;
            RaiseConnectionChanged(true, null);
            _readerThread = new Thread(ReadLoop) { IsBackground = true, Name = "Balboa BWA reader" };
            _readerThread.Start();
            Send(BalboaCommands.RequestConfiguration());
        }

        public void Send(byte[] frame)
        {
            if (frame == null) throw new ArgumentNullException("frame");
            var stream = _stream;
            if (!_running || stream == null) throw new InvalidOperationException("Balboa BWA is not connected.");
            lock (_sendLock) stream.Write(frame, 0, frame.Length);
        }

        private void ReadLoop()
        {
            var buffer = new List<byte>();
            var readBuffer = new byte[512];
            try
            {
                while (_running)
                {
                    var count = _stream.Read(readBuffer, 0, readBuffer.Length);
                    if (count <= 0) break;
                    for (var i = 0; i < count; i++) buffer.Add(readBuffer[i]);
                    var frames = BalboaFrameCodec.ExtractFrames(buffer);
                    foreach (var frame in frames)
                    {
                        var handler = FrameReceived;
                        if (handler != null) handler(this, new BalboaFrameEventArgs(frame));
                    }
                }
            }
            catch (Exception ex)
            {
                if (_running) RaiseConnectionChanged(false, ex.Message);
            }
            finally
            {
                var wasRunning = _running;
                _running = false;
                try { if (_stream != null) _stream.Close(); } catch { }
                try { if (_client != null) _client.Close(); } catch { }
                _stream = null; _client = null;
                if (wasRunning) RaiseConnectionChanged(false, "Connection closed.");
            }
        }

        private void RaiseConnectionChanged(bool connected, string detail)
        {
            var handler = ConnectionChanged;
            if (handler != null) handler(this, new BalboaConnectionEventArgs(connected, detail));
        }

        public void Dispose()
        {
            _running = false;
            try { if (_stream != null) _stream.Close(); } catch { }
            try { if (_client != null) _client.Close(); } catch { }
        }
    }

    public sealed class BalboaFrameEventArgs : EventArgs
    {
        public BalboaFrameEventArgs(BalboaFrame frame) { Frame = frame; }
        public BalboaFrame Frame { get; private set; }
    }

    public sealed class BalboaConnectionEventArgs : EventArgs
    {
        public BalboaConnectionEventArgs(bool connected, string detail) { Connected = connected; Detail = detail; }
        public bool Connected { get; private set; }
        public string Detail { get; private set; }
    }
}
