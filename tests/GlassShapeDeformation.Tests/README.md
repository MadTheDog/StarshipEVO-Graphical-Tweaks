# Isolated fixed glass position deformation

The pure immutable API is currently in
`GlassShapeDeformationModel.prototype.cs` beside this standalone test project.
It is intentionally outside `src/GraphicalTweaks` so SDK wildcard compilation
cannot activate it in the plugin. It uses `System` only, with no Unity types,
game references, native shader bytes or embedded mesh data. Nothing in this
directory changes runtime admission or the accepted cube path.

Run the synthetic tests from the project root:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.tools/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PWD '.tools/nuget'
& .tools/dotnet/dotnet.exe run --project tests/GlassShapeDeformation.Tests -c Release -p:NuGetAudit=false
```

Optionally append `-- local-evidence/glass-catalog-20261102-163138-9091255`
to validate the ignored live source records/material declarations and all 32
selectable actual matrices. An additional output filename exports a private
binary fixture consumed by `tools/Verify-GlassShapeDeformationGpu.ps1`.
Live input and exports stay ignored; do not commit or publish them.

## API and validation

`GlassShapeDeformationModel.TryDeform(input, out entityLocalPosition)` computes
the shader's `rendererPosition`: deformation, brick unit, actual rotation, then
brick position. Entity/object/world transforms are outside this API. Positions
are floats throughout. The matrix constructor copies sixteen logical row-major
coefficients into readonly fields and retains no mutable array. Native HLSL
constant-buffer storage needs a separate transpose to column-major packing;
the GPU fixture performs that storage conversion without altering coefficients.

The input includes source position/normal, whole-mesh bounds center/size,
material controls, explicit X/Y/Z counts, brick unit, rotation and brick position.
`DecodeRotationIndex(rotationAndMaterial)` returns its low five bits, matching
the shader; indices 24 through 31 in this capture are identity matrices.

Only ordinary alpha 2, border index -1, vertex mode 0, building offset 0 and
resize linkage 0/free, 7/radiusXY or 8/radiusXYZ are admitted. Offset-normal
mode 3 and every animated/unknown vertex mode are rejected. A finite source
normal is required even though vertex mode 0 leaves position independent of it.
Counts must be 0 through 15, units positive finite, bounds sizes positive finite,
and finite thresholds ordered low <= high. No arbitrary maximum coordinate or
unit cap is imposed; nonfinite input/intermediate/output rejects the operation.
On failure the output is the default zero vector.

Matrices must be finite, affine with exact `(0,0,0,1)` bottom row and zero
translation, and proper rigid rotations within 1e-5 in double row norms, row
dot products and determinant. This is an admission check, never geometry
canonicalization. It rejects zero/scale/shear/reflection inputs and preserves
the native residual coefficients. It does not authenticate matrix provenance:
the integration owner must supply the correct live table entry and validate
the native table's identity/lifetime. Shape-specific maximum counts also remain
the caller's responsibility; the shader model tests all nibble values through
15, including stress cases beyond some shape declarations' build limits.

Free linkage uses strict `>` threshold comparisons on position relative to the
whole-mesh center. At low it adds zero; at high it adds half the extension when
low < high. Equal thresholds have no half-extension interval. Radial linkage
uses the lower whole-mesh bound as anchor. Radius exactly 0.01 is unchanged;
strictly larger radii add raw count X, independent of material resize unit.
RadiusXY adds count Z only where radial Z is strictly greater than 0.5; count Y
is ignored. RadiusXYZ ignores both Y and Z counts. These details deliberately
follow our previously validated shared HLSL, rather than a generic AABB scale.

## Evidence and limits

The initial stub returned false for all inputs; the first supported free-resize
test failed with `admitted input rejected` before implementation. Ignored logs
`local-evidence/glass-deformation/01-red.txt` and `02-green.txt` preserve that
red/green sequence. The synthetic suite covers strict threshold boundaries,
nonzero centers/anchors, radial center and 0.01 boundary, count/unit extremes,
rotation order/orientation, unsupported materials/modes, every matrix coefficient
with nonfinite values, invalid matrices, overflows and immutable snapshots.

The optional live suite validates all eleven clear-family source captures,
their complete padded CPU/GPU byte equality, zero padding and actually used
material IDs. It exercises five count/unit/translation combinations at all 32
selectable indices. It never snaps native floats. This is position modeling,
not coverage admission, lifecycle integration, a seam-removal implementation or
runtime acceptance. Native GPU/hardware and arbitrary input parity remain
unproven by CPU tests.
