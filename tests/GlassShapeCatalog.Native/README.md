This is an unconnected native catalog prototype. It is excluded from the main
plugin project and does not install hooks, publish masks, mutate native records,
read GPU data, or manage buffer lifetime.

`GlassShapeRuntimeCatalog.TryCreate(ComputeBuffer, out catalog, out reason)` must
run on the Unity/main thread while native resources are stable. It snapshots
the current CPU record spans for the eleven explicit clear shape IDs and the
native `VoxelRotations` matrix table, validates the source ABI/materials/padding,
and derives source-local face labels through the linked contact policy.
All eleven shapes must validate before the catalog is returned.

`Sources` supplies metadata for `GlassShapeVariantLayout.TryCreate`.
`SourceBuffer` and `SourceCount` identify the native source generation.
`TryGetShape` returns raw padded records, actual per-triangle materials, original
local triangle indices per group, and dense per-padded-triangle labels.
`TryBuildGroups(BrickInstanceData, out groups, out reason)` uses those snapshots,
exact integer/32 instance placement, packed resize counts, unit and rotation to
produce contact triangles only on geometry changes. Health must be 255 and the
rotation index must be at most 31. Failures return null output and a reason.

The lifecycle owner must reset the catalog and expose all affected geometry on
source/material/rotation-table replacement, invalid resources, unknown instance
geometry, removal, health/ID changes or deferred work. Source-buffer identity
and current resource-lifetime proof remain the host's responsibility. There is
no per-frame refresh inside this prototype.

The standalone native project verifies compilation against the installed managed
assemblies. The pure console project uses synthetic coordinates and optional
private captured records; asset coordinates and binaries must remain ignored.
Compilation and captured-input tests do not establish native runtime rendering,
GPU parity, lifecycle acceptance or performance.
