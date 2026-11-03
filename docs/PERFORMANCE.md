# Performance measurements

These tests used one isolated SEVO scene at 2560×1440. Rendering changes vary
with the scene, hardware, temperatures and selected settings.

## Experimental decal CPU optimisation

Four camera-turning runs used 10× decal visibility and the normal native yaw
path. The experimental option was switched off/on/off/on.

| Run | Option | Mean FPS during camera turns | Mean decal CPU envelope |
|---|---|---:|---:|
| 1 | Off | 30.23 | 22.89 ms |
| 2 | On | 38.54 | 17.40 ms |
| 3 | Off | 29.69 | 23.54 ms |
| 4 | On | 38.92 | 16.82 ms |

The paired FPS increases were 27.5% and 31.1%. These measurements predate the
1.0.0 name/wording update and use the same decal implementation. Stationary
tails stayed around 63–65 FPS, while the decal CPU envelope increased from
0.37–0.38 ms to 1.76–1.80 ms with the option enabled. No garbage collection
occurred during the turning intervals. CPU envelopes include submission and
wait time.

The option stays experimental and off by default because it adds idle work and
moving-grid/large-world coverage remains incomplete. Native GPU decal tile
work still increases with higher visibility ranges.

## Other features

Native AO tests reduced four duplicate SSAO attachments to one, removed the
attachment when AO was disabled, and restored one on re-enable. Chosen AO
quality remained unchanged. A general FPS percentage was not isolated.

Empty-pass optimisation skips culling dispatches for passes with no elements.
Glass corrections, shadow settings and decal range have no representative
large-world FPS percentage. Earlier captures included GPU thermal/clock drift.
