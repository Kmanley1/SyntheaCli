#!/usr/bin/env bash
set -euo pipefail

# 0) Tools dirs
export DOTNET_ROOT="$HOME/.dotnet"
export JAVA_HOME="$HOME/.jdk"
export PATH="$DOTNET_ROOT:$JAVA_HOME/bin:$PATH"

# 1) Install .NET 8 SDK if missing
if ! command -v dotnet >/dev/null; then
  curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0 --install-dir "$DOTNET_ROOT" --no-path
fi

# 2) Install Java 17 if missing
if ! command -v java >/dev/null; then
  curl -sSL "https://github.com/adoptium/temurin17-binaries/releases/latest/download/OpenJDK17U-jre_x64_linux_hotspot.tar.gz" \
  | tar -xz -C "$HOME"
  mv "$HOME/jdk-17" "$JAVA_HOME"
fi

# 3) Build + publish CLI
 dotnet restore --nologo Synthea.Cli.sln
 dotnet publish Synthea.Cli/Synthea.Cli.csproj -c Release -o /workspace/synthea-cli/bin

echo "✅ synthea-cli ready → dotnet /workspace/synthea-cli/bin/Synthea.Cli.dll run -o /tmp/out --state OH -p 10"