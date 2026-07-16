# AGENTS.md - Societies Project

## Overview

Societies is a Godot 4 + C# prototype working toward this product north star: "A deterministic civilization/ecology simulation where humans and AI citizens work, trade, negotiate, govern, and experience shared consequences."

This is future intent, not current implementation scope. Deterministic simulation owns world facts and every state-changing outcome. Future LLMs may interpret structured state, deliberate, communicate, summarize memory, and propose commands, but all changes must enter through validated deterministic commands/events. Humans remain consequential, and offline/model-failure fallback must preserve the simulation and replay. See [planning/PRODUCT-THESIS.md](planning/PRODUCT-THESIS.md).

## Project Structure

- `src/societies/` - Godot project with C# scripts
- `planning/` - Comprehensive 7-session planning structure
- `tests/` - C# unit tests and Godot integration tests

## Current Build Reality

- The authoritative build is the Godot project under `src/societies/`
- The current default branch in this repository is `master`
- Use `CURRENT_BUILD.md` as the repo-truth summary before assuming stale planning still reflects implementation
- Treat `planning/` as aspirational unless the current Godot code confirms it
- W2-02 atomic contribution, W2-03 directive causality, and W2-04 deterministic outcomes/minimal crisis HUD are validated and merged; W2-04 landed through PR #117 at master `d519d4d`. V3-W2-VIS implementation/functional tests are complete by explicit exception recorded in `CURRENT_BUILD.md`; performance safety did not pass (p95 55.4529 ms vs 50 ms, max 142.9547 ms vs 250 ms), timing-only persistence/hash equivalence is unavailable, and Windows Forward+ PNG readback intermittently clips CanvasLayer glyphs/panels despite passing HUD assertions, so visual acceptance is waived, not passed. W2-05 is next but inactive/not implemented; Weeks 3-4 remain inactive until W2-06 records Continue V3.

## Tech Stack

- **Engine**: Godot 4.x + C#
- **Networking**: Local session in the current prototype, ENet deferred
- **Persistence**: Local JSON snapshot/event-log/run-summary output in the current prototype
- **Testing**: .NET Test + Godot headless runner
- **Planning**: Markdown-based documentation

## Development Workflow

### For Planning
1. Review existing session documents in `planning/sessions/`
2. Create or update planning documents collaboratively
3. Ensure cross-session dependencies are documented
4. Maintain version control through git

### For Development
1. Complete planning phase first
2. Implement features according to specifications
3. Follow existing code patterns and conventions
4. Run tests before committing

## Testing

```bash
# Run C# unit tests
dotnet test tests/Societies.Core.Tests/Societies.Core.Tests.csproj

# Run Godot headless tests (requires Godot)
godot --headless --path src/societies res://tests/HeadlessTestRunner.tscn
```

## Git Workflow

- Current default branch: `master`
- Feature branches: `feature/<description>`
- Commit planning documents to the relevant `planning/` location; short-horizon execution plans belong in `planning/active/`

## Planning Structure

The project uses a 7-session planning methodology:

1. **Session 1**: Technical Architecture
2. **Session 2**: AI System Design
3. **Session 3**: Core Gameplay Loops
4. **Session 4**: Progression & Balance
5. **Session 5**: Governance Mechanics
6. **Session 6**: Prototyping Roadmap
7. **Session 7**: Integration Master Plan

Each session is in `planning/sessions/session-N-<name>/`

## Resources

- Project README: `README.md`
- Planning Index: `planning/sessions/[AGENTS-READ-FIRST]-index.md`

## Key Commands Summary

```bash
# Build project
dotnet build src/societies/Societies.csproj

# Run tests
dotnet test tests/Societies.Core.Tests/Societies.Core.Tests.csproj

# Open in Godot
godot --path src/societies

# Run the full local validation loop
./scripts/run-prototype-validation.ps1
```

## Getting Started

When asked to work on planning documents, features, or research:
1. **Review existing context** in `planning/sessions/`
2. **Follow established patterns** from other session documents
3. **Document decisions** clearly with rationale
4. **Check dependencies** before making changes

This ensures:
- Systematic progress tracking
- No context loss across tasks
- Documented decision trail
- Consistent workflow
