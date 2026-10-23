[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src/MotionGlass/MotionGlass.csproj'
$resultFolder = Join-Path $repoRoot ('artifacts/tests/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $resultFolder -Force | Out-Null

if (-not $NoBuild) {
    & dotnet build $projectPath -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

$executable = Join-Path $repoRoot "src/MotionGlass/bin/$Configuration/net10.0-windows/win-x64/MotionGlass.exe"
if (-not (Test-Path -LiteralPath $executable)) { throw "App not found: $executable" }

# The normal release self-test uses synthetic frames and WARP; it never captures the desktop.
$quotedResultFolder = '"' + $resultFolder + '"'
$testProcess = Start-Process -FilePath $executable -ArgumentList '--self-test', $quotedResultFolder -WindowStyle Hidden -PassThru
if (-not $testProcess.WaitForExit(60000)) {
    $testProcess.Kill()
    throw 'Offscreen shader checks timed out.'
}
$resultPath = Join-Path $resultFolder 'self-test.json'
if (-not (Test-Path -LiteralPath $resultPath)) { throw 'The self-test did not write a result file.' }
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
$result.checks | ForEach-Object { Write-Output $_ }
if ($testProcess.ExitCode -ne 0 -or -not $result.passed) {
    throw ("Offscreen shader checks failed. " + $result.error)
}
Write-Output ("Offscreen checks passed. Results: " + $resultPath)
