# Isolated glass contact policy prototype

This executable compile-links the existing deformation prototype and generic
exact coverage engine. It does not change rendering, native records, collisions,
source/shader projects, installations or runtime behavior.

Captured 209 coverage fixtures separately verify its real profile mismatches
against207 and a rotated209 cap. Both compared caps remain visible; replacing
the neighbour with an identical opposing cap makes both hidden. Three actual
unit-cube unions cover209 caps, while smaller2x2 footprints retain protrusions
and every covering cube retains its exposed opposite cap.

Run using the existing SDK from the Graphical Tweaks root:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.tools/dotnet-home'
.\.tools\dotnet\dotnet.exe run --project tests/GlassShapeContact.Tests/GlassShapeContact.Tests.csproj
.\.tools\dotnet\dotnet.exe run --project tests/GlassShapeContact.Tests/GlassShapeContact.Tests.csproj -- local-evidence/glass-catalog-20261102-163138-9091255
```

The first command runs synthetic guards without private assets. The second
additionally reads the existing private capture; it never writes capture bytes.
Optional final argument `--joins-only` skips the large vertex sweep.

`GlassShapeContactCanonicalizer.TryCreate` freezes a deterministic dictionary
over absolute SOURCE position components. Sorted clusters have diameter at most
1e-6; their representative is an existing exact half integer, otherwise their
minimum. Input is bounded to 65,536 positions and absolute components <=64.
`TryCanonicalize` accepts only known source magnitudes, restoring signs.

`TryCanonicalizeRadialAnchor` is an explicit extension ONLY for the derived
radial CONTACT anchor: the nearest canonical source magnitude must lie within
1e-6. The adapter reconstructs a contact bounds center with unchanged bounds
size, then requires its derived float anchor to equal the canonical anchor
exactly. Free-mode native bounds remain unchanged. Actual GPU bounds never
change. This exception is necessary for resized 209's -Z cap.

`GlassShapeContactModel.TryCanonicalRotation` accepts only an exact homogeneous
affine row/column and a proper signed permutation within 2^-23 per coefficient.
The caller still authenticates resource identity and native rotation index.
It does not replace the native matrix.

`TryDeform(rawInput, policy, exactPlacement, out contact)` validates a separate
native integer/32 placement within +/-1,048,576, matching the supplied raw float
placement after float conversion. It rejects free threshold branch changes,
radial .01 branch changes and radiusXY axial .5 branch changes. Both local
deformations use the existing adapter, identity rotation and zero translation;
local Euclidean displacement must be <=2e-6*(1+maxCount)*brickUnit. A canonical
signed permutation and exact double placement are then applied without float
translation rounding. Unsupported/unsafe inputs return false with a zero point.

This is an explicit semantic approximation for intended contact, not proof that
the rendered triangles coincide. A tiny original source sliver may therefore
be classified as contact-equivalent. The policy preserves real .0025 profile
differences and never snaps arbitrary placements or outputs. Exact polygon
union still decides all coverage after contact geometry is constructed.

Integration must validate the complete fixed source catalog, topology, material
controls, generation ownership, counts and plane/group membership. Changing a
catalog requires rebuilding/reviewing its policy rather than silently adding
new dictionary values. This executable is not runtime/GPU acceptance.
