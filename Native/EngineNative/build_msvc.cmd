@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo Building EngineNative.dll (C++20 Acceleration Library)
echo ========================================================

set "VS_PATH="
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

if exist "%VSWHERE%" (
    for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -property installationPath`) do (
        set "VS_PATH=%%i"
    )
)

if "%VS_PATH%"=="" (
    if exist "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools" (
        set "VS_PATH=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools"
    )
)

if "%VS_PATH%"=="" (
    echo [ERROR] Visual Studio / BuildTools not found.
    exit /b 1
)

echo Found BuildTools at: %VS_PATH%
call "%VS_PATH%\VC\Auxiliary\Build\vcvars64.bat"

cd /d "%~dp0"

echo Compiling C++20 shared library...
cl.exe /nologo /std:c++20 /O2 /EHsc /LD src\EngineNative.cpp /I include /Fe:EngineNative.dll /link /DLL

if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] EngineNative.dll compiled successfully.
    if exist "..\..\bin\Debug\net10.0" copy /Y EngineNative.dll "..\..\bin\Debug\net10.0\"
    if exist "..\..\bin\Release\net10.0" copy /Y EngineNative.dll "..\..\bin\Release\net10.0\"
) else (
    echo [FAILED] Compilation failed with error code %ERRORLEVEL%.
    exit /b %ERRORLEVEL%
)

endlocal
