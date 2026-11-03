# Standalone shadow receiver GPU checks

Run `./tools/Verify-ShadowVisibilityGpu.ps1` from the repository root. It uses the
installed MSVC compiler, Windows SDK FXC and D3D11 WARP, without Unity activation,
game bytecode, extracted game shaders, native game assets or a running game.
Only original synthetic metadata, depth values and fullscreen geometry are used.
The shared `GlassSurface.GpuTests/FixtureResources.h` supplies resource helpers;
no game-dependent resource from that other fixture is used here.

The wrapper directly includes production `shaders/ShadowVisibility.cginc`.
Normal depth omits `UNITY_REVERSED_Z` entirely; reversed depth defines it to 1.
The fixture binds 25x1 RGBA32Float metadata and a 4x4 R32Float depth atlas (4x2 for
the horizontal two-cascade layout), then
reads the receiver result from a synthetic 1x1 floating-point render target.
Output is initialized to -13 before each draw so an empty draw cannot pass a
zero-visibility case. D3D reflection supplies constants and texture bindings.

The 55 cases per convention cover physical blocker/receiver ordering, equal
depth, positive and negative bias, strength saturation, equal/unequal PCF
weights, outer atlas clamping, readiness, both halves of both event stamps,
camera identities, marker/missing stamp rejection, zero dimensions, invalid
UV/depth/W, nontrivial matrix rows, 2/4-cascade selection, nested and excluded
spheres, camera-relative distance fade, and combined fade/strength.
Expected visibility values are explicit constants derived from the test setup,
not a CPU implementation of the shader. The run requires blocked, lit and
fractional outcomes, with a tolerance of 0.00002 on every output component.
The original 110 scalar cases are retained. Another 38 cases test Detailed
invalid/cascade/atlas/sample/distance statuses, including physically lit versus
fail-open lit and evaluation-order precedence.
The original multi-cascade cases now use realistic tile-center transforms and
atlas dimensions while retaining their original expected visibility/status.
Another 38 tile/native-metadata cases run in both scalar/detailed and
normal/reversed variants, for 152 additional executions. They test left/right
and 2x2 corner tap isolation,
receiver exclusion from its selected tile, permuted cascade ordering, unknown
aspect ratios, duplicate/off-center/nonfinite transforms, radius validation,
stabilized center tolerance, and actual-bound versus exported dimensions.
One case uses exact captured native sphere/UV-row constants and a 4096x2048
synthetic depth atlas: it failed under the original quarter-pixel center guard
and passes with the 1.5-pixel allowance. Additional cases bracket 1.5 pixels;
tiny maps retain a quarter-tile safety cap so tile identity remains strict.
An additional 25 cases per depth convention exercise the diagnostic-only invalid
reason helper and its probe encoding, bringing the complete run to 350 cases.
They distinguish unarmed, individual frame/camera/marker failures, metadata and
bound-atlas dimensions, cascade/radius/center/layout failures, and leave valid or
spatially excluded production statuses unchanged. Diagnostic variants compile
with `/Gec /WX` as well as the existing strict production wrapper checks.

The exact 25-texel native snapshot includes inactive sphere/radius `-Infinity`
lanes and uses a real 4096x2048 synthetic depth atlas. It reproduced visibility
1/status 100 before inactive-lane sanitization, then visibility 0/status 3 after
the fix. Inactive positive infinity/NaN also remain harmless, while invalid
ACTIVE radii in two/four-cascade layouts still fail open. No active validity
check was removed. `-CompilerMode Legacy` repeats the full suite using `/Gec`
for production wrappers; default `Strict` uses `/Ges`. Both modes passed all 350
cases. `-DiagnosticOnly` runs only the reason variants when isolating a failure.

Build products and `results.txt` go to ignored
`local-evidence/shadow-visibility-gpu/`. These tests establish synthetic receiver
behavior only. They do not prove native atlas retention, Unity event timing,
native matrix contents, four-cascade packing,
opaque rendering preservation or visual parity in the actual game.
