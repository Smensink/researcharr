#!/usr/bin/env bash
set -e

# Researcharr Development Build Script
# Builds backend + frontend and deploys UI automatically

echo "=== Researcharr Development Build ==="
echo ""

# Configuration
RID=${RID:-osx-arm64}
FRAMEWORK=${FRAMEWORK:-net6.0}
OUTPUT_DIR="_output/$FRAMEWORK/$RID"
PUBLISH_DIR="$OUTPUT_DIR/publish"
DATA_DIR=${DATA_DIR:-/tmp/researcharr-test}

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Function to print colored output
print_step() {
    echo -e "${GREEN}▶ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

# Step 1: Stop running instance
print_step "Stopping any running Researcharr instances..."
pkill -f "Readarr --nobrowser" 2>/dev/null || true
sleep 1

# Step 2: Build Frontend
print_step "Building frontend..."
cd frontend
if command -v yarn >/dev/null 2>&1; then
    yarn run build --env production
else
    npm run build -- --env production
fi
cd ..

# Step 3: Build Backend
print_step "Building backend for $RID..."
dotnet publish src/NzbDrone.Console/Readarr.Console.csproj \
    -c Release \
    -r $RID \
    -f $FRAMEWORK \
    --self-contained \
    -o $PUBLISH_DIR \
    -p:RunAnalyzersDuringBuild=false \
    -p:TreatWarningsAsErrors=false

if [ $? -ne 0 ]; then
    print_error "Backend build failed!"
    exit 1
fi

# Step 4: Copy UI to publish directory
print_step "Deploying UI to publish directory..."
mkdir -p "$PUBLISH_DIR/UI"
cp -r _output/UI/ "$PUBLISH_DIR/UI/"

# Verify UI was copied
if [ ! -f "$PUBLISH_DIR/UI/index.html" ]; then
    print_error "UI deployment failed - index.html not found!"
    exit 1
fi

print_step "Build completed successfully!"
echo ""
echo "Output: $PUBLISH_DIR"
echo "UI: $PUBLISH_DIR/UI"
echo ""

# Step 5: Optionally start the app
if [ "$1" == "--run" ] || [ "$1" == "-r" ]; then
    print_step "Starting Researcharr..."
    echo "Data directory: $DATA_DIR"
    echo "Access at: http://localhost:7337"
    echo ""
    echo "Press Ctrl+C to stop"
    echo ""
    ./$PUBLISH_DIR/Readarr --nobrowser --data="$DATA_DIR"
else
    echo "To start the application, run:"
    echo "  ./$PUBLISH_DIR/Readarr --nobrowser --data=$DATA_DIR"
    echo ""
    echo "Or run this script with --run to build and start:"
    echo "  ./dev-build.sh --run"
    echo ""
fi
