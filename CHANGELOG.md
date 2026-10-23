# Changelog

## 1.0.1-preview.1 — Unreleased performance candidate

- Reuse frame-history and accumulation textures instead of copying four full-resolution textures on every frame.
- Limit the Direct3D presentation queue to one frame to reduce queued-frame latency.
- Release desktop-duplication frames immediately before the next acquisition, following Microsoft's guidance to reduce redundant capture updates.
- Include synthetic moving-image hashes in offscreen test results for regression comparisons.

The candidate's five synthetic moving-image outputs match 1.0.0 byte for byte. Live desktop frame rate and latency have not been measured for this candidate. It still uses desktop capture and an overlay; this change does not provide game-renderer integration.

## 1.0.0 — 2026-10-23

Initial Windows x64 release.

### Features

- Desktop capture and a GPU-rendered, click-through motion blur overlay.
- Velocity based, Frame blending, Hybrid blending, Accumulation max, and Accumulation mix modes.
- Adjustable 0–100% blur amount in Velocity and accumulation modes.
- Mode-specific control states and Velocity-only refresh rate scaling.
- Blur off at startup, with Velocity based and 50% as the first-run defaults.
- Persistent mode, amount, and refresh scaling settings.
- Global toggle/off shortcuts, a system tray menu, and display-change handling.
- Portable, self-contained executable and offscreen shader checks.

### Known limitations

- SDR only; protected content and exclusive fullscreen applications may bypass the overlay.
- Initial single-display measurements were about 30–33 FPS in Velocity/Hybrid and 60–65 FPS in temporal modes.
- Motion estimation can produce artifacts. The live system cursor remains sharp above the captured trail.
- Multi-monitor and rotated-display support are implemented but not hardware-verified.
- The executable is not code-signed.
