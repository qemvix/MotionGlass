# Troubleshooting

[Back to MotionGlass](../README.md)

## The desktop is too blurry or trails linger

Turn the effect off with **Ctrl + Alt + Esc**, the main switch, or the tray menu. Closing the app also removes the overlay. Lower the amount in an adjustable mode, or choose another mode. Frame blending and accumulation intentionally produce ghosting.

## The effect feels less smooth than the display

The overlay adds capture and presentation work. Its frame rate may be below your display's refresh rate. Initial 1080p measurements were around 30–33 FPS for Velocity/Hybrid and 60–65 FPS for the temporal modes. GPU load and multiple displays can change these results.

Use the footer's FPS reading to judge the current rendering rate. Refresh rate scaling changes blur exposure, not rendering speed.

## A control is greyed out

This is intentional:

- Frame and Hybrid blending disable both amount and refresh scaling because their effect settings are fixed.
- Accumulation modes allow amount changes but disable refresh scaling.
- Velocity based allows both controls.

## A display is paused or shows no effect

Read the status message. HDR displays are paused; MotionGlass's current pipeline supports SDR. Locking the desktop or changing display configuration can interrupt capture. Reopen the controls and toggle the effect off and on after returning to the normal desktop if capture does not resume.

Protected video and secure desktops are outside the intended capture path. Exclusive fullscreen apps may appear above the overlay; borderless/windowed applications are the intended use.

## The cursor itself is still sharp

Windows draws the live system cursor above the desktop overlay. MotionGlass adds a captured trail beneath it and keeps the live pointer responsive. Some legacy monochrome or inverted cursor shapes may look different in the trail.

## A shortcut does not work

Another application may own the same shortcut. The controls window reports unavailable shortcuts. Use the main power switch or the system tray menu instead.

## The controls disappeared

Minimizing sends the controls to the system tray. Double-click the MotionGlass icon to reopen them. Starting a second copy reports that the app is already running.

## Reporting a problem

Use the [bug report form](https://github.com/qemvix/MotionGlass/issues/new?template=bug_report.yml). Include the app version, Windows version, GPU, display settings, mode, and reproducible steps. Do not post personal information from desktop captures.
