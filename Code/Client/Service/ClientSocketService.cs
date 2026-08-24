using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Client.Services
{
    public class ClientSocketService
    {
        private TcpClient? _client;
        private NetworkStream? _stream;

        public bool IsConnected =>
            _client != null && _client.Connected;

        public async Task<bool> ConnectAsync(
            string host = "127.0.0.1",
            int port = 8888)
        {
            try
            {
                Disconnect();

                _client = new TcpClient();

                await _client.ConnectAsync(host, port);

                _stream = _client.GetStream();

                return true;
            }
            catch
            {
                Disconnect();

                return false;
            }
        }

        public NetworkStream? GetStream()
        {
            return _stream;
        }

        public void Disconnect()
        {
            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch
            {
                // Không để lỗi đóng socket làm crash UI.
            }
            finally
            {
                _stream = null;
                _client = null;
            }
        }
    }
}