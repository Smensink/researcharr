# Researcharr Development Build Script

## Quick Start

```bash
# Build everything and start the app
./dev-build.sh --run

# Just build (don't start)
./dev-build.sh
```

## What It Does

The `dev-build.sh` script automates the entire development build process:

1. **Stops** any running Researcharr instances
2. **Builds** the frontend (using yarn or npm)
3. **Builds** the backend (with StyleCop disabled for faster builds)
4. **Deploys** UI files to the correct publish directory
5. **Optionally starts** the application

## Usage

### Build and Run

```bash
./dev-build.sh --run
# or
./dev-build.sh -r
```

This will build everything and immediately start Researcharr at `http://localhost:7337`

### Build Only

```bash
./dev-build.sh
```

This will build everything but won't start the app. You can then start it manually:

```bash
./_output/net6.0/osx-arm64/publish/Readarr --nobrowser --data=/tmp/researcharr-test
```

## Configuration

You can customize the build with environment variables:

```bash
# Build for different platform
RID=linux-x64 ./dev-build.sh

# Use different data directory
DATA_DIR=/custom/path ./dev-build.sh --run

# Use different .NET framework
FRAMEWORK=net7.0 ./dev-build.sh
```

## Output Locations

After building:

- **Backend**: `_output/net6.0/osx-arm64/publish/`
- **Frontend**: `_output/net6.0/osx-arm64/publish/UI/`
- **Executable**: `_output/net6.0/osx-arm64/publish/Readarr`

## Advantages Over build.sh

1. **No StyleCop failures** - Runs with analyzers disabled for faster development
2. **Automatic UI deployment** - No manual copying needed
3. **One command** - Build and run in a single step
4. **Stops running instances** - Prevents file lock errors
5. **Colored output** - Easy to see progress and errors

## When to Use Each Script

- **`dev-build.sh`**: Day-to-day development with frequent changes
- **`build.sh`**: Production builds when you need full validation and packages

## Troubleshooting

### Frontend build fails

Make sure you have yarn or npm installed:

```bash
yarn --version
# or
npm --version
```

### Backend build fails

Check that you have .NET SDK installed:

```bash
dotnet --version
```

### UI not loading (404 errors)

The script verifies UI deployment. If you see this error, check that `_output/UI/index.html` exists after the frontend build.

### Port 7337 already in use

Another instance is running. The script tries to stop it, but you can manually kill it:

```bash
pkill -f Readarr
```
