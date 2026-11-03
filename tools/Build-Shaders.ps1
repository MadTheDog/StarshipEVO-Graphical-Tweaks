param([switch]$ViaUnityCli,[string]$OutputDirectory='dist/GlassRendering',[string]$EditorPath='')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$editor=if($EditorPath){[IO.Path]::GetFullPath($EditorPath)}else{Join-Path $root '.tools/Unity-2019.4.30f1/Editor/Unity.exe'}
if(!(Test-Path -LiteralPath $editor)){throw 'Matching Unity Editor is not installed'}
$assets=Join-Path $root 'shader-project/Assets/GraphicalTweaks'
New-Item -ItemType Directory -Force -Path $assets | Out-Null
foreach($source in Get-ChildItem -LiteralPath (Join-Path $root 'shaders') -File){
 if($source.Extension -notin @('.shader','.hlsl','.cginc','.compute')){continue}
 $target=Join-Path $assets $source.Name
 Copy-Item -LiteralPath $source.FullName -Destination $target
 if(!(Test-Path -LiteralPath ($target+'.meta'))){
  $md5=[Security.Cryptography.MD5]::Create()
  try{$guid=([BitConverter]::ToString($md5.ComputeHash([Text.Encoding]::UTF8.GetBytes('GraphicalTweaks/'+$source.Name)))).Replace('-','').ToLowerInvariant()}finally{$md5.Dispose()}
  [IO.File]::WriteAllText($target+'.meta',"fileFormatVersion: 2`nguid: $guid`n")
 }
}
$project=Join-Path $root 'shader-project'
$output=if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))}
if(!$output.StartsWith(([IO.Path]::GetFullPath($root).TrimEnd('\')+'\'),[StringComparison]::OrdinalIgnoreCase)) {throw 'Shader output must remain inside this project'}
$outputRequest=Join-Path $project 'build.output'
[IO.File]::WriteAllText($outputRequest,$output,[Text.UTF8Encoding]::new($false))
$started=[DateTime]::UtcNow
try {
if($ViaUnityCli){
 $cli=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Unity/bin/unity.exe'
 if(!(Test-Path -LiteralPath $cli)){throw 'The already-installed Unity CLI was not found; no tool will be installed automatically'}
 $started=[DateTime]::UtcNow
 [IO.File]::WriteAllText((Join-Path $project 'build.request'),'build')
 & $cli open $project --editor-path $editor --non-interactive --no-banner --format json
 if($LASTEXITCODE){throw 'Unity CLI did not launch the approved shader project'}
 $bundle=Join-Path $output 'graphicaltweaks-glass.bundle'
 $info=Join-Path $output 'build-info.txt'
 $until=[DateTime]::UtcNow.AddSeconds(120)
 while([DateTime]::UtcNow -lt $until){
  if((Test-Path -LiteralPath $bundle) -and (Test-Path -LiteralPath $info) -and
     (Get-Item -LiteralPath $bundle).LastWriteTimeUtc -ge $started -and
     (Get-Item -LiteralPath $info).LastWriteTimeUtc -ge $started){
   Write-Output ('Matching Unity shader build completed: '+$bundle)
   return
  }
  Start-Sleep -Milliseconds 500
 }
 throw 'Unity CLI launched but no fresh completed shader bundle appeared within120 seconds; inspect the Editor log'
}
$log=Join-Path $root ('local-evidence/shader-build-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.log')
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $log) | Out-Null
$args='-batchmode -nographics -quit -projectPath "'+$project+'" -executeMethod BuildGraphicalTweaks.Build -logFile "'+$log+'"'
$process=Start-Process -FilePath $editor -ArgumentList $args -WindowStyle Hidden -PassThru
$process.WaitForExit()
$buildLog=[IO.File]::ReadAllText($log).Replace([string][char]0,'')
Write-Output ('Unity build log: '+$log)
Write-Output ($buildLog.Substring([Math]::Max(0,$buildLog.Length-7000)))
if($process.ExitCode -ne 0){throw ('Unity shader build exited '+$process.ExitCode)}
$bundle=Join-Path $output 'graphicaltweaks-glass.bundle'
$info=Join-Path $output 'build-info.txt'
if(!(Test-Path -LiteralPath $bundle) -or !(Test-Path -LiteralPath $info) -or
   (Get-Item -LiteralPath $bundle).LastWriteTimeUtc -lt $started -or
   (Get-Item -LiteralPath $info).LastWriteTimeUtc -lt $started){throw 'Fresh shader bundle/build info missing; inspect the Editor log'}
} finally {
 if($ViaUnityCli -and (Test-Path -LiteralPath (Join-Path $project 'build.request'))){Remove-Item -LiteralPath (Join-Path $project 'build.request') -Force}
 if(Test-Path -LiteralPath $outputRequest){Remove-Item -LiteralPath $outputRequest -Force}
}
