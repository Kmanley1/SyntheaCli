# 0002. JAR caching strategy

## Status
Accepted — `cache list` / `cache clear` surfaced in `3905e40` (PR #63).

## Context
The Synthea JAR is ~50 MB. Re-downloading on every run is unfriendly to
GitHub's anonymous rate limit (60 req/hour) and slows iteration. We
need a cache with three properties:

1. Survives across invocations and across reboots.
2. Discoverable by users without grepping source.
3. Inspectable and clearable without resorting to a file manager.

## Decision
- Store cached JARs under
  `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)/Synthea.Cli`.
  On Windows this resolves to `%LOCALAPPDATA%\Synthea.Cli`; on Linux
  and macOS the .NET runtime returns `~/.local/share/Synthea.Cli`. We
  rejected `XDG_CACHE_HOME` (`~/.cache/...`) — an earlier README
  claimed it, but the implementation never honoured it. Aligning the
  README to the implementation rather than the other way around keeps
  the cache path stable for existing users.
- `JarManager.TryFindCachedJar()` returns the newest `*with-dependencies.jar`
  in the cache directory without any network call.
- The cache directory path is exposed via `IJarSource.CachePath` so
  `synthea cache list` and `synthea cache clear` can operate on it
  without duplicating the path-resolution logic.
- `--jar` / `SYNTHEA_CLI_JAR_PATH` / `config.json.jarPath` bypasses the
  cache entirely (A-5).

## Consequences
- One JAR file per release version stays on disk until the user runs
  `synthea cache clear`. We do not auto-evict on age or size.
- Multi-user machines see one cache per user profile, matching the
  per-user `dotnet tool install` model.
- The cache path is not configurable via flag — `--jar` is the escape
  hatch for non-default locations.
