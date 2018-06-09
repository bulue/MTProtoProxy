﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;

namespace MTProtoProxy
{
    public class MTProtoProxyServer
    {
        public string Secret { get => _secret; }
        public int Port { get => _port; }
        public bool IsClosed { get => _isDisposed; }
        private readonly string _secret;
        private readonly int _port;
        private readonly string _ip;
        private int _backLog;
        private Socket _listenSocket;
        private readonly object _lockListener = new object();
        private readonly object _lockConnection = new object();
        private readonly List<MTProtoSocket> _protoSockets = new List<MTProtoSocket>();
        private volatile bool _isDisposed;
        public MTProtoProxyServer(in string secret, in int port, in string ip = "default")
        {
            _secret = secret;
            _port = port;
            _ip = ip;
            Console.WriteLine("MTProtoProxy Server By Telegram @MTProtoProxy v1.0.5-alpha");
            Console.WriteLine("open source => https://github.com/TGMTProto/MTProtoProxy");
        }
        public void Start(in int backLog = 100)
        {
            _backLog = backLog;
            IPAddress ipAddress = null;
            if (_ip == "default")
            {
                ipAddress = IPAddress.Any;
            }
            else
            {
                if (!IPAddress.TryParse(_ip, out ipAddress))
                {
                    throw new Exception("ipAddress is not valid");
                }
            }
            try
            {
                var ipEndPoint = new IPEndPoint(ipAddress, _port);
                _listenSocket = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                _listenSocket.Bind(ipEndPoint);
                _listenSocket.Listen(100);
                StartAsyncListen();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void Stop()
        {
            try
            {
                _listenSocket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                
            }
            try
            {
                _listenSocket.Close();
            }
            catch
            {
            }
        }


        private void StartAsyncListen()
        {
            try
            {
                _listenSocket.BeginAccept((ar) =>
                {
                    try
                    {
                        Socket newsocket = _listenSocket.EndAccept(ar);
                        StartAsyncListen();
                        SocketAccepted(newsocket);
                    }
                    catch(Exception e)
                    {
                        Logging.Error(e.ToString());
                    }
                }, null);
            }
            catch(Exception e)
            {
                Logging.Error(e.ToString());
            }
        }

        private void SocketAccepted(Socket socket)
        {
            try
            {
                Console.WriteLine("A new connection was created");
                var buffer = new byte[64];
                socket.BeginReceive(buffer, 0, buffer.Length, 0, OnClientSocketRecive, new object[] {socket, buffer, 0});
            }
            catch (Exception e)
            {
                Logging.Error(e.ToString());
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
        }

        private void OnClientSocketRecive(IAsyncResult ar)
        {

            object[] container = (object[])ar.AsyncState;
            Socket socket = (Socket)container[0];
            byte[] buffer = (byte[])container[1];
            int recived_bytes = (int)container[2];
            try
            {
                int bytes = socket.EndReceive(ar);
                if (bytes == 0)
                {
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                    Console.WriteLine("client is closed!!!!!!!" + " socket" + socket.GetHashCode());
                    return;
                }
                recived_bytes += bytes;
                if (recived_bytes == 64)
                {
                    var mtpSocket = new MTProtoSocket(socket);
                    mtpSocket.StartAsync(buffer, _secret);
                    Console.WriteLine("start a new session!!");
                }
                else
                {
                    Console.WriteLine("not enough 64 bytes! just " + recived_bytes + " socket" + socket.GetHashCode() + "  " + buffer.Length);
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                    //socket.BeginReceive(buffer, recived_bytes, buffer.Length - recived_bytes, 0, OnClientSocketRecive, new object[] { socket, buffer, recived_bytes });
                }
            }
            catch(Exception e)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                Logging.Error(e.ToString());
            }
        }
        protected virtual void Dispose(in bool isDisposing)
        {
            lock (_lockListener)
            {
                try
                {
                    if (_isDisposed)
                    {
                        return;
                    }
                    _isDisposed = true;

                    if (!isDisposing)
                    {
                        return;
                    }
                    //_socketListener.Stop();
                    _listenSocket.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                lock (_lockConnection)
                {
                    foreach (var mtp in _protoSockets)
                    {
                        try
                        {
                            mtp.Close();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }
                }
                TgSockets.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }
}