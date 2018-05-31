using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace MTProtoProxy
{
    internal static class TgSockets
    {
        private static volatile int _numberOfSockets;
        private static volatile bool _stop;
        private static readonly object _lockSockets = new object();
        private static readonly List<Socket> _sockets = new List<Socket>();
        private static readonly List<string> _ipServers = new List<string> { "149.154.175.50", "149.154.167.51", "149.154.175.100", "149.154.167.91", "149.154.171.5" };
        private static readonly List<string> _ipServersConfig = new List<string> { "149.154.175.50", "149.154.167.50", "149.154.175.100", "91.108.4.204", "91.108.56.161" };
        public static void Stop()
        {
            _stop = true;
        }

        public static IPEndPoint GetTgServerIp(in int dcId)
        {
            var ip1 = _ipServersConfig[dcId - 1];
            var ipAddress = IPAddress.Parse(ip1);
            return new IPEndPoint(ipAddress, Constants.TelegramPort);
        }

        public static IAsyncResult AsyncGetSocket(in int dcId)
        {
            var ip1 = _ipServersConfig[dcId - 1];
            var ipAddress = IPAddress.Parse(ip1);
            var endPoint = new IPEndPoint(ipAddress, Constants.TelegramPort);
            Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            try
            {
                IAsyncResult ret = socket.BeginConnect(endPoint, (ar) =>
                {

                },socket);
                return ret;
            }
            catch(SocketException e)
            {
                Console.WriteLine($"The server can not connect to the telegram server {ip1}");
                Console.WriteLine(e);
            }
            return null;
        }


        public static Socket GetSocket(in int dcId)
        {
            Socket socket = null;
            Console.WriteLine("这儿没有好方法来做这件事!");
            var ip1 = _ipServersConfig[dcId - 1];
            var ipAddress = IPAddress.Parse(ip1);
            var endPoint = new IPEndPoint(ipAddress, Constants.TelegramPort);
            socket = new Socket(endPoint.AddressFamily, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            try
            {
                socket.Connect(endPoint);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.TimedOut)
                {
                    ip1 = _ipServers[dcId - 1];
                    ipAddress = IPAddress.Parse(ip1);
                    endPoint = new IPEndPoint(ipAddress, Constants.TelegramPort);
                    try
                    {
                        socket.Connect(endPoint);
                    }
                    catch (Exception ex)
                    {
                        socket = null;
                        Console.WriteLine($"The server can not connect to the telegram server {ip1}");
                        Console.WriteLine(ex);
                    }
                }
            }
            catch (Exception e)
            {
                socket = null;
                Console.WriteLine($"The server can not connect to the telegram server {ip1}");
                Console.WriteLine(e);
            }
            return socket;
        }

        public static void Close()
        {
            Stop();
            lock (_lockSockets)
            {
                foreach (var socket in _sockets)
                {
                    try
                    {
                        socket.Shutdown(SocketShutdown.Both);
                        socket.Disconnect(false);
                        socket.Dispose();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
                _sockets.Clear();
            }
        }
    }
}