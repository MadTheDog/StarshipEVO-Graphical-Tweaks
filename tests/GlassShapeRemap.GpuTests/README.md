# General glass shape GPU prototype

This standalone D3D11 WARP fixture exercises synthetic data only. It does not
load the game, Unity, assets, saves, or the installed Graphical Tweaks plugin.
The new compute shader is not integrated into a bundle or production controller.
Run from the workspace root:

```powershell
& tools/Verify-GlassShapeRemapGpu.ps1
```

The script uses existing MSVC, Windows SDK FXC and the operating system's WARP
device. It installs nothing. Generated HLSL, DXBC, object files and executables
stay under ignored `local-evidence/shape-remap-gpu/`. FXC uses `/Ges /WX` and
MSVC uses `/W4 /WX`.

## Kernel and buffer contract

The kernel names are deliberately new: `GTShapeCopyNativeV1`,
`GTShapeBuildVariantsV1`, and `GTShapeRemapCompactV1`. A future host must require
all three names before enabling this path. It must not bind an old cube bundle
under this contract.

All triangle offsets below are triangle indices, not vertex indices or bytes.
Native triangle zero remains inactive and is never remapped. Every source shape
occupies its complete transparent pass, padded to a positive multiple of twelve
triangles. The source pool vertex count must be divisible by three.

| Resource | Register | Stride | Meaning |
| --- | --- | ---: | --- |
| `_GTShapeNativeRecords` | t0 |124| Immutable expanded native vertex pool |
| `_GTShapeTriangleFaceBits` | t1 |4| One source-local face-group bit per native triangle; zero means padding or an unmaskable surface |
| `_GTShapeClusterLookup` | t2 |16| `uint4` descriptor indexed by native cluster-start triangle |
| `_GTShapeBuildMasks` | t3 |8| `uint2(sourceLocalMask, expectedSourceShapeStartTriangle)` indexed by native build-data ID |
| `_GTShapeNativeMapping` | t4 |8| Native `uint2(clusterStartTriangle, buildDataId)` draw mapping |
| `_GTShapeNativeClipBits` | t5 |4| Bit view of native float clip distances |
| `_GTShapeOwnedRecords` | u0 |124| Owned native-pool clone followed by immutable appended variants |
| `_GTShapeOwnedMapping` | u1 |8| Owned compact draw mapping, separate for near/far |
| `_GTShapeOwnedClipBits` | u2 |4| Owned compact clip bits, separate for near/far |

Each 124-byte record is represented as seven `uint4` fields and one `uint3`.
The kernels never classify lighting normals or interpret UVs, material fields,
deformation fields or float values. Native bytes, including NaN payloads, survive
copying. A hidden triangle copies its entire first source record to all three
vertices so subsequent native deformation cannot reopen it. Visible records,
unmaskable surfaces and padding retain their complete original records.
Malformed multi-bit or out-of-group triangle labels retain that triangle.

Eligible source group IDs must be densely numbered from bit zero, with at most
eight groups. `validMask` is therefore one of1,3,7,15,31,63,127,255, and variant
count is `validMask + 1`. A shape can have many unmaskable curved facets while
exposing only a few planar cap groups. Do not allocate variants for every curved
facet. Source-local bits already account for CPU coverage of resized and rotated
polygons; the GPU does not perform an entity-axis rotation lookup.

For cluster `c`, the lookup entry at `sourceStart + 12*c` is:

```text
(variantBase + 12*c, paddedTriangleStride, validMask, sourceStart)
```

All other lookup entries default to zero. `variantBase` denotes mask zero for
the complete shape; the lookup X component denotes mask zero for THIS cluster.
The appended base must be at or after the native triangle count, with
`(variantBase - nativeTriangleCount) % 12 == 0`. Native source starts themselves
need not be globally divisible by twelve. A valid remapped cluster starts at:

```text
lookup.x + mask * lookup.y
```

The shader also checks source-relative cluster alignment, complete source and
variant extents, actual resource counts/strides and declared bounds. Addition
and multiplication follow subtraction/division checks so malformed counts do
not wrap into apparently valid ranges. Unknown descriptors, zero masks, masks
with ANY unknown bit, mismatched expected source shapes, invalid build IDs,
misalignment or bad geometry dimensions retain the native mapping. Invalid
mask bits are rejected as a whole rather than silently ANDed away. Clip bits
are copied unchanged even when remapping is rejected. Zero mapping remains zero.

Expected source identity protects a recycled build ID that now denotes another
shape. It cannot detect a recycled ID that denotes a different instance of the
same shape. The future CPU adapter must zero recycled IDs before publishing new
coverage, and must invalidate descriptors when native buffers are replaced.

## Parameters and recording order

The b0 constant buffer is exactly64 bytes, sixteen unsigned32-bit fields in
this order (Unity binds these fields by their names):

