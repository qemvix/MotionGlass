# Validation

[Back to MotionGlass](../README.md)

## Offscreen checks

The release self-test uses a WARP software Direct3D device and synthetic images. It checks:

- Static-image preservation in each of the five modes.
- Successful rendering of a moving synthetic rectangle in each mode.
- Distinct moving-image output between every pair of modes.
- Unmodified image pixels at 0% in Velocity, Accumulation max, and Accumulation mix.

Run `scripts/Test.ps1`, or invoke the portable executable with `--self-test <output-folder>`. The test writes `self-test.json`, exits with a failure code when a check fails, and does not enable desktop capture or desktop blur.

## Initial manual/integration observations

The initial development session checked the controls' enable/disable rules for all five modes. A brief live test on one 1920×1080 Windows display exercised each mode and checked that 0% and power off removed the effect. The process exited after the test.

Observed rendering rates were approximately 30 FPS for Velocity, 33 FPS for Hybrid, 65 FPS for Frame blending, and 62 FPS for each accumulation mode. These short measurements are not benchmarks or performance guarantees.

## What is not established

- Multi-monitor behavior and display rotation have not been tested on hardware.
- The test suite does not establish compatibility with every game, capture application, cursor shape, protected-content path, or graphics driver.
- Synthetic frame checks are not a substitute for visual quality review of motion estimation.
- The executable is unsigned; no code-signing or installer validation is claimed.

The automated Windows workflow builds and runs the offscreen checks only. Live effect tests should remain explicit and supervised.
