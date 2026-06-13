# syntax=docker/dockerfile:1
#
# synthea-cli — air-gapped container image.
#
# Bakes two things into one image so it runs with ZERO network access:
#   1. A self-contained linux-x64 build of the CLI (its own .NET runtime — the
#      host needs no .NET installed).
#   2. A pinned Synthea "with-dependencies" JAR, wired in via the documented
#      SYNTHEA_CLI_JAR_PATH override so JarManager never reaches out to GitHub.
#
# Build (latest Synthea JAR):
#   docker build -t synthea-cli .
# Build pinned to a specific Synthea release:
#   docker build --build-arg SYNTHEA_VERSION=v3.3.0 -t synthea-cli:syn-3.3.0 .
# Run (mount a host dir for output; --user keeps the bind mount writable and
# matches the host owner — Synthea writes under <mount>/output/):
#   docker run --rm -u "$(id -u):$(id -g)" -v "$PWD/out:/data" \
#       synthea-cli run -o /data -p 100 --state OH    # files land in ./out/output/

# Must be a Debian/Ubuntu-based SDK — the `jar` stage below uses apt-get.
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG JRE_IMAGE=eclipse-temurin:17-jre-jammy
# Synthea release tag to bake, e.g. v3.3.0. "latest" resolves to the newest
# upstream release at build time; the resulting image is immutable thereafter.
ARG SYNTHEA_VERSION=latest

# ---- Stage 1: publish a self-contained linux-x64 build of the CLI ----
FROM ${DOTNET_SDK_IMAGE} AS build
# Optional: trust a corporate/proxy root CA (e.g. Zscaler) so HTTPS NuGet
# restore works behind an SSL-inspecting proxy. ./certs has no .crt in clean
# CI → no-op. Drop a PEM .crt there to enable it (see certs/README.md).
COPY certs/ /tmp/corp-ca/
RUN cp /tmp/corp-ca/*.crt /usr/local/share/ca-certificates/ 2>/dev/null && update-ca-certificates || true
WORKDIR /src
COPY . .
RUN dotnet restore src/Synthea.Cli/Synthea.Cli.csproj
RUN dotnet publish src/Synthea.Cli/Synthea.Cli.csproj \
        -c Release -r linux-x64 --self-contained true \
        -p:PublishSingleFile=true -p:PackAsTool=false \
        -o /app

# ---- Stage 2: fetch the (pinned) Synthea JAR ----
FROM ${DOTNET_SDK_IMAGE} AS jar
ARG SYNTHEA_VERSION
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates \
 && rm -rf /var/lib/apt/lists/*
# Same optional corp-CA trust as the build stage (curl hits github.com over HTTPS).
COPY certs/ /tmp/corp-ca/
RUN cp /tmp/corp-ca/*.crt /usr/local/share/ca-certificates/ 2>/dev/null && update-ca-certificates || true
RUN set -eux; \
    mkdir -p /opt/synthea; \
    if [ "$SYNTHEA_VERSION" = "latest" ]; then \
        url="https://github.com/synthetichealth/synthea/releases/latest/download/synthea-with-dependencies.jar"; \
    else \
        url="https://github.com/synthetichealth/synthea/releases/download/${SYNTHEA_VERSION}/synthea-with-dependencies.jar"; \
    fi; \
    curl -fsSL --retry 3 --retry-delay 2 "$url" -o /opt/synthea/synthea-with-dependencies.jar; \
    # Guard against a 200-with-HTML rate-limit/abuse interstitial: the real JAR
    # is >100 MB, so anything under 10 MB is a failed/corrupt download.
    test "$(stat -c%s /opt/synthea/synthea-with-dependencies.jar)" -gt 10000000

# ---- Stage 3: minimal runtime — Java (for Synthea) + CLI + baked JAR ----
FROM ${JRE_IMAGE} AS runtime
LABEL org.opencontainers.image.title="synthea-cli" \
      org.opencontainers.image.description="Air-gapped Synthea generator: self-contained .NET CLI + a baked Synthea JAR." \
      org.opencontainers.image.source="https://github.com/Kmanley1/SyntheaCli" \
      org.opencontainers.image.licenses="MIT"

# Self-contained .NET 10 apps need a handful of native libs to start and to do
# TLS (the doctor reachability probe). The temurin jammy base pulls most in
# transitively, but install them explicitly so the image survives base drift.
# libicu70 is the ICU package for Ubuntu 22.04 (jammy).
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
        libicu70 libssl3 libgcc-s1 libstdc++6 zlib1g ca-certificates \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app /opt/synthea-cli
COPY --from=jar /opt/synthea/synthea-with-dependencies.jar /opt/synthea/synthea-with-dependencies.jar
RUN ln -s /opt/synthea-cli/Synthea.Cli /usr/local/bin/synthea

# JarManager.EnsureJarAsync short-circuits to this path — no GitHub call.
ENV SYNTHEA_CLI_JAR_PATH="/opt/synthea/synthea-with-dependencies.jar" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

# Run as non-root; uid 1000 matches the common host user so bind-mounted
# output dirs stay writable.
RUN useradd --uid 1000 --create-home --shell /usr/sbin/nologin synthea \
 && mkdir -p /data && chown synthea:synthea /data
USER synthea
WORKDIR /data

ENTRYPOINT ["synthea"]
CMD ["--help"]
