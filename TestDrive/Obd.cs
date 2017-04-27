using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.IO;
using System.Net.NetworkInformation;

namespace TestDrive
{
    public class Obd
    {
        const uint BUFFER_SIZE = 1024;
        const int INTERVAL = 100;
        const string DEFAULT_VALUE = "-255";
        private readonly Object _lock = new Object();
        private bool _connected = true;
        private Dictionary<string, string> _data;
        private IPAddress _ipAddress;
        private IPEndPoint _ipEndPoint;
        private Dictionary<string, string> _parameterIDs;
        private int _port;
        private bool _running = true;
        private Socket _socket;
        private Stream _stream;

        public async Task<bool> Init()
        {
            _running = true;
            _data = new Dictionary<string, string> {{"vin", DEFAULT_VALUE}};
            _parameterIDs = ObdParser.GetParameterIds();

            foreach (var v in _parameterIDs.Values)
            {
                _data.Add(v, DEFAULT_VALUE);
            }

            bool isObdReaderAvailable = false;

            foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (netInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                    || netInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                {
                    foreach (var addrInfo in netInterface.GetIPProperties().UnicastAddresses)
                    {
                        var ipaddr = addrInfo.Address;
                        if (ipaddr.ToString().StartsWith("192.168.0"))
                        {
                            isObdReaderAvailable = true;
                            break;
                        }
                    }
                }
            }

            if (!isObdReaderAvailable)
            {
                _socket = null;
                _running = false;
                _connected = false;
                return false;
            }

            if (!ConnectSocket())
            {
                _socket = null;
                _running = false;
                _connected = false;
                return false;
            }

            if (_connected)
            {
                // initialize the device
                string s;
                s = await SendAndReceive("ATZ\r");
                s = await SendAndReceive("ATE0\r");
                s = await SendAndReceive("ATL1\r");
                s = await SendAndReceive("ATSP00\r");
                
                PollObd();
                
                return true;
            }
            else
                return false;
        }

        public Dictionary<string, string> Read()
        {
            if (_socket == null)
            {
                // there's no connection
                return null;
            }

            var ret = new Dictionary<string, string>();

            lock (_lock)
            {
                foreach (var key in _data.Keys)
                {
                    ret.Add(key, _data[key]);
                }
                foreach (var v in _parameterIDs.Values)
                {
                    _data[v] = DEFAULT_VALUE;
                }
            }

            return ret;
        }

        private async void PollObd()
        {
            try
            {
                string s = await GetVIN();

                lock (_lock)
                {
                    _data["vin"] = s;
                }

                while (true)
                {
                    foreach (var cmd in _parameterIDs.Keys)
                    {
                        var key = _parameterIDs[cmd];
                        
                        s = await RunCmd(cmd);

                        if (s != "ERROR")
                        {
                            lock (_lock)
                            {
                                _data[key] = s;
                            }
                        }
                            
                        if (!_running) return;
                    }

                    await Task.Delay(INTERVAL);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                _running = false;

                if (_stream != null)
                {
                    _stream.Close();
                    _stream = null;
                }

                if (_socket != null)
                {
                    _socket.Close();
                    _socket = null;
                }
            }
        }

        private async Task<string> GetVIN()
        {
            var result = await SendAndReceive("0902\r");

            if (result.StartsWith("49"))
            {
                while (!result.Contains("49 02 05"))
                {
                    string tmp = await ReceiveAsync();
                    result += tmp;
                }
            }

            return ObdParser.ParseVINMsg(result);
        }

        private bool ConnectSocket()
        {
            // setup the connection via socket
            _ipAddress = IPAddress.Parse("192.168.0.10"); //hard-coded in obdlink MX
            _port = 35000; // hard-coded in obdlink MX
            _ipEndPoint = new IPEndPoint(_ipAddress, _port);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                _socket.Connect(_ipEndPoint);
                _stream = new NetworkStream(_socket);
                _connected = true;
            }
            catch (Exception)
            {
                _connected = false;
                return false;
            }
            return true;
        }

        private async Task<string> SendAndReceive(string msg)
        {
            try
            {
                await WriteAsync(msg);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }

            System.Threading.Thread.Sleep(100);

            try
            {
                string s = await ReceiveAsync();
                System.Diagnostics.Debug.WriteLine("Received: " + s);
                s = s.Replace("SEARCHING...\r\n", "");
                return s;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            return string.Empty;
        }

        private async Task WriteAsync(string msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
            byte[] buffer = Encoding.ASCII.GetBytes(msg);
            await _stream.WriteAsync(buffer, 0, buffer.Length);
            _stream.Flush();
        }

        private async Task<string> ReceiveAsync()
        {
            string ret = await ReceiveAsyncRaw();

            while (!ret.Trim().EndsWith(">"))
            {
                string tmp = await ReceiveAsyncRaw();
                ret = ret + tmp;
            }

            return ret;
        }

        private async Task<string> ReceiveAsyncRaw()
        {
            byte[] buffer = new byte[BUFFER_SIZE];
            var bytes = await _stream.ReadAsync(buffer, 0, buffer.Length);
            var s = Encoding.ASCII.GetString(buffer, 0, bytes);
            System.Diagnostics.Debug.WriteLine(s);
            return s;
        }

        private async Task<string> RunCmd(string cmd)
        {
            var result = await SendAndReceive(cmd + "\r");
            return ObdParser.ParseObd01Msg(result);
        }
    }
}