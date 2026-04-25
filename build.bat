@echo off
REM Build script for ViStart .NET

echo Building ViStart .NET...
echo.

REM Check if MSBuild is available
where msbuild >nul 2>&1
if %errorlevel% neq 0 (
    echo MSBuild not found in PATH.
    echo Please run this from a Visual Studio Command Prompt or Developer Command Prompt.
    pause
    exit /b 1
)

REM Build Release configuration
msbuild ViStart.sln /p:Configuration=Release /p:Platform=x86 /t:Rebuild /v:minimal

if %errorlevel% neq 0 (
    echo.
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Build successful!
echo Output: ViStart\bin\Release\ViStart.exe
echo.
pause
