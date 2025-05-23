# Changelog

## [0.2.0] - 2025-05-23
- Numerous CLI enhancements compared to v0.1.0:
  - population size and random seed options
  - gender, age range, module, and ZIP filters
  - configuration file and FHIR version support
  - snapshot management (`--initial-snapshot`, `--updated-snapshot`, `--days-forward`)
  - output format selection (CSV, FHIR, etc.)
- Windows `nuget-helper.ps1` script and improved `setup.sh` logic
- Refactored `Program` and expanded unit tests
- Update README and bump package version to 0.2.0.

## [0.1.0] - Initial nuget-tool release
- First packaged release of the synthea-cli .NET global tool.
