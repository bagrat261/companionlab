@echo off
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File "%~dp0ci\run-ollama.ps1" -Model "hf.co/LEONW24/Qwen3.5-9B-Uncensored:Q4_K_M"
if errorlevel 1 pause
