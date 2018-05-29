using System;
using System.Net.Sockets;
using System.Net;

namespace MTProtoProxy
{
    internal class SocketListener
    {
        private Socket _listenSocket;
        public bool StartListen(in IPEndPoint ipEndPoint, in int backlog)
        {
            var socket = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);

            try
            {
                socket.Bind(ipEndPoint);
                socket.Listen(int.MaxValue);
                _listenSocket = socket;
                return true;
            }
            catch (Exception e)
            {
                socket.Dispose();
                Console.WriteLine(e);
            }
            return false;
        }

        public Socket Accept()
        {
            return _listenSocket.Accept();
        }

        public void Stop()
        {
            _listenSocket.Dispose();
            _listenSocket = null;
        }
    }
}