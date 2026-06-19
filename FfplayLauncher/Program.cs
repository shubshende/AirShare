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
var profile = ReadProfile(appDirectory);
var playbackArgs = inputPath is null
    ? args
    : BuildPlaybackArgs(profile, inputPath);

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

using var ffplay = Process.Start(startInfo);
if (ffplay is null)
{
    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Failed to start ffplay_real.exe{Environment.NewLine}");
    return 3;
}

ffplay.WaitForExit();
return ffplay.ExitCode;

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

static string[] BuildPlaybackArgs(string profile, string inputPath)
{
    var common = new[]
    {
        "-window_title", "AirReceiver Video",
        "-alwaysontop",
        "-left", "80",
        "-top", "40",
        "-x", "520",
        "-f", "h264"
    };

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

    return common.Concat(profileArgs).Append(inputPath).ToArray();
}
