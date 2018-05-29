using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Threading.Tasks;

namespace MTProtoProxy
{
    public class MTProtoProxyServer : IDisposable
    {
        public string Secret { get => _secret; }
        public int Port { get => _port; }
        public bool IsClosed { get => _isDisposed; }
        private readonly string _secret;
        private readonly int _port;
        private readonly string _ip;
        private int _backLog;
        //private SocketListener _socketListener;
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
            ThrowIfDisposed();
            _backLog = backLog;
            //_socketListener = new SocketListener();
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
                //TgSockets.StartAsync();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            //if (_socketListener.StartListen(ipEndPoint, _backLog))
            //{
            //    StartListener();
            //    TgSockets.StartAsync();
            //}
        }


        private void StartAsyncListen()
        {
            _listenSocket.BeginAccept((ar) =>
            {
                Socket newsocket = _listenSocket.EndAccept(ar);
                StartAsyncListen();
                SocketAccepted(newsocket);
            }, null);
        }

        //private Task StartListener()
        //{
        //    return Task.Run(() =>
        //    {
        //        while (true)
        //        {
        //            try
        //            {
        //                var socket = _socketListener.Accept();
        //                SocketAccepted(socket);
        //            }
        //            catch (Exception e)
        //            {
        //                Console.WriteLine(e);
        //                break;
        //            }
        //        }
        //    });
        //}
        private void SocketAccepted(Socket socket)
        {
            try
            {
                Console.WriteLine("A new connection was created");
                var buffer = new byte[64];
                //var result = socket.Receive(buffer);
                socket.BeginReceive(buffer, 0, buffer.Length, 0, OnClientSocketRecive, new object[] {socket, buffer, 0});
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
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
                    socket.Shutdown(SocketShutdown.Send);
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
                    //socket.Shutdown(SocketShutdown.Both);
                    //socket.Close();
                    Console.WriteLine("not enough 64 bytes! just " + recived_bytes + " socket" + socket.GetHashCode() + "  " + buffer.Length);
                    socket.BeginReceive(buffer, recived_bytes, buffer.Length - recived_bytes, 0, OnClientSocketRecive, new object[] { socket, buffer, recived_bytes });
                }
            }
            catch
            {
                socket.Shutdown(SocketShutdown.Both);
            }
        }
        //private void MTProtoSocketDisconnected(object sender, EventArgs e)
        //{
        //    var mtp = (MTProtoSocket)sender;
        //    mtp.Dispose();
        //    lock (_lockConnection)
        //    {
        //        _protoSockets.Remove(mtp);
        //    }
        //}
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException("Connection was disposed.");
            }
        }
    }
}