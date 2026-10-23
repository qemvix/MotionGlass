[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src/MotionGlass/MotionGlass.csproj'
[xml]$project = Get-Content -LiteralPath $projectPath
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a three-part release version.' }
$outputFolder = Join-Path $repoRoot "artifacts/release/v$version"
$publishFolder = Join-Path $repoRoot 'artifacts/publish/win-x64'
$bundleFolder = Join-Path $outputFolder 'portable'
New-Item -ItemType Directory -Path $publishFolder, $bundleFolder -Force | Out-Null

& dotnet publish $projectPath -c Release -r win-x64 --self-contained true `
    -p:DebugType=None -p:DebugSymbols=false --nologo -o $publishFolder
if ($LASTEXITCODE -ne 0) { throw 'Publishing the portable executable failed.' }

Copy-Item -LiteralPath (Join-Path $publishFolder 'MotionGlass.exe') -Destination $bundleFolder -Force
foreach ($fileName in @('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.txt', 'CHANGELOG.md')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $fileName) -Destination $bundleFolder -Force
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs') -Destination $bundleFolder -Recurse -Force

# This check runs the published binary on synthetic images; no desktop overlay is created.
$resultFolder = Join-Path $repoRoot ('artifacts/package-tests/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $resultFolder -Force | Out-Null
$quotedResultFolder = '"' + $resultFolder + '"'
$testProcess = Start-Process -FilePath (Join-Path $publishFolder 'MotionGlass.exe') `
    -ArgumentList '--self-test', $quotedResultFolder -WindowStyle Hidden -PassThru
if (-not $testProcess.WaitForExit(60000)) {
    $testProcess.Kill()
    throw 'Portable offscreen checks timed out.'
}
$resultPath = Join-Path $resultFolder 'self-test.json'
if (-not (Test-Path -LiteralPath $resultPath)) { throw 'Portable checks did not produce a result file.' }
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if ($testProcess.ExitCode -ne 0 -or -not $result.passed) { throw ("Portable checks failed. " + $result.error) }
Copy-Item -LiteralPath $resultPath -Destination (Join-Path $bundleFolder 'verification.json') -Force

$exePath = Join-Path $outputFolder 'MotionGlass.exe'
$zipPath = Join-Path $outputFolder "MotionGlass-v$version-win-x64.zip"
Copy-Item -LiteralPath (Join-Path $publishFolder 'MotionGlass.exe') -Destination $exePath -Force
Compress-Archive -Path (Join-Path $bundleFolder '*') -DestinationPath $zipPath -Force

$hashLines = foreach ($filePath in @($exePath, $zipPath)) {
    $hash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $hash + '  ' + (Split-Path -Leaf $filePath)
}
[IO.File]::WriteAllLines((Join-Path $outputFolder 'SHA256SUMS.txt'), $hashLines, [Text.UTF8Encoding]::new($false))
Write-Output "Portable checks passed. Release files: $outputFolder"
