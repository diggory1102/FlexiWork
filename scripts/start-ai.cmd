@echo off
rem Bam dup de khoi dong tro ly AI (Ollama + Caddy + Cloudflare Tunnel). Tat: start-ai.cmd -Stop
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-ai.ps1" %*
echo.
pause
