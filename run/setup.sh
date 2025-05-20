#!/usr/bin/env bash
# setup.sh – build synthea-cli for the Codex runner (Ubuntu 22.04 base image)

set -euo pipefail

# 1) Install runtimes (approx 150 MB)
sudo apt-get update -qq
sudo apt-get install -y --no-install-recommends \
    openjdk-17-jre-headless \
    dotnet-sdk-8.0 \
 && sudo apt-get clean && sudo rm -rf /var/lib/apt/lists/*

# 2) Restore & publish the CLI
dotnet restore --nologo
dotnet publish Synthea.Cli/Synthea.Cli.csproj -c Release -o /workspace/synthea-cli/bin

echo "✅ synthea-cli built; run it with:"
echo "dotnet /workspace/synthea-cli/bin/Synthea.Cli.dll run -o /tmp/out --state OH -p 10"
