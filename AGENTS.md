# AGENTS.md - Societies Project

## Overview

Societies is an AI-powered multiplayer civilization simulation game where humans and AI agents coexist as equal citizens. Built with Godot 4 + C#.

## Project Structure

- `src/societies/` - Godot project with C# scripts
- `planning/` - Comprehensive 7-session planning structure
- `tests/` - C# unit tests and Godot integration tests

## Current Build Reality

- The authoritative build is the Godot project under `src/societies/`
- The current default branch in this repository is `master`
- Use `CURRENT_BUILD.md` as the repo-truth summary before assuming stale planning still reflects implementation
- Treat `planning/` as aspirational unless the current Godot code confirms it

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
- Commit planning documents to `planning/sessions/`

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
