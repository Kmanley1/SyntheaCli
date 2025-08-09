# Changelog

## [0.3.0] - 2025-08-08
### Major Enhancements
- **Complete .NET CLI Implementation**: Added comprehensive `RunCommand` and `RunOptions` classes with full parameter validation and System.CommandLine integration
- **Process Management**: Implemented robust `ProcessHelpers` class for Java process execution with proper error handling and output capture
- **JAR Management**: Enhanced `JarManager` with automatic Synthea JAR downloading, caching, and validation
- **Java Integration**: Full compatibility with Java 11-24, with automatic Java detection and path resolution

### Testing & Quality Improvements  
- **Comprehensive Test Suite**: 44 total tests (40 unit tests + 4 integration tests)
- **Integration Test Fixes**: Resolved test isolation issues with unique output directories per test run
- **End-to-End Testing**: Full integration testing with actual Synthea JAR execution
- **Test Environment Setup**: Automated Java dependency management for CI/CD

### Build & Environment
- **Enhanced Setup Scripts**: Completely rewritten `setup.sh` for Linux/Ubuntu with ChatGPT Codex optimization
- **Windows Setup Support**: New `setup.ps1` script with automatic Java installation via winget
- **Centralized Build Output**: Improved build configuration using `Directory.Build.props` with artifacts directory
- **Environment Validation**: Comprehensive environment checking and dependency installation

### CLI Features & Validation
- **Parameter Validation**: Complete validation for all Synthea parameters including:
  - State code validation (two-letter codes)
  - ZIP code format validation (5 digits or 5+4)
  - Gender validation (M/F)
  - Age range validation (min-max format)
  - FHIR version validation (R4/STU3)
  - File path existence validation
- **Error Handling**: User-friendly error messages with helpful guidance
- **Help System**: Comprehensive help text with examples and parameter descriptions

### Documentation & Developer Experience
- **ChatGPT Codex Integration**: Added comprehensive `CODEX-SETUP.md` documentation
- **Setup Documentation**: Detailed setup instructions for multiple platforms
- **Feature Parity Analysis**: Comprehensive analysis confirming 85-90% feature parity with Java Synthea
- **Troubleshooting Guide**: Complete troubleshooting documentation for common issues

### Technical Architecture
- **Modular Design**: Clean separation of concerns with dedicated classes for commands, options, JAR management, and process handling
- **Async Processing**: Proper async/await patterns for file operations and process execution
- **Resource Management**: Proper disposal patterns and resource cleanup
- **Cross-Platform Support**: Full Windows, Linux, and macOS compatibility

### Bug Fixes
- **Test Isolation**: Fixed integration test interference by using unique output directories
- **File Path Resolution**: Improved CLI binary discovery for integration tests
- **Java Version Compatibility**: Enhanced Java version detection and validation
- **Build Output Paths**: Corrected artifact output paths for consistent builds

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
