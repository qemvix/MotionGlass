# User guide

[Back to MotionGlass](../README.md)

## First launch

Download the portable ZIP from [Releases](https://github.com/qemvix/MotionGlass/releases/latest), extract it, and run `MotionGlass.exe`. The .NET runtime is included. There is no installer or administrator requirement.

The power switch starts **off**. The first-run mode is **Velocity based**, amount **50%**, with refresh rate scaling **off**. Later launches restore mode settings while keeping the effect off.

Turn the effect on only when you want it. The footer reports live rendering status or explains why a display is paused.

## Amount and modes

### Velocity based

Estimates movement between successive screen images, then samples along that direction to produce blur. The amount sets the exposure length relative to motion between frames. At 0%, the overlay is hidden and processing pauses.

Refresh rate scaling adjusts exposure using the rendering interval and a 60 Hz reference. It helps keep a similar exposure duration when the rendering rate changes. It does not increase the frame rate or change Windows display settings.

### Frame blending

Blends the current frame and three previous frames with weights of 40%, 30%, 20%, and 10%. This creates overlapping images along moving edges. The blend is fixed, so the amount slider and refresh scaling switch are greyed out.

### Hybrid blending

Mixes the four-frame blend with directional blur. It uses a fixed exposure and mix, so the amount and refresh scaling controls are greyed out.

### Accumulation max

Takes the per-channel maximum of the current image and fading accumulated history. Bright objects leave light trails. Dark objects on bright backgrounds may leave little visible trail. The amount controls how long history persists; refresh scaling is greyed out.

### Accumulation mix

Mixes new frames with exponentially fading history to produce longer, softer trails. The amount controls persistence, and refresh scaling is greyed out. At 100%, history continues to fade; it does not freeze permanently.

## Turning blur off

- Use the main power switch.
- Press **Ctrl + Alt + Esc** to turn it off, or **Ctrl + Alt + B** to toggle it.
- Right-click the system tray icon and select **Turn off** or **Exit**.
- Close the main window to exit completely.

Minimizing hides the controls while leaving the app in the tray. Double-click the tray icon to reopen it. The app reports a shortcut conflict if another application has already registered one of the shortcuts.

## Saved settings

Mode, amount, and refresh scaling are stored in `%LOCALAPPDATA%\MotionGlass\settings.json`. Disabled controls retain their values. **Reset defaults** restores Velocity based, 50%, and scaling off; the power switch is separate.

## Displays and content

MotionGlass creates one worker for each connected display. SDR content is supported. If HDR is active on a display, that display is paused. Capture loss hides the affected overlay; reconnection triggers reinitialization.

The operating system's current pointer stays sharp above the overlay, while captured cursor images provide the trail. Secure desktops, protected video, and exclusive fullscreen content can bypass the effect. Normal windows and borderless applications are the intended use.

No frames are recorded to disk or uploaded. The app does not install a background service or add a Windows startup entry.
