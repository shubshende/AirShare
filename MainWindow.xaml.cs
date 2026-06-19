using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
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
        
        StartAirPlayCore();
        _ = RefreshMiracastStatusAsync();
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

    private void WriteCoreLog(string logPath, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        try
        {
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}{Environment.NewLine}");

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
        catch
        {
            // Logging must never take down the receiver.
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
