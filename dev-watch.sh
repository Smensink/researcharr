#!/bin/bash
set -ex

echo "======================================"
echo "Researcharr Dev Watch Setup"
echo "======================================"

# Install system dependencies
echo "Installing system dependencies..."
apt-get update -qq && apt-get install -y -qq sqlite3 libsqlite3-dev curl inotify-tools > /dev/null 2>&1

# SDK image already includes the 6.0 runtime
dotnet --list-runtimes

# Install Node.js 20.x
if ! command -v node >/dev/null 2>&1; then
  echo "Installing Node.js 20.x..."
  curl -fsSL https://deb.nodesource.com/setup_20.x | bash - > /dev/null 2>&1
  apt-get install -y -qq nodejs > /dev/null 2>&1
fi

# Install Yarn globally
if ! command -v yarn >/dev/null 2>&1; then
  echo "Installing Yarn..."
  npm install -g yarn > /dev/null 2>&1
fi

echo "Node.js version: $(node --version)"
echo "Yarn version: $(yarn --version)"

# Install frontend dependencies
if [ ! -f "node_modules/.bin/webpack" ]; then
  echo "Installing frontend dependencies (this may take a few minutes on first run)..."
  yarn install
else
  echo "Frontend dependencies already installed"
fi

# Restore .NET packages
echo "Restoring .NET packages..."
dotnet restore src/Readarr.sln > /dev/null 2>&1

# Clear stale build artifacts to avoid locked files, then copy UI assets for faster startup
if [ -d "_output/net6.0" ]; then
  rm -rf _output/net6.0/*
fi
if [ -d "_output/UI" ]; then
  echo "Copying existing UI files..."
  cp -r _output/UI _output/net6.0/ 2>/dev/null || true
fi

echo ""
echo "======================================"
echo "Starting Watch Modes"
echo "======================================"

# Start frontend watch in background with auto-copy
echo "Starting frontend watch (webpack)..."
(
  yarn watch 2>&1 | while IFS= read -r line; do
    echo "[WEBPACK] $line"
    # Copy UI files when webpack finishes compiling
    if echo "$line" | grep -q "compiled successfully"; then
      echo "[WEBPACK] Build complete, copying UI files..."
      cp -r _output/UI _output/net6.0/ 2>/dev/null || echo "[WEBPACK] UI files copied"
    fi
  done
) &
YARN_PID=$!

# Give webpack a moment to start
sleep 2

# Trap to kill background processes on exit
trap "echo 'Stopping...'; kill $YARN_PID 2>/dev/null || true; exit" INT TERM EXIT

# Start backend watch in foreground
echo "Starting backend watch (dotnet)..."
echo ""
export DOTNET_ROOT=/usr/share/dotnet
dotnet run --project src/NzbDrone.Console/Readarr.Console.csproj --framework net6.0 --nobrowser --data=/data --Urls=http://0.0.0.0:7337
