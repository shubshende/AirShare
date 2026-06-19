using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Makaretu.Dns;

namespace AirReceiver;

public class AirPlayService
{
    private MulticastService? _mdns;
    private ServiceDiscovery? _sd;
    private TcpListener? _rtspListener;
    private bool _isRunning;

    public event Action<string>? OnStatusMessage;

    public void Start()
    {
        try
        {
            _isRunning = true;
            var macAddress = "00:11:22:33:44:55";
            var deviceName = "AirReceiver PC";

            _mdns = new MulticastService();
            _sd = new ServiceDiscovery(_mdns);

            var airplayProfile = new ServiceProfile(deviceName, "_airplay._tcp", 7000);
            airplayProfile.AddProperty("deviceid", macAddress);
            airplayProfile.AddProperty("features", "0x5A7FFFF7,0xE");
            airplayProfile.AddProperty("model", "AppleTV5,3");
            airplayProfile.AddProperty("srcvers", "220.68");
            airplayProfile.AddProperty("flags", "0x4");
            airplayProfile.AddProperty("vv", "2");
            _sd.Advertise(airplayProfile);

            var raopName = $"{macAddress.Replace(":", "")}@{deviceName}";
            var raopProfile = new ServiceProfile(raopName, "_raop._tcp", 7000);
            raopProfile.AddProperty("ch", "2");
            raopProfile.AddProperty("cn", "0,1,2,3");
            raopProfile.AddProperty("et", "0,3,5");
            raopProfile.AddProperty("md", "0,1,2");
            raopProfile.AddProperty("pw", "false");
            raopProfile.AddProperty("sr", "44100");
            raopProfile.AddProperty("ss", "16");
            raopProfile.AddProperty("tp", "UDP");
            raopProfile.AddProperty("vn", "65537");
            raopProfile.AddProperty("vs", "220.68");
            raopProfile.AddProperty("am", "AppleTV5,3");
            _sd.Advertise(raopProfile);

            _mdns.Start();
            
            // Start TCP Listener for RTSP
            _rtspListener = new TcpListener(IPAddress.Any, 7000);
            _rtspListener.Start();
            Task.Run(ListenForRtspAsync);

            OnStatusMessage?.Invoke($"AirPlay Broadcast Started as '{deviceName}'");
            Debug.WriteLine("mDNS broadcast and RTSP listener started.");
        }
        catch (Exception ex)
        {
            OnStatusMessage?.Invoke($"Failed to start AirPlay: {ex.Message}");
        }
    }

    private async Task ListenForRtspAsync()
    {
        while (_isRunning)
        {
            try
            {
                var client = await _rtspListener!.AcceptTcpClientAsync();
                var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                OnStatusMessage?.Invoke($"Incoming connection from iPhone ({endpoint})...");
                
                // Read the first few bytes to see the request
                var stream = client.GetStream();
                var buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                
                if (bytesRead > 0)
                {
                    string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    string firstLine = request.Split('\n')[0].Trim();
                    OnStatusMessage?.Invoke($"Received RTSP: {firstLine}");
                }
                
                // We won't respond with a valid FairPlay payload, so the iPhone will disconnect.
                client.Close();
            }
            catch (Exception)
            {
                // Listener stopped or error
            }
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _rtspListener?.Stop();
        _sd?.Dispose();
        _mdns?.Stop();
        _mdns?.Dispose();
    }
}
