# Repository Guardrails

## Backend C# File Layout (Required)
- Use exactly one top-level `interface` per file.
- Use exactly one top-level `class` per file.
- File name must match the top-level type name.
- Keep file-scoped namespaces.
- Keep this rule for all files under `/api/src/Budget.Api`.

## Safety
- This application is production-running.
- Do not introduce destructive data operations.
- Prefer backward-compatible, additive changes.
