#!/usr/bin/env bash
# ========================================================
# Building libEngineNative.so (C++20 Acceleration Library)
# ========================================================
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "${SCRIPT_DIR}"

CXX="${CXX:-g++}"

echo "[EngineNative] Compiling C++20 shared library with Vulkan Linux (X11/Wayland) support..."

VK_FLAGS=""
if command -v pkg-config >/dev/null 2>&1 && pkg-config --exists vulkan; then
    VK_FLAGS="$(pkg-config --cflags --libs vulkan)"
elif [ -n "${VULKAN_SDK}" ]; then
    VK_FLAGS="-I${VULKAN_SDK}/include -L${VULKAN_SDK}/lib -lvulkan"
else
    VK_FLAGS="-lvulkan"
fi

${CXX} -std=c++20 -O2 -fPIC -shared \
    src/EngineNative.cpp \
    -I include \
    -D ENGINE_NATIVE_HAS_VULKAN \
    ${VK_FLAGS} \
    -lX11 \
    -lpthread \
    -o libEngineNative.so

echo "[SUCCESS] libEngineNative.so compiled successfully."

# Copy to build output directories if present
if [ -d "../../bin/Debug/net10.0" ]; then
    cp -f libEngineNative.so "../../bin/Debug/net10.0/"
fi
if [ -d "../../bin/Release/net10.0" ]; then
    cp -f libEngineNative.so "../../bin/Release/net10.0/"
fi

