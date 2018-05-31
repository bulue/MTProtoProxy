using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MTProtoProxy
{
    internal class MTProtoSocket
    {
        public IPEndPoint IPEndPoint { get => _ipEndPoint; }
        private readonly IPEndPoint _ipEndPoint;
        private readonly MTProtoPacket _mtprotoPacketTgSocket;
        private readonly MTProtoPacket _mtprotoPacketClientSocket;
        private Socket _tgSocket;
        private Socket _clientSocket;
        private readonly object _closedLock = new object();
        private volatile bool _closed = false;

        private byte[] _tgSocketRecive = new byte[64 * 1024];
        private byte[] _clientSocketRecive = new byte[64 * 1024];
        //private readonly object _lockConnection = new object();
        //private readonly object _lockTgSocket = new object();
        public event EventHandler MTProtoSocketDisconnected;
        public MTProtoSocket(in Socket clientSocket)
        {
            _clientSocket = clientSocket;
            _mtprotoPacketTgSocket = new MTProtoPacket();
            _mtprotoPacketClientSocket = new MTProtoPacket();
            _ipEndPoint = (IPEndPoint)_clientSocket.RemoteEndPoint;
        }

        //public void StartAsync(in string secret)
        //{
        //    _tgSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
        //}
        public void StartAsync(in byte[] buffer, in string secret)
        {
            if (_closed) return;

            _mtprotoPacketClientSocket.Clear();
            _mtprotoPacketClientSocket.SetInitBufferObfuscated2(buffer, secret);


            if (_mtprotoPacketClientSocket.ProtocolType == ProtocolType.None)
            {
                Console.WriteLine("Error in protocol");
                return;
            }
            var dcId = Math.Abs(BitConverter.ToInt16(buffer.SubArray(60, 2), 0));

            var endPoint = TgSockets.GetTgServerIp(dcId);
            _tgSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            _tgSocket.BeginConnect(endPoint, TgSocketConnectCallback, null);

            //_tgSocket = TgSockets.GetSocket(dcId);
            //_tgSocket = (Socket)xxx.AsyncState;
            //_tgSocket.EndConnect(xxx);

            //Console.WriteLine("原来这样真的可以的！！");

        }


        void TgSocketConnectCallback(IAsyncResult aret)
        {
            if (_closed) return;
            try
            {
                _tgSocket.EndConnect(aret);
                //StartTGSocketRecive();
                //StartClientSocketRecive();
                Console.WriteLine("真的开始 接收信息了!!");
                var randomBuffer = _mtprotoPacketTgSocket.GetInitBufferObfuscated2(_mtprotoPacketClientSocket.ProtocolType);
                //AsyncSendToTgSocket(randomBuffer, randomBuffer.Length);
                _tgSocket.BeginSend(randomBuffer, 0, randomBuffer.Length, 0, TgSocketHandleShakeCallback, new object[] { randomBuffer, 0 });
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                Close();
            }
        }

        void TgSocketHandleShakeCallback(IAsyncResult ar)
        {
            if (_closed) return;
            try
            {
                int bytes = _tgSocket.EndSend(ar);
                object[] container = (object[])ar.AsyncState;
                byte[] sendbuffer = (byte[])container[0];
                int allsendbytes = (int)container[1];
                if (bytes > 0)
                {
                    allsendbytes += bytes;
                    if (allsendbytes == sendbuffer.Length)
                    {
                        StartClientSocketRecive();
                        StartTGSocketRecive();
                        Console.WriteLine("handle shake with tg server is suceess!");
                    }
                    else
                    {
                        _tgSocket.BeginSend(sendbuffer, allsendbytes, sendbuffer.Length - allsendbytes, 0, TgSocketHandleShakeCallback, new object[] { sendbuffer, allsendbytes });
                    }
                }
                else
                {
                    _clientSocket.Shutdown(SocketShutdown.Send);
                    Console.WriteLine("tg socket send first 64 bytes faild!");
                }
            }
            catch(Exception e)
            {

            }
        }

        public void StartTGSocketRecive()
        {
            if (_closed) return;
            try
            {
                _tgSocket.BeginReceive(_tgSocketRecive, 0, _tgSocketRecive.Length, SocketFlags.None, TGSocketReciveCallback, null);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                Close();
            }
        }

        private void TGSocketReciveCallback(IAsyncResult ar)
        {
            if (_closed) return;
            try
            {
                int bytes = _tgSocket.EndReceive(ar);
                if (bytes == 0)
                {
                    Console.WriteLine("A connection was closed, clientSocket close send");
                    _clientSocket.Shutdown(SocketShutdown.Send);
                    return;
                }

                var decrypt = _mtprotoPacketTgSocket.DecryptObfuscated2(_tgSocketRecive, bytes);
                var encrypt = _mtprotoPacketClientSocket.EncryptObfuscated2(decrypt, decrypt.Length);

                AsyncSendToClientSocket(encrypt, encrypt.Length);
            }
            catch
            {
                Close();
            }
        }


        public void AsyncSendToClientSocket(in byte[] buffer, in int length)
        {
            try
            {
                _clientSocket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, ClientSocketSendCallBack, new object[] {buffer, length });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Close();
            }
        }

        public void ClientSocketSendCallBack(IAsyncResult ar)
        {
            if (_closed) return;
            try
            {
                int bytes = _clientSocket.EndSend(ar);
                object[] state = (object[])ar.AsyncState;
                var buffer = (byte[])state[0];
                int lenght = (int)state[1];
                int bytesRemaining = lenght - bytes;
                if (bytesRemaining > 0)
                {
                    Console.WriteLine("reconstruct _remoteSendBuffer to re-send");
                    Buffer.BlockCopy(buffer, bytes, buffer, 0, bytesRemaining);
                    AsyncSendToClientSocket(buffer, bytesRemaining);
                }
                else
                {
                    StartTGSocketRecive();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Close();
            }
        }


        public void StartClientSocketRecive()
        {
            if (_closed) return;
            try
            {
                _clientSocket.BeginReceive(_clientSocketRecive, 0, _clientSocketRecive.Length, 0, ClientSocketReciveCallback, null);
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
                Close();
            }
        }


        private void ClientSocketReciveCallback(IAsyncResult ar)
        {
            if (_closed) return;
            try
            {
                int bytes = _clientSocket.EndReceive(ar);
                if (bytes > 0)
                {
                    var decrypt = _mtprotoPacketClientSocket.DecryptObfuscated2(_clientSocketRecive, bytes);
                    var encrypt = _mtprotoPacketTgSocket.EncryptObfuscated2(decrypt, decrypt.Length);
                    AsyncSendToTgSocket(encrypt, encrypt.Length);
                }
                else
                {
                    _tgSocket.Shutdown(SocketShutdown.Send);
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                Close();
            }
        }

        private void AsyncSendToTgSocket(in byte[] buffer, in int length)
        {
            if (_closed) return;
            try
            {
                _tgSocket.BeginSend(buffer, 0, length, SocketFlags.None, TgSocketSendCallback, new object[] { buffer, length });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Close();
            }
        }


        private void TgSocketSendCallback(IAsyncResult ar)
        {
            if (_closed) return;

            try
            {
                int sendBytes = _tgSocket.EndSend(ar);
                object[] container = (object[])ar.AsyncState;
                byte[] buffer = (byte[])container[0];
                int length = (int)container[1];
                int bytesRemaining = length - sendBytes;
                if (bytesRemaining > 0)
                {
                    Console.WriteLine("reconstruct _remoteSendBuffer to re-send");
                    Buffer.BlockCopy(buffer, sendBytes, buffer, 0, bytesRemaining);
                    AsyncSendToTgSocket(buffer, bytesRemaining);
                }
                else
                {
                    StartClientSocketRecive();
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
                Close();
            }
        }


        public void Close()
        {
            lock(_closedLock)
            {
                if (_closed)
                {
                    return;
                }
                _closed = true;
            }

            try
            {
                _tgSocket.Shutdown(SocketShutdown.Both);
                _tgSocket.Close();
            }
            catch
            {

            }

            try
            {
                _clientSocket.Shutdown(SocketShutdown.Both);
                _clientSocket.Close();
            }
            catch
            {

            }
        }
    }
}