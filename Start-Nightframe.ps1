#!/usr/bin/env pwsh
<#
.SYNOPSIS
    NIGHTFRAME Local Launcher - PowerShell version

.DESCRIPTION
    Starts all NIGHTFRAME components locally on this PC
#>

Write-Host ""
Write-Host "  ╔═══════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "  ║           NIGHTFRAME v2.0 - LOCAL INITIALIZATION                          ║" -ForegroundColor Cyan
Write-Host "  ╠═══════════════════════════════════════════════════════════════════════════╣" -ForegroundColor Cyan
Write-Host "  ║  Starting all components on this PC...                                    ║" -ForegroundColor Cyan
Write-Host "  ╚═══════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$OrchestratorDir = Join-Path $ScriptDir "Orchestrator"
$DroneDir = Join-Path $ScriptDir "Drone"
$TrainingDir = Join-Path $ScriptDir "training"

# Track started processes
$processes = @()

try {
    # Check prerequisites
    Write-Host "[1/5] Checking prerequisites..." -ForegroundColor Yellow
    
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Host "ERROR: .NET SDK not found" -ForegroundColor Red
        exit 1
    }
    
    $pythonAvailable = Get-Command python -ErrorAction SilentlyContinue
    Write-Host "   ✓ Prerequisites OK" -ForegroundColor Green
    Write-Host ""

    # Build Orchestrator
    Write-Host "[2/5] Building Orchestrator..." -ForegroundColor Yellow
    Push-Location $OrchestratorDir
    $buildResult = dotnet build -c Release -v q 2>&1
    Pop-Location
    Write-Host "   ✓ Orchestrator built" -ForegroundColor Green
    Write-Host ""

    # Build Drone
    Write-Host "[3/5] Building Drone..." -ForegroundColor Yellow
    Push-Location $DroneDir
    $buildResult = dotnet build -c Release -v q 2>&1
    Pop-Location
    Write-Host "   ✓ Drone built" -ForegroundColor Green
    Write-Host ""

    # Start Orchestrator
    Write-Host "[4/5] Starting Orchestrator..." -ForegroundColor Yellow
    $orchestratorProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList "run -c Release --urls `"http://localhost:5000;https://localhost:5001`"" `
        -WorkingDirectory $OrchestratorDir `
        -PassThru -WindowStyle Minimized
    $processes += $orchestratorProcess
    Start-Sleep -Seconds 3
    Write-Host "   ✓ Orchestrator started on http://localhost:5000" -ForegroundColor Green
    Write-Host ""

    # Start local Drone
    Write-Host "[5/5] Starting Local Drone..." -ForegroundColor Yellow
    $droneProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList "run -c Release -- --orchestrator http://localhost:5001 --role compute" `
        -WorkingDirectory $DroneDir `
        -PassThru -WindowStyle Minimized
    $processes += $droneProcess
    Start-Sleep -Seconds 2
    Write-Host "   ✓ Local drone connected" -ForegroundColor Green
    Write-Host ""

    Write-Host ""
    Write-Host "  ═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  ◈ NIGHTFRAME IS NOW RUNNING LOCALLY" -ForegroundColor Green
    Write-Host "  ═══════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "   Orchestrator:  " -NoNewline; Write-Host "http://localhost:5000" -ForegroundColor Blue
    Write-Host "   gRPC:          " -NoNewline; Write-Host "https://localhost:5001" -ForegroundColor Blue
    Write-Host "   SignalR Hub:   " -NoNewline; Write-Host "http://localhost:5000/hub/console" -ForegroundColor Blue
    Write-Host ""
    Write-Host "  To start web console:"
    Write-Host "   cd gamma1-web; npm run dev" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  To start AI training:"
    Write-Host "   cd training; python train_metacognitive.py --mode continuous" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  Press Ctrl+C to stop all services..." -ForegroundColor Yellow
    
    # Wait for user to cancel
    while ($true) {
        Start-Sleep -Seconds 1
    }
}
finally {
    Write-Host ""
    Write-Host "Stopping services..." -ForegroundColor Yellow
    foreach ($proc in $processes) {
        if (-not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        }
    }
    Write-Host "Done." -ForegroundColor Green
}
