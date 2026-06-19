using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using Microsoft.Win32;
using Hardcodet.Wpf.TaskbarNotification;

namespace AirReceiver;

public partial class MainWindow : Window
{
    private Process? _airPlayProcess;
    private readonly MiracastService _miracastService = new();

    public MainWindow()
    {
        InitializeComponent();
        MyNotifyIcon.Icon = SystemIcons.Information;
        
        LoadSettings();
        StartAirPlayCore();
        _ = RefreshMiracastStatusAsync();
    }

    private void LoadSettings()
    {
        try
        {
            var airPlayCoreDirectory = FindAirPlayCoreDirectory();
            if (airPlayCoreDirectory is null) return;
            var appSettingsPath = Path.Combine(airPlayCoreDirectory, "appsettings_win.json");

            if (File.Exists(appSettingsPath))
            {
                var json = File.ReadAllText(appSettingsPath);
                var root = JsonNode.Parse(json);
                var instanceName = root?["AirPlayReceiver"]?["Instance"]?.ToString() ?? "SHUBHAM";
                ReceiverNameTextBox.Text = instanceName;

                var prefs = root?["AirReceiverPrefs"];
                FullscreenCheckBox.IsChecked = prefs?["Fullscreen"]?.GetValue<bool>() ?? false;
                BorderlessCheckBox.IsChecked = prefs?["Borderless"]?.GetValue<bool>() ?? false;
                AlwaysOnTopCheckBox.IsChecked = prefs?["AlwaysOnTop"]?.GetValue<bool>() ?? false;
            }

            var startupKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            StartupCheckBox.IsChecked = startupKey?.GetValue("AirReceiver") != null;
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error loading settings: {ex.Message}");
        }
    }

