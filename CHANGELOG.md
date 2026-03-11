# Changelog

## 0.1.1 (2026-03-10)

- Fix README path in csproj so README displays on nuget.org

## 0.1.0 (2026-03-10)

- Initial release
- `TaskGraph.Add` — register sync and async tasks with named dependencies
- `TaskGraph.GetExecutionOrder` — return a valid topological order
- `TaskGraph.RunAsync` — execute all tasks in parallel where possible
- `CircularDependencyException` — raised on cycle detection
- `MissingDependencyException` — raised on unknown dependency reference
