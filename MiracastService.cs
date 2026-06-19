using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AirReceiver;

public class MiracastService
{
    public async Task<string> GetStatusAsync()
    {
        try
        {
            var output = await RunPowerShellAsync(
                "$app = Get-StartApps | Where-Object { $_.AppID -eq 'Microsoft.PPIProjection_cw5n1h2txyewy!Microsoft.PPIProjection' -or $_.Name -eq 'Wireless Display' } | Select-Object -First 1; if ($app) { $app.AppID; exit 0 }; Get-AppxPackage -Name Microsoft.PPIProjection | Select-Object -First 1 -ExpandProperty PackageFullName");

            if (!string.IsNullOrWhiteSpace(output))
            {
                return "Miracast ready. Open Wireless Display, then cast from Android Smart View or Windows Win+K.";
            }

            return "Wireless Display receiver was not found. Install the Wireless Display optional feature first.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return "Open Windows projection settings to verify Miracast support.";
        }
    }

    public void OpenWirelessDisplayReceiver()
    {
        if (!TryStart("explorer.exe", "shell:AppsFolder\\Microsoft.PPIProjection_cw5n1h2txyewy!Microsoft.PPIProjection"))
        {
            OpenProjectionSettings();
        }
    }

    public void OpenProjectionSettings()
    {
        StartUri("ms-settings:project");
    }

    public void OpenOptionalFeatures()
    {
        StartUri("ms-settings:optionalfeatures");
    }

    private static bool TryStart(string fileName, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }

    private static void StartUri(string uri)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = uri,
            UseShellExecute = true
        });
    }

    private static async Task<string> RunPowerShellAsync(string command)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                ArgumentList = { "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", command },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(error);
        }

        return output;
    }
}
