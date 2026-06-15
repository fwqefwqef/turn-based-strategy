@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%"
dotnet run --project "StatGainLab.csproj"
if errorlevel 1 (
    echo.
    echo Failed to launch Stat Gain Lab.
    pause
)
popd
endlocal
