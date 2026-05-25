@echo off
echo Starting PlustekBCR...
dotnet run --launch-profile "PlustekBCR (Unpackaged)"
if %errorlevel% neq 0 (
    echo.
    echo Application failed with error code %errorlevel%
    pause
)
