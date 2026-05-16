// Parallel test execution is enabled by default; the legacy
// CollectionBehavior(DisableTestParallelization = true) flag was removed in
// the A-12 DI refactor once Program.Runner / EnsureJarAsyncFunc / JarManager.Http
// / JarManager.CacheRootOverride stopped being mutable-static.
