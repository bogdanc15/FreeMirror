# FreeMirror

A free, open-source Windows desktop controller for the [UxPlay AirPlay receiver](https://github.com/leapbtw/uxplay-windows).
Mirror an iPhone or iPad's screen and audio to your PC over your local network.

## Features

- Requests 720p, 1080p, 1440p, 4K, or experimental 8K streams.
- Requests a maximum of 30 or 60 fps.
- Optional low-delay video mode.
- Full-screen button with window restoration, supporting Direct3D 11 and 12.
- No FreeMirror accounts, subscriptions, watermarks, session timers, or telemetry.

**Resolution and frame rate are requests, not guarantees.** Your Apple device
chooses the actual stream size and frame rate. 8K is an experimental 7680×4320
request: receiver startup is tested, but native 8K video delivery is unverified.
FreeMirror does not upscale or generate frames.

## Install

Requires **Windows 10/11, x64** and an Apple device with Screen Mirroring.

1. Download `FreeMirror-v0.1.0-win-x64.zip` from [Releases](https://github.com/bogdanc15/FreeMirror/releases).
2. Extract the whole ZIP to a writable folder. Do not run it from inside the ZIP.
3. Run `Setup-Receiver.ps1` with PowerShell. It downloads the pinned upstream
   receiver from its official GitHub release and verifies its SHA-256 checksum.
   It does not need administrator rights or change PowerShell policy.
4. Open `FreeMirror.exe`, choose your settings and click **Start receiving**.
5. On your iPhone/iPad, open **Control Center → Screen Mirroring → FreeMirror**.

The receiver is downloaded separately; it is not redistributed in this project's
ZIP. This keeps the upstream distribution and its license notices intact.
FreeMirror's .NET runtime is included, so installing .NET separately is unnecessary.
The app is unsigned. If Windows blocks it, inspect the source or build it yourself;
do not disable Windows security protections.

If Windows blocks PowerShell scripts, use the manual method instead: download
[uxplay-windows.zip from this exact release](https://github.com/leapbtw/uxplay-windows/releases/tag/2.0.0.1736),
check SHA-256 `9d3a51c15fc9db857351195e7eb7bbb21700d9ae25d936a54bcf8536b62cca18`,
and extract its contents into a folder named `receiver` beside `FreeMirror.exe`.

## Use and limitations

Both devices must be on the same trusted local network. Allow the receiver
through Windows Firewall on **private networks** if prompted. The upstream
receiver may offer to install Bonjour if it is missing. FreeMirror does not
install services or change firewall rules automatically.

Start with **1080p / 60 fps / Low delay**. Try 720p for less network and decoding
load. Low delay presents video without waiting for synchronization timestamps;
audio can drift, so turn it off for films if needed. Ethernet for the PC and
good Wi-Fi for the Apple device can help. Higher resolutions can increase delay.
Hardware decoding depends on the PC's GPU, codecs and drivers.

The picture opens in a separate window. Click **Full screen** while mirroring.
Use **Alt+Tab** to return to the controls and click **Exit full screen** to restore
the video window. Full screen does not change the incoming stream resolution.
Stop receiving before changing quality settings, then reconnect the Apple device.

This receiver does not enable a pairing PIN by default. Use a trusted network.
Protected streaming services may display a black picture. Xbox, Android casting,
remote internet access, and guaranteed 4K/8K at 60 fps are outside this release.

FreeMirror temporarily uses UxPlay's per-user arguments file, restoring it on
normal stop. Do not run a separate uxplay-windows instance or edit its arguments
while FreeMirror is receiving. If the controller is forcibly terminated, quit
the receiver from its tray icon. See [SECURITY.md](SECURITY.md) for configuration
and privacy considerations.

## Build and test

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.

```powershell
.\scripts\Build.ps1
```

The script builds a self-contained x64 controller, runs the self-checks, and
creates the distributable ZIP and checksum under `artifacts`. It includes the
controller source and license, and excludes screenshots, diagnostics, symbols,
local receiver files and Git history.

For an optional receiver integration test, extract the output ZIP, run its
`Setup-Receiver.ps1`, close other receivers, then run:

```powershell
$test = Start-Process .\FreeMirror.exe -ArgumentList '--integration-test' -PassThru -Wait
Get-Content .\test-result.txt
if ($test.ExitCode -ne 0) { throw 'Integration checks failed' }
```

These checks cover option validation, real receiver startup, RTSP responses,
full-screen/window restoration and configuration restoration. They do not prove
sender compatibility or measure video resolution, delivered fps or latency.
The Direct3D 12 full-screen fix has also been exercised on a live mirrored window.

## Privacy and contributions

The default receiver name is simply **FreeMirror**. The controller does not use
the Windows username or computer name in its default identity. It does not upload
diagnostics. Setup contacts GitHub to download the upstream receiver. The upstream
receiver and Bonjour handle local device discovery and streaming separately.

Before submitting an issue, redact usernames, computer names, local paths, device
identifiers, addresses and any content visible in a mirrored screen. Do not post
credentials or raw logs. See [CONTRIBUTING.md](CONTRIBUTING.md).

## Credits and license

FreeMirror is independently written controller code, released under
**GPL-3.0-or-later**; see [LICENSE](LICENSE). It was developed with AI assistance.
It does not contain proprietary Microsoft Store app code or assets.

The actual AirPlay implementation is provided by [UxPlay](https://github.com/FDH2/UxPlay),
the Windows integration by [uxplay-windows](https://github.com/leapbtw/uxplay-windows),
and video/audio processing by their dependencies. Their authors retain credit
and their respective licenses. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Apple, AirPlay, iPhone, iPad and Windows are trademarks of their respective owners.
This project is not affiliated with or endorsed by Apple or Microsoft.
