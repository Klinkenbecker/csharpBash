@echo off
for /L %%i in (1,1,7) do (echo tick %%i & ping -n 2 127.0.0.1 >nul)
