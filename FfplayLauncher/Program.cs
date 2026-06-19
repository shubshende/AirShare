using System.Diagnostics;

var appDirectory = AppContext.BaseDirectory;
var realFfplayPath = Path.Combine(appDirectory, "ffplay_real.exe");
var logPath = Path.Combine(appDirectory, "ffplay_launcher.log");

if (!File.Exists(realFfplayPath))
{
    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Missing {realFfplayPath}{Environment.NewLine}");
    return 2;
}

var inputPath = args.LastOrDefault(arg => !arg.StartsWith('-') && arg.Contains("AirPlayVideo", StringComparison.OrdinalIgnoreCase));
if (inputPath is null && args.Length == 0)
{
    inputPath = @"\\.\pipe\AirPlayVideo";
}
var profile = ReadProfile(appDirectory);
var playbackArgs = inputPath is null
    ? args
    : BuildPlaybackArgs(appDirectory, profile, inputPath);

var startInfo = new ProcessStartInfo
{
    FileName = realFfplayPath,
    WorkingDirectory = appDirectory,
    UseShellExecute = false
};

foreach (var arg in playbackArgs)
{
    startInfo.ArgumentList.Add(arg);
}

startInfo.Environment["SDL_VIDEO_WINDOW_POS"] = "80,40";
startInfo.Environment["SDL_VIDEO_CENTERED"] = "0";

File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Original args: {string.Join(' ', args.Select(Quote))}{Environment.NewLine}");
File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Profile: {profile}{Environment.NewLine}");
File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Starting ffplay_real.exe {string.Join(' ', startInfo.ArgumentList.Select(Quote))}{Environment.NewLine}");

using var process = Process.Start(startInfo);
if (process is not null)
{
    process.WaitForExit();

    // UX Enhancement: If the video window closes, kill the AirPlay engine 
    // to force the iOS device to disconnect completely.
    var airPlayProcs = Process.GetProcessesByName("AirPlay");
    foreach (var p in airPlayProcs)
    {
        try { p.Kill(); } catch { }
    }

    return process.ExitCode;
}

File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Failed to start ffplay_real.exe{Environment.NewLine}");
return 3;

static string Quote(string value)
{
    return value.Contains(' ') || value.Contains('"')
        ? $"\"{value.Replace("\"", "\\\"")}\""
        : value;
}

static string ReadProfile(string appDirectory)
{
    var profilePath = Path.Combine(appDirectory, "ffplay_profile.txt");
    if (!File.Exists(profilePath))
    {
        File.WriteAllText(profilePath, "fast");
        return "fast";
    }

    var profile = File.ReadAllText(profilePath).Trim().ToLowerInvariant();
    return profile is "stable" or "balanced" or "fast" ? profile : "fast";
}

static string[] BuildPlaybackArgs(string appDirectory, string profile, string inputPath)
{
    var commonList = new System.Collections.Generic.List<string>
    {
        "-window_title", "AirReceiver Video",
        "-left", "80",
        "-top", "40",
        "-x", "520",
        "-f", "h264"
    };

    try
    {
        var appSettingsPath = Path.Combine(appDirectory, "appsettings_win.json");
        if (File.Exists(appSettingsPath))
        {
            var json = File.ReadAllText(appSettingsPath);
            var root = System.Text.Json.Nodes.JsonNode.Parse(json);
            var prefs = root?["AirReceiverPrefs"];
            if (prefs != null)
            {
                if (prefs["Fullscreen"]?.GetValue<bool>() == true) commonList.Add("-fs");
                if (prefs["Borderless"]?.GetValue<bool>() == true) commonList.Add("-noborder");
                if (prefs["AlwaysOnTop"]?.GetValue<bool>() == true) commonList.Add("-alwaysontop");
            }
        }
    }
    catch { } // Default to no extra flags if parsing fails

    var profileArgs = profile switch
    {
        "stable" => new[]
        {
            "-probesize", "10485760",
            "-analyzeduration", "1000000",
            "-fflags", "+genpts",
            "-flags", "low_delay",
            "-sync", "video"
        },
        "balanced" => new[]
        {
            "-probesize", "1048576",
            "-analyzeduration", "100000",
            "-fflags", "+genpts",
            "-flags", "low_delay",
            "-sync", "video"
        },
        _ => new[]
        {
            "-probesize", "32768",
            "-analyzeduration", "0",
            "-fflags", "+genpts",
            "-flags", "low_delay",
            "-vf", "setpts=0",
            "-sync", "ext"
        }
    };

    return commonList.Concat(profileArgs).Append(inputPath).ToArray();
}
