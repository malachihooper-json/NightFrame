@echo off
REM ═══════════════════════════════════════════════════════════════════════════════
REM                    NIGHTFRAME LOCAL LAUNCHER
REM ═══════════════════════════════════════════════════════════════════════════════
REM  Starts all NIGHTFRAME components locally on this PC
REM ═══════════════════════════════════════════════════════════════════════════════

echo.
echo  ╔═══════════════════════════════════════════════════════════════════════════╗
echo  ║           NIGHTFRAME v2.0 - LOCAL INITIALIZATION                          ║
echo  ╠═══════════════════════════════════════════════════════════════════════════╣
echo  ║  Starting all components on this PC...                                    ║
echo  ╚═══════════════════════════════════════════════════════════════════════════╝
echo.

REM Set paths
set SCRIPT_DIR=%~dp0
set ORCHESTRATOR_DIR=%SCRIPT_DIR%Orchestrator
set DRONE_DIR=%SCRIPT_DIR%Drone
set WEB_DIR=%SCRIPT_DIR%gamma1-web
set TRAINING_DIR=%SCRIPT_DIR%training

REM Check prerequisites
echo [1/5] Checking prerequisites...

where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo ERROR: .NET SDK not found. Please install from https://dot.net
    exit /b 1
)

where python >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo WARNING: Python not found. Training features will be disabled.
    set PYTHON_AVAILABLE=0
) else (
    set PYTHON_AVAILABLE=1
)

echo    ✓ Prerequisites OK
echo.

REM Build Orchestrator
echo [2/5] Building Orchestrator...
cd /d "%ORCHESTRATOR_DIR%"
dotnet build -c Release -v q
if %ERRORLEVEL% neq 0 (
    echo ERROR: Orchestrator build failed
    exit /b 1
)
echo    ✓ Orchestrator built
echo.

REM Build Drone
echo [3/5] Building Drone...
cd /d "%DRONE_DIR%"
dotnet build -c Release -v q
if %ERRORLEVEL% neq 0 (
    echo ERROR: Drone build failed  
    exit /b 1
)
echo    ✓ Drone built
echo.

REM Start Orchestrator in background
echo [4/5] Starting Orchestrator...
cd /d "%ORCHESTRATOR_DIR%"
start "NIGHTFRAME Orchestrator" /min dotnet run -c Release --urls "http://localhost:5000;https://localhost:5001"
timeout /t 3 /nobreak >nul
echo    ✓ Orchestrator started on http://localhost:5000
echo.

REM Start local Drone
echo [5/5] Starting Local Drone...
cd /d "%DRONE_DIR%"
start "NIGHTFRAME Drone" /min dotnet run -c Release -- --orchestrator http://localhost:5001 --role compute
timeout /t 2 /nobreak >nul
echo    ✓ Local drone connected
echo.

echo.
echo  ═══════════════════════════════════════════════════════════════════════════
echo  ◈ NIGHTFRAME IS NOW RUNNING LOCALLY
echo  ═══════════════════════════════════════════════════════════════════════════
echo.
echo    Orchestrator:  http://localhost:5000
echo    gRPC:          https://localhost:5001  
echo    SignalR Hub:   http://localhost:5000/hub/console
echo.
echo  To start web console:
echo    cd gamma1-web ^&^& npm run dev
echo.
echo  To start AI training:
echo    cd training ^&^& python train_metacognitive.py --mode continuous
echo.
echo  Press any key to stop all services...
pause >nul

REM Stop services
echo Stopping services...
taskkill /FI "WINDOWTITLE eq NIGHTFRAME Orchestrator" >nul 2>&1
taskkill /FI "WINDOWTITLE eq NIGHTFRAME Drone" >nul 2>&1
echo Done.
