<#
.SYNOPSIS
  Jalankan DemoApi + NBomber di Docker.
.EXAMPLE
  ./stress-test.ps1                 # stress test (default)
  ./stress-test.ps1 throughput 500  # throughput 500 RPS
  ./stress-test.ps1 error-rate 0    # checkout tanpa kegagalan
  ./stress-test.ps1 load -KeepApi   # API tetap hidup setelah test
#>
param(
    [ValidateSet('load', 'stress', 'throughput', 'latency', 'error-rate')]
    [string]$Case = 'stress',
    [int]$Value,
    [switch]$KeepApi
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

docker info *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker belum jalan. Buka Docker Desktop dulu.' }

docker compose --profile tests build
if ($LASTEXITCODE -ne 0) { throw 'Build image gagal.' }
docker compose up -d api
if ($LASTEXITCODE -ne 0) { throw 'Gagal menyalakan API (port 5080 dipakai?).' }

try {
    $deadline = (Get-Date).AddSeconds(60)
    while ($true) {
        try { Invoke-RestMethod http://127.0.0.1:5080/health -TimeoutSec 2 | Out-Null; break }
        catch {
            if ((Get-Date) -gt $deadline) { docker compose logs api; throw 'API tidak siap dalam 60 detik.' }
            Start-Sleep -Milliseconds 500
        }
    }
    Write-Host "API siap. Menjalankan kasus '$Case'..." -ForegroundColor Cyan

    $testArgs = @($Case)
    if ($PSBoundParameters.ContainsKey('Value')) { $testArgs += $Value }
    docker compose run --rm tests @testArgs
    $code = $LASTEXITCODE
}
finally {
    if (-not $KeepApi) { docker compose down | Out-Null }
}

$latest = Get-ChildItem reports -Directory | Sort-Object LastWriteTime | Select-Object -Last 1
if ($latest) { Write-Host "Report: $($latest.FullName)" -ForegroundColor Green }
Write-Host "Exit code: $code (0 = SLO PASS, 1 = SLO FAIL, 2 = INVALID)"
exit $code
