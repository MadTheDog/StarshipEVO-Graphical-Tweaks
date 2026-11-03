# CPU versus shared glass geometry on WARP

Run `tools/Verify-GlassShapeDeformationGpu.ps1` from the project root. It uses
the existing .NET SDK, MSVC, Windows SDK FXC, Unity headers and operating system
D3D11 WARP; it installs nothing and launches no game/GUI. Generated fixture,
DXBC, disassembly, object, executable, hashes and logs stay in ignored
`local-evidence/glass-deformation/`. The default input is the ignored live
catalog `glass-catalog-20261102-163138-9091255`. No original native shader
bytecode or mesh bytes are embedded in this source or loaded as shader code.

The CPU test executable reads live expanded records and material declarations,
validates their eligibility, computes expected float positions and exports a
private bounded fixture. The original C++ driver uploads those records,
actual material controls and all 64 unrounded matrices (only indices 0..31 are
selected), and compiles our unchanged `GlassSurfaceGeometry.hlsl` using the
same `/Gec /WX` settings as the prior geometry parity fixture. It captures
`rendererPosition` by a test-only geometry shader. Entity/object transforms are
identity; brick unit/rotation/position are exercised separately.

The run contains 1,760 draws / 121,920 live-source vertex cases, covering eleven
clear-family shapes, all 32 packed rotation indices and five combinations:

| Counts X/Y/Z | Brick unit | Brick position |
| --- | --- | --- |
| 0/0/0 | 1 | 0/0/0 |
| 1/2/3 | 0.25 | 10/-2/3 |
| 15/15/15 | 1 | 0.125/-0.25/0.5 |
| 15/0/7 | 0.125 | 8192/-8192/4096 |
| 4/15/2 | 16 | 0/0/0 |

Count 15 is deliberate shader-input stress for arches whose legal build
limits can be lower; it is not a claim those native declarations allow it.

The driver reports numeric equality, bit equality (including zero signs),
ordered-float ULP distance, maximum absolute component error and Euclidean
position distance. It separates free/radiusXY/radiusXYZ results. Its 0.01-unit
sanity bound only makes gross fixture errors fail execution; it is **not** a
coverage tolerance and does not certify a numerical difference as acceptable.
Review the actual counters. A successful process alone establishes no exact
parity claim.

The CPU implementation uses ordinary float multiplication/addition and no
explicit fused multiply-add. FXC emits `mad` instructions, as well as `sqrt`
and `div` for the radial lanes; WARP behavior is measured rather than inferred
from instruction names. Results do not establish the same rounding on every
hardware/backend/compiler or for inputs outside the measured cases. Our shared
HLSL previously had native shader validation; this fixture measures CPU versus
that HLSL and does not rerun native shader parity. No runtime glass variants,
coverage policy, entity lifecycle or deployment is activated.

The 2026-11-02 local run produced 365,760/365,760 numerically equal **and bit
equal** components, with zero ULP/component/distance error. Per-linkage component
totals were free 129,600, radiusXY 63,360 and radiusXYZ 172,800. The pure suite
reported 436 checks; the optional live run reported 135,996 checks including
per-byte zero-padding validation. The detailed log and input/source hashes are
`local-evidence/glass-deformation/05-warp-final.txt` and `hashes.json`.
