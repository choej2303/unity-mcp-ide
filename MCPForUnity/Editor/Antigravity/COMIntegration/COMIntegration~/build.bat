@echo off
echo Building COMIntegration tool...

REM Check if running in a VS Developer Command Prompt
where cl >nul 2>nul
if %errorlevel% neq 0 (
    echo Error: 'cl' command not found.
    echo Please run this script from a Visual Studio Developer Command Prompt (x64 Native Tools Command Prompt).
    pause
    exit /b 1
)

REM Ensure output directory exists
if not exist "..\Release" (
    echo Creating output directory ..\Release
    mkdir "..\Release"
)

REM Build command from howtobuild.txt
cl /EHsc /std:c++17 COMIntegration.cpp /link Shlwapi.lib ole32.lib /out:"..\Release\COMIntegration.exe"

if %errorlevel% neq 0 (
    echo Build failed!
    exit /b %errorlevel%
)

echo.
echo Build successful!
echo Output: ..\Release\COMIntegration.exe
