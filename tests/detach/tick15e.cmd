@echo off
for /L %%i in (1,1,15) do (echo tick %%i %time% & ping -n 2 127.0.0.1 >nul 2>>pingerr.log || echo   ping failed)
