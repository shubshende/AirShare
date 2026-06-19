# AirReceiver Release

## Build

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true -o .\publish\AirReceiver-win-x64
```

The publish output must be shipped as a folder because `AirplayCore` contains native binaries, receiver config, and the `ffplay` launcher.

## Installer

Install Inno Setup, then run:

```powershell
iscc .\installer\AirReceiver.iss
```

The installer output is written to:

```text
dist\AirReceiver-Setup-1.0.0.exe
```

The installer adds inbound Windows Firewall rules for TCP 5000, TCP 7000, and UDP 5353.
