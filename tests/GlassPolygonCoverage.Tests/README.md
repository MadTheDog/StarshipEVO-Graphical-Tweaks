# Pure generic glass polygon coverage

Run from the workspace root with the cached SDK:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.tools/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PWD '.tools/nuget'
& .tools/dotnet/dotnet.exe run --project tests/GlassPolygonCoverage.Tests -c Release -p:NuGetAudit=false
```

Append `-- --benchmark` for bounded adjacent-cube, mixed-panel, health-change and
stretched-cube timing/allocation samples. Set `DOTNET_TieredCompilation=0` to
remove tiered-JIT changes when comparing cell sizes. Benchmark output states
the framework, architecture and tiering setting. No Unity/game process,
graphics API, native shader, asset, save or physical model is used.

The 2,112 correctness checks cover mixed rectangular/triangular unions,
different tessellation, nonconvex ring and curved-band caps with holes, tiny
uncovered slivers, plane/edge gaps down to 1e-14, native-scale 1e-7 noncoincidence,
real 0.0025 gaps, rotated/diagonal planes, health/removal/entity/ID transitions,
independent source-local groups, nonfinite/degenerate input and zero padding.
They exercise candidate, fragment, operation, rational-precision, cell,
per-brick and global capacity failure. A deterministic 128-step randomized
lifecycle sequence compares every mask with the unchanged legacy cube model
(2,048 assertions). Two thousand distant coplanar panels check spatial candidate
growth, and a 972-cube 3D grid checks fully surrounded cubes and cell density.
Benchmarks add 507 mask/capacity assertions so their timing cannot be
obtained by silently failing open.

## API

`GlassPolygonCoverageModel` is a pure, single-owner-thread event service:

- `Upsert(GlassPolygonCoverageBrick)` replaces a build ID's indexed snapshot.
- `Remove(buildId)`, `RemoveEntity(entityId)` and `Clear()` reopen affected faces.
- `GetMask(buildId)` returns a `uint`; missing/ineligible input returns zero.
- `DrainChanges(List<GlassPolygonCoverageChange>)` appends coalesced final masks.
- `CandidateChecks` and `BudgetFailures` expose diagnostic counters; `Clear`
  resets them.

The brick contains an entity ID, build ID, health byte and 1–8 face groups.
Each group has a unique source-local bit 0–7 and an array of triangles. Points
are doubles representing the **actual entity-local rendered geometry after
native material deformation, resize, brick unit, voxel rotation and position**.
The model does not infer that geometry from AABBs. It snapshots coordinates
when indexed; later array mutation requires another explicit `Upsert`.

Only health 255 can hide or occlude. Candidate triangles must belong to a
different build ID in the same entity, lie on the exact same plane, and have
opposite winding. All shape families share one index and union calculation.
A group bit is set only if every nondegenerate triangle is covered; an empty
or degenerate-only group remains visible. Other groups on the same brick do
not occlude each other. Duplicate/unknown bits and nonfinite/unknown input
make the whole submitted brick ineligible.

## Exactness and conservative limits

There is no positional, angular or uncovered-area tolerance. Input doubles
are converted to their exact dyadic values. Plane equations are normalized
integer keys; polygons use reduced rational coordinates. Convex half-plane
subtraction removes the union of opposing triangles. A nonzero residual area,
however small, keeps the target visible. Exact matching opposite triangles
provide a cheap complete proof without subtraction allocations. Holes are
preserved because neither side is replaced by a convex hull.

Default limits are:

| Bound | Default |
|---|---:|
| Absolute coordinate | 1,048,576 units |
| Projected spatial cell size | 8 units |
| Input triangles per brick, including degenerate padding | 512 |
| Indexed triangles total | 262,144 |
| Cells per triangle / brick | 256 / 8,192 |
| Triangles per exact-plane/entity cell | 512 |
| Subtraction candidates per target triangle | 256 |
| Residual fragments per target triangle | 256 |
| Candidate visits plus exact cross products per group | 8,192 |
| Vertices per clipped convex polygon | 32 |
| Reduced rational numerator/denominator | 512 bits each |
| Normalized integer plane coefficient | 2,048 bits |

The constructor accepts the configurable limits listed in
`GlassPolygonCoverageLimits`; rational/plane/polygon precision bounds are fixed.
An input/index capacity failure discards that brick's new snapshot and restores
its mask to zero. Old neighbors are recomputed. A coverage work/fragment/precision
failure leaves the affected group visible. Capacity becoming available later
does not automatically resubmit a rejected brick; the integration owner must
retry or explicitly upsert its geometry again. No background thread or retry
loop is started by this model.

These guards bound work and storage; they are not a hard latency guarantee.
BigInteger arithmetic and temporary collections still allocate. The benchmark
isolates model updates using preconstructed inputs; native vertex extraction
and input construction would add integration cost. Results are .NET 8 Release
desktop measurements, not the game's Unity Mono timings or FPS acceptance.
The evidence logs under ignored `local-evidence/shape-inventory/` report both
the initial and optimized implementations, including allocation totals and
per-update median/p95/maximum. Smaller cells reduce nearby unit-cube candidates
but increase indexing work for large stretched faces; profile before choosing
an integration-specific setting.

One comparable run on .NET 8.0.33 X64 with tiered compilation disabled recorded:

| Workload | Total ms | Update p95 ms | Allocated MiB |
|---|---:|---:|---:|
| Legacy rectangle model, 500 adjacent cubes | 19.13 | 0.058 | 14.83 |
| Generic model, same 500 cubes | 167.28 | 0.624 | 48.22 |
| Legacy model, 100 health transitions | 6.83 | 0.123 | 6.04 |
| Generic model, same 100 transitions | 31.73 | 0.568 | 15.95 |
| Generic model, 500 mixed unions / 1,500 panels | 125.19 | 0.158 | 13.72 |
| Generic model, 64 adjacent cubes stretched 16x | 12.87 | 0.293 | 3.89 |

All benchmark geometry/masks are checked and none of these runs used a budget
fallback. The legacy model only supports exact tick-grid rectangles; it does not
provide mixed polygon, diagonal or hole coverage. The generic cube batch was
about 8.7 times slower and allocated about 3.25 times as much in this run.
This is a real cost, not a claim that event-only work is free. The model should
not replace synchronous full-world initialization without integration profiling
and bounded incremental scheduling. Changed geometry must not retain stale
GPU masks while its new proof waits. That scheduling/invalidation layer is not
implemented by this pure model.

The same run reduced unit-cube allocation to 24.11 MiB with two-unit cells,
but stretched-cube allocation rose to 18.58 MiB and time to 89.72 ms (versus
3.89 MiB / 12.87 ms with eight-unit cells). Keep the default until actual
shape sizes are profiled. See `polygon-benchmark-legacy-comparison.txt` for
per-update median/max and candidate counts; occasional maxima exceeded 10 ms
even when typical updates were much shorter.

Strictness intentionally retains intended joins that differ by native float
roundoff. Runtime quarter-rotation/canonicalization policy needs separate native
geometry evidence. This model never snaps an approximately coincident arch or
box surface into a join. No runtime integration is included here; the tested
cube-166 service, shader buffers and 0.6 package remain unchanged.
