param([string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Data')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = Join-Path ([IO.Path]::GetTempPath()) ('crew-single-career-' + [guid]::NewGuid().ToString('N') + '.exe')
$framework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.5'
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') /nologo /target:exe /nostdlib /noconfig "/out:$output" "/r:$framework/mscorlib.dll" "/r:$framework/System.dll" "/r:$framework/System.Core.dll" "/r:$framework/System.Web.Extensions.dll" (Join-Path $repo 'Assets/Script/GameSaveRepository.cs') (Join-Path $repo 'Assets/Script/GameSaveManager.SingleCareer.cs') (Join-Path $PSScriptRoot 'SingleCareerCloudChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Single-career check compilation failed.' }
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $output
if ($LASTEXITCODE -ne 0) { throw 'Single-career checks failed.' }
