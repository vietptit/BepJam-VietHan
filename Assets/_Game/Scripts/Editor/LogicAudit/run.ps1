param([string]$EntryPoint = 'LogicAudit')
$ErrorActionPreference = 'Stop'
$scriptsRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$auditBuild = Join-Path ([IO.Path]::GetTempPath()) ('BepJamLogicAudit-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $auditBuild | Out-Null
$dotnetRoot = Split-Path (Get-Command dotnet).Source -Parent
$sdkVersion = ((dotnet --version) | Select-Object -Last 1).Trim()
$referencePack = Get-ChildItem (Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Ref') -Directory | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
$framework = 'net' + ([Version]$referencePack.Name).Major + '.0'
$references = Get-ChildItem (Join-Path $referencePack.FullName "ref/$framework") -Filter '*.dll'
$sources = @('Data/GameData.cs', 'Data/Chapters.cs', 'Data/Album.cs', 'Core/Kitchen.cs', 'Core/LevelGen.cs', 'Editor/DifficultyReport.cs') | ForEach-Object { Join-Path $scriptsRoot $_ }
$policy = Join-Path $scriptsRoot 'Game/AutoPlayPolicy.cs'
if (Test-Path -LiteralPath $policy) { $sources += $policy }
$sources += Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs.txt' | Select-Object -ExpandProperty FullName
$response = @('/nologo', '/nostdlib+', '/target:exe', '/langversion:9', '/warnaserror+', "/main:$EntryPoint", "/out:`"$auditBuild/Audit.dll`"")
$response += $references | ForEach-Object { '/r:"' + $_.FullName + '"' }
$response += $sources | ForEach-Object { '"' + $_ + '"' }
$responsePath = Join-Path $auditBuild 'compile.rsp'
[IO.File]::WriteAllLines($responsePath, $response)
& dotnet (Join-Path $dotnetRoot "sdk/$sdkVersion/Roslyn/bincore/csc.dll") "@$responsePath"
if ($LASTEXITCODE -ne 0) { throw 'Audit compilation failed' }
[IO.File]::WriteAllText((Join-Path $auditBuild 'Audit.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"' + $framework + '","framework":{"name":"Microsoft.NETCore.App","version":"' + ([Version]$referencePack.Name).Major + '.0.0"}}}')
Write-Output "Build: $auditBuild"
& dotnet (Join-Path $auditBuild 'Audit.dll')
if ($LASTEXITCODE -ne 0) { throw 'Logic audit failed' }

# Compile the complete real button class and QA click helper with inert graphics APIs.
# Keeping these source-driven catches changes in the press/eligibility contract.
$buttonSource = [IO.File]::ReadAllText((Join-Path $scriptsRoot 'UI/UIKit.cs'))
$buttonStart = $buttonSource.IndexOf('    public class JuicyButton ')
$buttonEnd = $buttonSource.IndexOf('    /// <summary>Gentle looping idle motion', $buttonStart)
$pilotSource = [IO.File]::ReadAllText((Join-Path $scriptsRoot 'Game/AutoPilot.cs'))
$clickStart = $pilotSource.IndexOf('        static void Click(')
$clickEnd = $pilotSource.IndexOf('        static InvalidOperationException QaFailure(', $clickStart)
if ($buttonStart -lt 0 -or $buttonEnd -lt 0 -or $clickStart -lt 0 -or $clickEnd -lt 0) { throw 'Input audit source boundaries changed' }
$inputSource = 'using System; using UnityEngine; using UnityEngine.UI; using UnityEngine.EventSystems; namespace BepJam {' + $buttonSource.Substring($buttonStart, $buttonEnd - $buttonStart) + ' public static class AutoPilot {' + $pilotSource.Substring($clickStart, $clickEnd - $clickStart) + '}}'
$inputPath = Join-Path $auditBuild 'ActualInput.cs'
[IO.File]::WriteAllText($inputPath, $inputSource)
$inputResponse = @('/nologo', '/nostdlib+', '/target:exe', '/langversion:9', '/warnaserror+', "/out:`"$auditBuild/Input.dll`"")
$inputResponse += $references | ForEach-Object { '/r:"' + $_.FullName + '"' }
$inputResponse += Get-ChildItem (Join-Path $PSScriptRoot 'Input') -Filter '*.cs.txt' | ForEach-Object { '"' + $_.FullName + '"' }
$inputResponse += '"' + $inputPath + '"'
$inputRsp = Join-Path $auditBuild 'input.rsp'
[IO.File]::WriteAllLines($inputRsp, $inputResponse)
& dotnet (Join-Path $dotnetRoot "sdk/$sdkVersion/Roslyn/bincore/csc.dll") "@$inputRsp"
if ($LASTEXITCODE -ne 0) { throw 'Input audit compilation failed' }
Copy-Item -LiteralPath (Join-Path $auditBuild 'Audit.runtimeconfig.json') -Destination (Join-Path $auditBuild 'Input.runtimeconfig.json')
& dotnet (Join-Path $auditBuild 'Input.dll')
if ($LASTEXITCODE -ne 0) { throw 'Input audit failed' }
