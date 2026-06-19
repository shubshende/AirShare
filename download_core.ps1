$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$release = Invoke-RestMethod -Uri "https://api.github.com/repos/YimingZhanshen/Airplay2OnWindows/releases/latest"
$asset = $release.assets | Where-Object { $_.name -match '\.zip$' } | Select-Object -First 1

if ($asset) {
    Write-Host "Downloading $($asset.name) from $($asset.browser_download_url)..."
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile "AirplayCore.zip"
    Write-Host "Extracting..."
    Expand-Archive -Path "AirplayCore.zip" -DestinationPath "AirplayCore" -Force
    Write-Host "Done!"
} else {
    Write-Host "Could not find a zip asset in the latest release."
}
