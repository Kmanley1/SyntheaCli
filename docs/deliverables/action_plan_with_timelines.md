# Action Plan with Timelines

This document outlines the concrete steps for improving the `synthea-cli` project. It expands on the recommendations in the ongoing evaluation and organizes them by timeframe.

## Short Term (0-3 months)

- **Structured Logging** – Replace console writes with Microsoft.Extensions.Logging for consistent log output.
- **Data Handling Documentation** – Publish guidelines describing storage and retention of generated datasets.

## Medium Term (3-6 months)

- **Internal Artifact Mirror** – Host the Synthea JAR inside the organization to remove dependency on external GitHub availability.
- **CI Updates** – Modify CI scripts to pull the JAR from the mirror and push container images to an internal registry.

## Long Term (6+ months)

- **Containerized Distribution** – Provide an optional Docker image with the JAR pre‑downloaded to support offline or restricted networks.
- **Configuration Abstraction** – Introduce configuration files or environment variables for future expansion beyond the current `run` command.

[Download this file](./action_plan_with_timelines.md)
