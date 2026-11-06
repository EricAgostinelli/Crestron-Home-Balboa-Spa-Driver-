using System;

namespace BalboaSpa.Protocol
{
    public sealed class BalboaConnectionOptions
    {
        public BalboaConnectionOptions(string host, int port)
        {
            if (String.IsNullOrWhiteSpace(host))
                throw new ArgumentException("A BWA hostname or IP address is required.", "host");
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException("port");

            Host = host.Trim();
            Port = port;
        }

        public string Host { get; private set; }
        public int Port { get; private set; }

        public static BalboaConnectionOptions CreateDefault(string host)
        {
            return new BalboaConnectionOptions(host, 4257);
        }
    }
}
