#!/usr/bin/env bash
# setup.sh – build synthea-cli for the Codex runner (Ubuntu 22.04 base image)
# Enhanced for ChatGPT Codex with full environment setup and integration test support

set -euo pipefail

echo "🚀 Setting up Synthea CLI environment for ChatGPT Codex..."

# 1) Ensure Java 17+ and .NET 8 are present. Skip apt if already installed.
echo "📦 Checking system dependencies..."
packages=()
if ! command -v java >/dev/null; then
    packages+=(openjdk-17-jre-headless)
    echo "  - Java 17 JRE will be installed"
fi
if ! command -v dotnet >/dev/null; then
    packages+=(dotnet-sdk-8.0)
    echo "  - .NET 8.0 SDK will be installed"
fi

if [ ${#packages[@]} -ne 0 ]; then
    echo "  - Installing packages: ${packages[*]}"
    sudo apt-get update -qq
    sudo apt-get install -y --no-install-recommends "${packages[@]}"
    sudo apt-get clean
    sudo rm -rf /var/lib/apt/lists/*
    echo "  ✅ System dependencies installed"
else
    echo "  ✅ All system dependencies already available"
fi

# 2) Verify Java version for Synthea compatibility
echo "🔍 Verifying Java installation..."
java_version=$(java -version 2>&1 | head -n 1 | cut -d'"' -f2 | cut -d'.' -f1)
if [ "$java_version" -ge 11 ]; then
    echo "  ✅ Java $java_version is compatible with Synthea"
else
    echo "  ⚠️  Java $java_version may not be fully compatible with Synthea (requires Java 11+)"
fi

# 3) Restore dependencies
echo "📥 Restoring .NET dependencies..."
dotnet restore --nologo
echo "  ✅ Dependencies restored"

# 4) Build the solution for both Release and Debug configurations
# Integration tests look for both Release and Debug builds
echo "🔨 Building Synthea CLI..."

# Build Release configuration (for production use)
echo "  - Building Release configuration..."
dotnet build --no-restore -c Release --nologo
echo "    ✅ Release build completed"

# Build Debug configuration (for integration tests)
echo "  - Building Debug configuration..."
dotnet build --no-restore -c Debug --nologo
echo "    ✅ Debug build completed"

# 5) Verify integration test dependencies
echo "🧪 Verifying integration test setup..."
debug_dll_path="artifacts/bin/Debug/net8.0/Synthea.Cli.dll"
release_dll_path="artifacts/bin/Release/net8.0/Synthea.Cli.dll"

if [ -f "$debug_dll_path" ]; then
    echo "  ✅ Debug CLI available at: $debug_dll_path"
else
    echo "  ❌ Debug CLI not found at: $debug_dll_path"
fi

if [ -f "$release_dll_path" ]; then
    echo "  ✅ Release CLI available at: $release_dll_path"
else
    echo "  ❌ Release CLI not found at: $release_dll_path"
fi

# 6) Run tests to verify everything works
echo "🧪 Running tests to verify setup..."
if dotnet test --no-build --verbosity minimal; then
    echo "  ✅ All tests passed successfully"
else
    echo "  ⚠️  Some tests failed - check output above for details"
fi

# 7) Setup complete
echo ""
echo "🎉 Synthea CLI setup complete!"
echo ""
echo "📋 Available commands:"
echo "  • Run CLI (Release):  dotnet artifacts/bin/Release/net8.0/Synthea.Cli.dll run --help"
echo "  • Run CLI (Debug):    dotnet artifacts/bin/Debug/net8.0/Synthea.Cli.dll run --help"
echo "  • Run all tests:      dotnet test"
echo "  • Build solution:     dotnet build"
echo ""
echo "🔧 Environment ready for ChatGPT Codex development!"
echo ""
echo "📝 Example usage:"
echo "  dotnet artifacts/bin/Release/net8.0/Synthea.Cli.dll run -o /tmp/out --state OH -p 10"