    private void StartAirPlayCore()
    {
        try
        {
            StatusText.Text = "Starting AirPlay Engine...";

            var airPlayCoreDirectory = FindAirPlayCoreDirectory();
            if (airPlayCoreDirectory is null)
            {
                UpdateStatus("Error: AirplayCore folder was not found.");
                return;
            }

            EnsureFfplayLauncher(airPlayCoreDirectory);

            var exePath = Path.Combine(airPlayCoreDirectory, "AirPlay.exe");
            if (File.Exists(exePath))
            {
                var logPath = Path.Combine(airPlayCoreDirectory, "AirPlayCore.log");
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = airPlayCoreDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _airPlayProcess = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };
                _airPlayProcess.OutputDataReceived += (_, e) => WriteCoreLog(logPath, e.Data);
                _airPlayProcess.ErrorDataReceived += (_, e) => WriteCoreLog(logPath, e.Data);
                _airPlayProcess.Exited += async (_, _) =>
                {
                    UpdateStatus("AirPlay connection closed.");
                    await System.Threading.Tasks.Task.Delay(1000);
                    if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            StartAirPlayCore();
                        });
                    }
                };

                _airPlayProcess.Start();
                _airPlayProcess.BeginOutputReadLine();
                _airPlayProcess.BeginErrorReadLine();

                UpdateStatus("AirPlay Engine Running! Ready for connections.");
            }
            else
            {
                UpdateStatus("Error: AirPlay.exe not found in AirplayCore folder.");
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error starting core: {ex.Message}");
        }
    }

    private static string? FindAirPlayCoreDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "AirplayCore"),
            Path.Combine(Environment.CurrentDirectory, "AirplayCore")
        };

        var found = candidates.FirstOrDefault(IsValidAirPlayCoreDirectory);
        if (found is not null)
        {
            return found;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "AirplayCore");
            if (IsValidAirPlayCoreDirectory(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static bool IsValidAirPlayCoreDirectory(string path)
    {
        return File.Exists(Path.Combine(path, "AirPlay.exe"));
    }

    private static void EnsureFfplayLauncher(string coreDirectory)
    {
        var ffplayPath = Path.Combine(coreDirectory, "ffplay.exe");
        if (File.Exists(ffplayPath))
        {
            return;
        }

        var ffplayRealPath = Path.Combine(coreDirectory, "ffplay_real.exe");
        if (!File.Exists(ffplayRealPath))
        {
            throw new FileNotFoundException("AirPlay video player is missing. Expected ffplay.exe or ffplay_real.exe.", ffplayPath);
        }

        File.Copy(ffplayRealPath, ffplayPath);
    }

    private string _currentAirPlayIp = "";
    private readonly object _logLock = new object();

    private void WriteCoreLog(string logPath, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        try
        {
            lock (_logLock)
            {
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}{Environment.NewLine}");
            }

            if (line.Contains("Client connected: "))
            {
                _currentAirPlayIp = line.Split(new[] { "Client connected: " }, StringSplitOptions.None)[1].Split(':')[0];
                UpdateStatus($"AirPlay device connected (IP: {_currentAirPlayIp}). Audio only or waiting for video.");
            }
            else if (line.Contains("Video player connected!"))
            {
                UpdateStatus($"AirPlay screen mirroring active (IP: {_currentAirPlayIp}).");
            }
        }
        catch (Exception ex)
        {
            // Logging must never take down the receiver, but write to debug if possible.
            System.Diagnostics.Debug.WriteLine($"Log error: {ex.Message}");
        }
    }

    private void UpdateStatus(string message)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            StatusText.Text = message;
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            StatusText.Text = message;
        });
    }

    private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
    {
        StopAirPlayCore();
        Application.Current.Shutdown();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Hide();
        MyNotifyIcon.ShowBalloonTip("AirReceiver", "Running in the background", BalloonIcon.Info);
    }

    private async System.Threading.Tasks.Task RefreshMiracastStatusAsync()
    {
        var status = await _miracastService.GetStatusAsync();
        await Dispatcher.InvokeAsync(() =>
        {
            MiracastStatusText.Text = status;
        });
    }

    private void OpenWirelessDisplay_Click(object sender, RoutedEventArgs e)
    {
        _miracastService.OpenWirelessDisplayReceiver();
        UpdateStatus("Wireless Display opened. Cast from Android Smart View or Windows Win+K.");
    }

    private void OpenProjectionSettings_Click(object sender, RoutedEventArgs e)
    {
        _miracastService.OpenProjectionSettings();
        UpdateStatus("Opened Windows projection settings.");
    }

    private void InstallWirelessDisplay_Click(object sender, RoutedEventArgs e)
    {
        _miracastService.OpenOptionalFeatures();
        UpdateStatus("Install the Wireless Display optional feature, then reopen Wireless Display.");
    }

    private void DisconnectAirPlay_Click(object sender, RoutedEventArgs e)
    {
        StopAirPlayCore();
        StartAirPlayCore();
        UpdateStatus("AirPlay connection disconnected. Ready for new connections.");
    }
    
    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var airPlayCoreDirectory = FindAirPlayCoreDirectory();
            if (airPlayCoreDirectory is null) return;
            var appSettingsPath = Path.Combine(airPlayCoreDirectory, "appsettings_win.json");

            if (File.Exists(appSettingsPath))
            {
                var json = File.ReadAllText(appSettingsPath);
                var root = JsonNode.Parse(json) as JsonObject;
                if (root != null)
                {
                    if (root["AirPlayReceiver"] is JsonObject receiver)
                    {
                        receiver["Instance"] = ReceiverNameTextBox.Text;
                    }
                    else
                    {
                        root["AirPlayReceiver"] = new JsonObject { ["Instance"] = ReceiverNameTextBox.Text };
                    }

                    var prefs = root["AirReceiverPrefs"] as JsonObject ?? new JsonObject();
                    prefs["Fullscreen"] = FullscreenCheckBox.IsChecked == true;
                    prefs["Borderless"] = BorderlessCheckBox.IsChecked == true;
                    prefs["AlwaysOnTop"] = AlwaysOnTopCheckBox.IsChecked == true;
                    root["AirReceiverPrefs"] = prefs;

                    File.WriteAllText(appSettingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                }
            }

            // Handle Startup
            var startupKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (StartupCheckBox.IsChecked == true)
            {
                startupKey?.SetValue("AirReceiver", Process.GetCurrentProcess().MainModule?.FileName ?? "");
            }
            else
            {
                startupKey?.DeleteValue("AirReceiver", false);
            }

            UpdateStatus("Settings saved! Restarting AirPlay engine to apply...");
            DisconnectAirPlay_Click(sender, e);
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error saving settings: {ex.Message}");
        }
    }

    private void FixFirewall_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "advfirewall firewall add rule name=\"AirReceiver AirPlay\" dir=in action=allow protocol=TCP localport=7000,5000",
                Verb = "runas", // Request Admin privileges
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(startInfo);
            UpdateStatus("Firewall rules updated for AirPlay ports (7000, 5000).");
        }
        catch (Exception)
        {
            UpdateStatus("Firewall fix cancelled or failed. Please run app as Administrator.");
        }
    }

    private void MenuItem_Open_Click(object sender, RoutedEventArgs e)
    {
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
    }

    private void MenuItem_FixFirewall_Click(object sender, RoutedEventArgs e)
    {
        FixFirewall_Click(sender, e);
    }

    private void MyNotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        MenuItem_Open_Click(sender, e);
    }
    
    protected override void OnClosed(EventArgs e)
    {
        StopAirPlayCore();
        base.OnClosed(e);
    }

    private void StopAirPlayCore()
    {
        try
        {
            if (_airPlayProcess is { HasExited: false })
            {
                _airPlayProcess.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
        finally
        {
            _airPlayProcess?.Dispose();
            _airPlayProcess = null;
        }
    }
}