| Word | Name | Meaning |
| ---: | --- | --- |
|0| `_GTShapeSourceVertexCount` | Expected full native vertex buffer count |
|1| `_GTShapeOwnedVertexCount` | Expected full owned vertex buffer count |
|2| `_GTShapeWorkStart` | Copy/build work offset, in vertices |
|3| `_GTShapeWorkCount` | Work item count for this copy/build batch |
|4| `_GTShapeSourceTriangleStart` | Shape's source start, build kernel only |
|5| `_GTShapePaddedTriangleStride` | Complete padded shape range, build only |
|6| `_GTShapeVariantBaseTriangle` | First appended triangle of mask zero, build only |
|7| `_GTShapeValidMask` | Dense eligible group mask, build only |
|8| `_GTShapeMappingStart` | First native mapping/clip entry for this batch |
|9| `_GTShapeMappingOutputStart` | First compact output entry for this batch |
|10| `_GTShapeMappingCount` | Mapping/clip work count for this batch |
|11| `_GTShapeBuildMaskCount` | Published build mask count, bounded by actual dimensions |
|12| `_GTShapeLookupCount` | Published lookup count, bounded by actual dimensions |
|13..15| `_GTShapeReserved0..2` | Zero, reserved |

First copy the native pool once using `GTShapeCopyNativeV1`. Then dispatch
`GTShapeBuildVariantsV1` once per admitted shape (or in bounded batches), over
`3 * paddedTriangleStride * (validMask + 1)` records. Both kernels support
nonzero work offsets and leave work outside their range untouched. Validate
nonoverlapping appended ranges on the CPU before publishing any descriptors.
If initialization is incomplete or fails, retain native draws; the remap kernel
cannot infer whether otherwise valid descriptor metadata has been initialized.

Record `GTShapeRemapCompactV1` after native culling/sorting, immediately before
the corresponding native transparent draw in that same command buffer. Bind
the returned compact mapping at draw start index zero, or the explicit output
offset used for that draw. Keep the original indirect arguments and native draw
ordering. Large dispatches split at no more than65535 groups of64 threads, with
separate native input and compact output batch offsets. Actual mapping AND clip
buffer dimensions constrain writes together, including unequal buffer sizes.

All source buffers must be distinct from writable outputs. Near/far command
buffers must use separate output buffers or provably nonoverlapping ranges:
they may execute in either order. Current and retired owned resources remain
alive until every recording manager that references them has cleared or
detached its command buffers. This prototype supplies no lifetime controller,
GPU fencing, native hooks, CPU polygon proof, or production activation.

## Allocation budget

Use checked64-bit arithmetic on the host before allocation. With `V` native
vertices, shapes `i` having padded triangle strides `S_i` and dense masks `M_i`,
lookup length `L`, mask capacity `B`, and compact near/far capacities `N`/`F`:

```text
owned shape bytes = 124 * (V + sum(3 * S_i * (M_i + 1)))
triangle label bytes = 4 * nativeTriangleCount
lookup bytes = 16 * L
published mask bytes = 8 * B
compact mapping + clip bytes = 12 * (N + F)
outstanding owned bytes = all current allocations + all retained allocations
```

Check the complete planned allocation against the existing256MiB owned budget
before allocating, including temporary replacement resources while older ones
remain referenced. Unknown shapes require no variants. Budget exhaustion must
retain native rendering. This fixture's four synthetic shapes append13,536
records, or1,678,464 bytes. Its native pool clone adds89,280 bytes; an extra21
sentinel records make the test output1,770,348 bytes. These synthetic sizes do
not establish the game's actual memory requirement. The design inventory's
eleven proposed bounded shape/cap families append2,571,264 bytes before the pool
clone, masks, lookup, draw outputs and retirement costs.

## Verification and limits

The first RED run preserved the native pool and failed the complete-record
variant comparison with variant generation intentionally absent. The current
strict FXC/MSVC build passes seven behavioral groups:

- Raw native pool bytes, float NaN payloads, copy batches and untouched tails.
- Every mask for one-, two- and ten-cluster shapes, including eight groups,
  complete triangle collapse, padding and unmaskable facets.
- Malformed multi-bit and unknown triangle group labels retain triangles.
- Invalid shape descriptors, dimensions and overflowing work offsets write nothing.
- Compact multi-cluster remapping, zero/unknown native passthrough, mismatched
  recycled IDs, invalid masks, exact clip bits and immutable native inputs.
- Independent near/far storage in both execution orders, nonzero batch offsets,
  and growth without changing retained old allocations.
- Actual short source/output/lookup/mask/owned-shape buffers, corrupt descriptors,
  published counts, source/output tails and unsigned overflow guards.

Passing WARP verifies these compute operations on synthetic records. Live
expanded range/material identity, native vertex deformation parity, mixed-shape
polygon coverage, actual Unity bundle compilation and native command-buffer
execution remain separate integration gates. The installed0.6 candidate and
its existing cube remapper are unchanged.
