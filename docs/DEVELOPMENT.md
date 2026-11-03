# Building from source

Player installation is covered in the README. Source builds need:

- .NET SDK 8 or later.
- Unity Editor 2019.4.30f1 with Windows build support, for shader builds.
- A local SEVO installation and BepInEx 5 core assemblies.

Game assemblies and assets are read from your local installation. They are
excluded from this repository and release archive.

## Build the shader bundle

```powershell
./tools/Build-Shaders.ps1 -EditorPath 'C:/Path/To/Unity2019/Editor/Unity.exe'
```

The output is `dist/GlassRendering/graphicaltweaks-glass.bundle` and its
`build-info.txt`. A previously built matching bundle can be reused for C#
wording/version changes. Unity activation may require opening the shader
project in the Editor first.

## Build and package

```powershell
./tools/Build.ps1 `
  -GameDirectory 'C:/Path/To/Starship EVO' `
  -LoaderDirectory 'C:/Path/To/Starship EVO/BepInEx/core' `
  -OutputDirectory dist/Sal-GraphicalTweaks-1.0.0
```

The build runs the behavior suites by default. Use `-SkipTests` for wording,
documentation or version-only changes; use the full suites when rendering,
geometry, culling or cache behavior changes. `-NoRestore` reuses restored NuGet
packages. The large assertion count comes mostly from parameterised/randomised
geometry loops and adds no in-game work.

Some native diagnostic harnesses require local capture fixtures or SDKs. Raw
game captures, isolated installations, saves, tools, logs and bundles stay out
of Git. Production sources also live under several `tests/*` directories and
are linked by the main project, so retain that structure.

Release 1.0.0 changes naming, version and wording from the tested 0.9.0 renderer.
The C# build and package checks cover this update. Native startup, setting
off/on and GPU publication were tested on 0.9.0; a new 1.0.0 in-game session
has not been run.
