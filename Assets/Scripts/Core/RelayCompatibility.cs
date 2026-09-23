using System;
using System.IO;
using System.Net.Sockets;

namespace Sandplay.Core
{
    public static class RelayCompatibility
    {
        public const int Version = 2;

        public static void Check(Stream stream, int version = Version)
        {
            int writeTimeout = stream.WriteTimeout;
            try
            {
                stream.ReadTimeout = 10000;
                stream.WriteTimeout = 10000;
                var hello = NetSerializer.Pack(NetMsgType.ProtocolHello, BitConverter.GetBytes(version));
                stream.Write(hello, 0, hello.Length);
                var length = Read(stream, 4);
                if (BitConverter.ToInt32(length, 0) != 5) throw new IOException("Invalid protocol response");
                var response = Read(stream, 5);
                if (response[0] != (byte)NetMsgType.ProtocolResult || BitConverter.ToInt32(response, 1) != version)
                    throw new IOException("Incompatible protocol");
            }
            catch (IOException ex) { throw new RelayVersionException(ex); }
            finally { stream.ReadTimeout = 45000; stream.WriteTimeout = writeTimeout; }
        }

        public static byte[] ReadControlResponse(Stream stream)
        {
            int length = BitConverter.ToInt32(Read(stream, 4), 0);
            if (length < 1 || length > 4096) throw new IOException("Invalid relay response length.");
            return Read(stream, length);
        }

        private static byte[] Read(Stream stream, int length)
        {
            var bytes = new byte[length];
            for (int offset = 0; offset < length;)
            {
                int count = stream.Read(bytes, offset, length - offset);
                if (count == 0) throw new EndOfStreamException();
                offset += count;
            }
            return bytes;
        }
    }

    public sealed class RelayVersionException : IOException
    {
        public RelayVersionException(Exception inner) : base("The app and session server are incompatible. Update the app; if it is current, contact support to update the server.", inner) { }
    }
}
