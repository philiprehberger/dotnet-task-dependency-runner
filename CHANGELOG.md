# Changelog

## 0.2.5 (2026-03-22)

- Add dates to changelog entries

## 0.2.4 (2026-03-20)

- Align README and csproj descriptions

## 0.2.3 (2026-03-16)

- Add Development section to README
- Add GenerateDocumentationFile and RepositoryType to .csproj

## 0.2.0 (2026-03-12)

### Added

- `MaxConcurrency` option to limit parallel task execution
- Per-task timeout support with `TaskTimeoutException`
- `OnTaskCompleted` progress callback reporting task name and completion count

## 0.1.1 (2026-03-10)

- Fix README path in csproj so README displays on nuget.org

## 0.1.0 (2026-03-10)

- Initial release
- `TaskGraph.Add` — register sync and async tasks with named dependencies
- `TaskGraph.GetExecutionOrder` — return a valid topological order
- `TaskGraph.RunAsync` — execute all tasks in parallel where possible
- `CircularDependencyException` — raised on cycle detection
- `MissingDependencyException` — raised on unknown dependency reference
