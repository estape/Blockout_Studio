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

set "VK_INC="
set "VK_LIB=user32.lib gdi32.lib"

if "%VULKAN_SDK%"=="" (
    if exist "C:\VulkanSDK\1.4.363.0" (
        set "VULKAN_SDK=C:\VulkanSDK\1.4.363.0"
    )
)

if not "%VULKAN_SDK%"=="" (
    echo Detected Vulkan SDK at: %VULKAN_SDK%
    set "VK_INC=/I "%VULKAN_SDK%\Include" /D ENGINE_NATIVE_HAS_VULKAN"
    set "VK_LIB=/LIBPATH:"%VULKAN_SDK%\Lib" vulkan-1.lib user32.lib gdi32.lib"
)

echo Compiling C++20 shared library with Vulkan viewport support...
cl.exe /nologo /std:c++20 /O2 /EHsc /LD src\EngineNative.cpp /I include %VK_INC% /Fe:EngineNative.dll /link /DLL %VK_LIB%

if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] EngineNative.dll compiled successfully.
    if exist "..\..\bin\Debug\net10.0" copy /Y EngineNative.dll "..\..\bin\Debug\net10.0\"
    if exist "..\..\bin\Release\net10.0" copy /Y EngineNative.dll "..\..\bin\Release\net10.0\"
) else (
    echo [FAILED] Compilation failed with error code %ERRORLEVEL%.
    exit /b %ERRORLEVEL%
)

endlocal
