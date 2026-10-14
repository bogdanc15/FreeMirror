# Changelog

## 0.1.0

- Windows x64 controller for the separately downloaded UxPlay receiver.
- Resolution requests from 720p to 4K, plus experimental 8K.
- 30/60 fps requests and optional low-delay mode.
- Full-screen toggle for Direct3D 11 and Direct3D 12 video windows.
- Active-mirroring status and normal-stop configuration restoration.
- Privacy-focused public source export, checked setup download and build scripts.

Known limits: native 8K output and end-to-end latency are unmeasured; actual
stream parameters depend on the sender. Releases are unsigned. Force-closing
the controller can leave temporary upstream settings in place.
