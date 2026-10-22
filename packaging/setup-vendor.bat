@echo off
cd /d "%~dp0"
echo.
echo  iGCRGB - preparing Colorful iGC lighting components...
echo  (see setup-vendor.ps1 / README for details)
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup-vendor.ps1"
echo.
pause
