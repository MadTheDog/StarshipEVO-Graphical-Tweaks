# Geometry eligibility contract checks

Run with the workspace .NET SDK:

`./.tools/dotnet/dotnet.exe run --project tests/GlassSceneSupport.Tests -c Release`

The pure tests link only `GlassSceneSupportModel.cs`, without loading Unity or
game assemblies. They cover base geometry mode ranges, additive exemptions,
vertex-1 and pixel-11 dynamic material selectors, verified squared-hologram base
geometry, and triangle-index/cluster-length span bounds with overflow checks.

Native GPU interoperability established that deformation uses the BASE material
before dynamic material selection. Dynamic selectors therefore remain eligible
when base vertex/resize/normal modes are supported. Alpha20's X/Z equalization
has separately passed108 native geometry cases across every supported resize
mode, both dynamic selectors, unequal/equal counts and transformed entities.
An additive base with either selector cannot skip unknown geometry checks,
because its resolved material may contribute alpha. Fixed additive families can
retain the exemption. Shading-only differences intentionally are not inputs to
this CPU guard; the shader handles them with conservative coverage.

These tests verify the geometry contract only. The main assembly must also be
built, and native lifecycle-hook/inventory behavior needs runtime validation.
