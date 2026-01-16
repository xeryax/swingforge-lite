# Automated testing cycle for triangulation fixes
# 1. Read logs
# 2. Build app
# 3. Run app
# 4. Force close after 15 seconds
# 5. Read logs
# 6. Repeat

$LogPath = "C:\Users\xerya\AppData\Roaming\Kinovea\Logs"
$ExePath = "Kinovea\bin\x64\Release\SwingForge.exe"
$BuildPath = "Kinovea\Kinovea.csproj"
$MSBuildPath = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

$Iteration = 1
$MaxIterations = 5

Write-Host "=== Automated Triangulation Testing Cycle ===" -ForegroundColor Cyan
Write-Host "Logs: $LogPath" -ForegroundColor Yellow
Write-Host "Exe: $ExePath" -ForegroundColor Yellow
Write-Host "Max Iterations: $MaxIterations" -ForegroundColor Yellow
Write-Host ""

while ($Iteration -le $MaxIterations) {
    Write-Host "`n=== ITERATION $Iteration ===" -ForegroundColor Green
    
    # Step 1: Read latest log
    Write-Host "`n[1/5] Reading latest log file..." -ForegroundColor Cyan
    $logFiles = Get-ChildItem -Path $LogPath -Filter "*.txt" | Sort-Object LastWriteTime -Descending
    if ($logFiles.Count -gt 0) {
        $latestLog = $logFiles[0]
        Write-Host "Latest log: $($latestLog.Name)" -ForegroundColor Gray
        Write-Host "Last modified: $($latestLog.LastWriteTime)" -ForegroundColor Gray
        
        # Show last 20 lines of log
        Write-Host "`nLast 20 lines of log:" -ForegroundColor Yellow
        Get-Content $latestLog.FullName -Tail 20 | ForEach-Object {
            if ($_ -match "WARN|ERROR|Triangulation|Frame.*Camera") {
                Write-Host $_ -ForegroundColor Red
            } elseif ($_ -match "valid=|zeroConf=|lowConf=|failed=") {
                Write-Host $_ -ForegroundColor Yellow
            } else {
                Write-Host $_ -ForegroundColor Gray
            }
        }
    } else {
        Write-Host "No log files found" -ForegroundColor Yellow
    }
    
    # Step 2: Build app
    Write-Host "`n[2/5] Building application..." -ForegroundColor Cyan
    $buildResult = & $MSBuildPath $BuildPath /p:Configuration=Release /p:Platform=x64 /t:Build /v:minimal 2>&1
    
    $buildErrors = $buildResult | Select-String -Pattern "error|Error|ERROR"
    if ($buildErrors) {
        Write-Host "Build FAILED!" -ForegroundColor Red
        $buildErrors | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        Write-Host "`nStopping test cycle due to build errors." -ForegroundColor Red
        break
    } else {
        Write-Host "Build succeeded" -ForegroundColor Green
    }
    
    # Step 3: Run app
    Write-Host "`n[3/5] Running application (will auto-open last video)..." -ForegroundColor Cyan
    if (Test-Path $ExePath) {
        $process = Start-Process -FilePath $ExePath -PassThru
        Write-Host "App started (PID: $($process.Id))" -ForegroundColor Gray
        
        # Step 4: Wait 15 seconds then force close
        Write-Host "`n[4/5] Waiting 15 seconds for 2D pose detection to run..." -ForegroundColor Cyan
        Start-Sleep -Seconds 15
        
        Write-Host "Force closing application..." -ForegroundColor Cyan
        try {
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
            Write-Host "App closed successfully" -ForegroundColor Green
        } catch {
            Write-Host "Error closing app: $_" -ForegroundColor Red
            # Try to find and kill any remaining SwingForge processes
            Get-Process -Name "SwingForge" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        }
        
        # Wait a moment for log file to be written
        Start-Sleep -Seconds 2
    } else {
        Write-Host "ERROR: Executable not found at $ExePath" -ForegroundColor Red
        break
    }
    
    # Step 5: Read logs again
    Write-Host "`n[5/5] Reading updated log file..." -ForegroundColor Cyan
    Start-Sleep -Seconds 1
    $logFiles = Get-ChildItem -Path $LogPath -Filter "*.txt" | Sort-Object LastWriteTime -Descending
    if ($logFiles.Count -gt 0) {
        $latestLog = $logFiles[0]
        Write-Host "Latest log: $($latestLog.Name)" -ForegroundColor Gray
        Write-Host "Last modified: $($latestLog.LastWriteTime)" -ForegroundColor Gray
        
        # Show last 30 lines focusing on triangulation
        Write-Host "`nLast 30 lines (triangulation-related):" -ForegroundColor Yellow
        $logContent = Get-Content $latestLog.FullName -Tail 50
        $triangulationLines = $logContent | Select-String -Pattern "Triangulation|Frame.*Camera|valid=|zeroConf=|lowConf=|failed=|WARN|ERROR" | Select-Object -Last 30
        if ($triangulationLines) {
            $triangulationLines | ForEach-Object {
                if ($_ -match "WARN|ERROR") {
                    Write-Host $_ -ForegroundColor Red
                } elseif ($_ -match "valid=|zeroConf=|lowConf=|failed=") {
                    Write-Host $_ -ForegroundColor Yellow
                } elseif ($_ -match "Frame.*Camera") {
                    Write-Host $_ -ForegroundColor Cyan
                } else {
                    Write-Host $_ -ForegroundColor Gray
                }
            }
        } else {
            Write-Host "No triangulation-related log entries found" -ForegroundColor Yellow
        }
    }
    
    $Iteration++
    
    if ($Iteration -le $MaxIterations) {
        Write-Host "`nWaiting 3 seconds before next iteration..." -ForegroundColor Gray
        Start-Sleep -Seconds 3
    }
}

Write-Host "`n=== Testing Cycle Complete ===" -ForegroundColor Cyan
Write-Host "Total iterations: $($Iteration - 1)" -ForegroundColor Yellow
