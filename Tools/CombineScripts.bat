@echo off
echo Combining scripts into single text file...
powershell -ExecutionPolicy Bypass -File "%~dp0CombineScripts.ps1"
echo Done.
pause