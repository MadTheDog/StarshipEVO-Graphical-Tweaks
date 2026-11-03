param([string]$OutputDirectory='dist/GraphicalTweaks',[switch]$NoRestore,[string]$ShaderDirectory='dist/GlassRendering',[switch]$SkipTests,[string]$GameDirectory='',[string]$LoaderDirectory='')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME=Join-Path $root '.tools/dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:NUGET_PACKAGES=Join-Path $root '.tools/nuget'
$dotnetPath=Join-Path $root '.tools/dotnet/dotnet.exe'
if(!(Test-Path -LiteralPath $dotnetPath)){$dotnetPath=(Get-Command dotnet -ErrorAction Stop).Source}
Push-Location $root
try {
 $restoreOptions=@()
 if($NoRestore){$restoreOptions=@('--no-restore')}
 if(!$SkipTests){
 foreach($suite in @('GraphicalTweaks','GlassFaceCoverage','NearShadowDetail','GlassSceneSupport','GlassRayClassifier','DecalVisibilityWork','GlassNominalCaps','GlassNominalProfiles','GlassShapePublication','GlassPartialGeometry','GlassComparisonOutcome')) {
  & $dotnetPath run --project ('tests/'+$suite+'.Tests') -c Release @restoreOptions
  if($LASTEXITCODE){throw ($suite+' behavior checks failed')}
 }
 foreach($project in @('tests/GlassPartialCoverage.Tests','tests/GlassPartialCache.Tests','tests/GlassPartialTail.Native/PolicyTests','tests/GlassPartialCoverage.Native/PolicyTests','tests/GlassPartialReferenceChecks.Tests')) {
  & $dotnetPath run --project $project -c Release @restoreOptions
  if($LASTEXITCODE){throw ($project+' behavior checks failed')}
 }
 } else {Write-Output 'Behavior suites skipped for this wording/version build.'}
 $buildProperties=@()
 if($GameDirectory){$buildProperties+=('-p:GameDir='+$GameDirectory)}
 if($LoaderDirectory){$buildProperties+=('-p:LoaderDir='+$LoaderDirectory)}
 & $dotnetPath build src/GraphicalTweaks -c Release @restoreOptions @buildProperties
 if($LASTEXITCODE){throw 'Plugin build failed'}
 $shaderRoot=if([IO.Path]::IsPathRooted($ShaderDirectory)){[IO.Path]::GetFullPath($ShaderDirectory)}else{[IO.Path]::GetFullPath((Join-Path $root $ShaderDirectory))}
 if(!$shaderRoot.StartsWith(([IO.Path]::GetFullPath($root).TrimEnd('\')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Shader input must remain inside the development workspace'}
 $bundle=Join-Path $shaderRoot 'graphicaltweaks-glass.bundle'
 $buildInfo=Join-Path $shaderRoot 'build-info.txt'
 if(!(Test-Path -LiteralPath $bundle) -or !(Test-Path -LiteralPath $buildInfo)) {throw 'Build the original glass shader bundle with the matching Unity Editor first; see README.md'}
 if((Get-Content -LiteralPath $buildInfo -TotalCount 1) -ne '2019.4.30f1') {throw 'Glass bundle must be built with Unity2019.4.30f1'}
 $output=[IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
 $workspacePrefix=[IO.Path]::GetFullPath($root).TrimEnd('\')+'\'
 if(!$output.StartsWith($workspacePrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Build output must remain inside the development workspace'}
 New-Item -ItemType Directory -Force -Path $output | Out-Null
 Copy-Item -LiteralPath 'src/GraphicalTweaks/bin/Release/netstandard2.0/GraphicalTweaks.dll' -Destination $output
 Copy-Item -LiteralPath $bundle -Destination $output
 Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $output 'README.md')
 Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $output 'LICENSE')
 [string[]]$hashLines=foreach($name in @('GraphicalTweaks.dll','graphicaltweaks-glass.bundle','README.md','LICENSE')) {
  (Get-FileHash -LiteralPath (Join-Path $output $name)).Hash.ToLowerInvariant()+'  '+$name
 }
 [IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS.txt'),$hashLines,[Text.UTF8Encoding]::new($false))
 Write-Output ('Local package: '+$output)
} finally { Pop-Location }
