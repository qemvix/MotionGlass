# Changelog

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
