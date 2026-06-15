@echo off
echo Starting PlustekBCR...
dotnet build --no-restore
if %errorlevel% neq 0 (
    echo.
    echo Build failed with error code %errorlevel%
    pause
    exit /b %errorlevel%
)

set "APP_EXE=%~dp0bin\Debug\net8.0-windows10.0.19041.0\win-x64\PlustekBCR.exe"
if not exist "%APP_EXE%" (
    echo.
    echo Built executable not found:
    echo %APP_EXE%
    pause
    exit /b 1
)

start "" "%APP_EXE%"
