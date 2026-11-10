param([switch]$StablePopupBaseline)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $projectRoot
$editorData = 'C:/Program Files/Unity/Hub/Editor/6000.3.21f1/Editor/Data'
$existing = Get-ChildItem -LiteralPath 'Library/Bee/artifacts' -Recurse -Filter 'Assembly-CSharp.rsp' | Select-Object -First 1
if (!$existing) { throw 'Unity-generated runtime compiler response file was not found.' }
$options = Get-Content -LiteralPath $existing.FullName | Where-Object {
    $_ -notmatch '^-(out|refout|analyzer|analyzerconfig):' -and $_ -notmatch '^"?Assets[/\\].*\.cs"?$'
}
$response = @($options) + @('-out:"Tools/photos/AlbumCompileCheck.dll"')
$response += Get-ChildItem -LiteralPath 'Assets/_Game/Scripts' -Recurse -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '[/\\]Editor[/\\]' } |
    ForEach-Object {
        $source = $_.FullName
        if ($StablePopupBaseline -and $_.Name -in @('Popups.cs', 'PopupsCulture.cs')) {
            $source = Join-Path $projectRoot ('Tools/backup_v8/Scripts/UI/' + $_.Name)
        }
        '"' + $source + '"'
    }
$response | Set-Content -LiteralPath 'Tools/photos/compile.rsp' -Encoding utf8
& "$editorData/NetCoreRuntime/dotnet.exe" "$editorData/DotNetSdkRoslyn/csc.dll" '@Tools/photos/compile.rsp'
if ($LASTEXITCODE -ne 0) { throw "Runtime compilation failed ($LASTEXITCODE)." }
if ($StablePopupBaseline) {
    Write-Output 'Album changes compile with backed-up v8 Popups.cs / PopupsCulture.cs (no Unity launch). Current unrelated popup edits are excluded.'
} else {
    Write-Output 'All runtime C# scripts compile against installed Unity assemblies (no Unity launch).'
}
