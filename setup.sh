#!/usr/bin/env bash
# setup.sh – build synthea-cli for the Codex runner (Ubuntu 22.04 base image)

set -euo pipefail

# 1) Ensure Java 17+ and .NET 8 are present. Skip apt if already installed.
packages=()
if ! command -v java >/dev/null; then
    packages+=(openjdk-17-jre-headless)
fi
if ! command -v dotnet >/dev/null; then
    packages+=(dotnet-sdk-8.0)
fi
# Some tests rely on the standard zip utilities
if ! command -v zip >/dev/null; then
    packages+=(zip unzip)
fi
if [ ${#packages[@]} -ne 0 ]; then
    sudo apt-get update -qq
    sudo apt-get install -y --no-install-recommends "${packages[@]}"
    sudo apt-get clean
    sudo rm -rf /var/lib/apt/lists/*
fi

# 2) Restore & publish the CLI
dotnet restore --nologo
dotnet publish Synthea.Cli/Synthea.Cli.csproj -c Release -o /workspace/synthea-cli/bin

echo "✅ synthea-cli built; run it with:"
echo "dotnet /workspace/synthea-cli/bin/Synthea.Cli.dll run -o /tmp/out --state OH -p 10"
