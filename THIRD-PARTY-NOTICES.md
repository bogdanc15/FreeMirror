# Third-party components

## FreeMirror controller

Copyright (c) 2026 bogdanc15. GPL-3.0-or-later; see LICENSE.

## .NET runtime

Self-contained releases include the Microsoft .NET and Windows Desktop runtimes.
Their MIT licenses and applicable third-party notices are copied from the exact
restored runtime packages into the release's `licenses` folder by Build.ps1.

- https://github.com/dotnet/runtime
- https://github.com/dotnet/windowsdesktop
- https://github.com/dotnet/winforms

## Receiver downloaded by the user

The FreeMirror repository and release do not redistribute uxplay-windows,
UxPlay, Qt, GStreamer, FFmpeg, mDNSResponder, or their binary dependencies.
Setup-Receiver.ps1 retrieves the unmodified official upstream ZIP for local use.
Upstream license notices remain in that downloaded distribution.

Pinned artifact:

- Release: https://github.com/leapbtw/uxplay-windows/releases/tag/2.0.0.1736
- Source: https://github.com/leapbtw/uxplay-windows/tree/2.0.0.1736
- UxPlay engine source: https://github.com/leapbtw/libuxplay/tree/437f37514257d9cb513ac7fbdee743b4da85852e
- Original UxPlay: https://github.com/FDH2/UxPlay
- SHA-256 of uxplay-windows.zip: `9d3a51c15fc9db857351195e7eb7bbb21700d9ae25d936a54bcf8536b62cca18`

uxplay-windows is GPLv3; its distribution includes components with other licenses.
Consult its LICENSE.rtf and source for details. If you choose to redistribute
the receiver yourself, you are responsible for the licenses and corresponding
source of that distribution. This project's release automation intentionally
does not bundle those receiver binaries.
