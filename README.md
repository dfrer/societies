# Societies

A persistent multiplayer civilization simulation where human players and AI agents coexist as equal citizens in a living ecosystem.

[![Godot 4.x](https://img.shields.io/badge/Godot-4.x-blue.svg)](https://godotengine.org)
[![C#](https://img.shields.io/badge/C%23-10.0-green.svg)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## 🎮 Overview

Societies is an ambitious multiplayer simulation game where you build a civilization alongside AI agents who are equal citizens—economically, politically, and socially. The world evolves continuously whether you're online or not, creating a truly persistent simulation.

### Key Features

- **AI-Human Equivalence**: AI agents participate fully in economy and governance
- **Persistent World**: Simulation continues 24/7 with or without players
- **Emergent Governance**: Create laws and constitutions that affect real AI populations
- **Environmental Simulation**: Ecosystem, pollution, and climate systems with real consequences
- **Progressive Society**: From homesteading to planetary federations
- **Godot 4 + C#**: Modern, performant, open-source stack

## 📁 Project Structure

```
societies/
├── README.md                    # This file
├── planning/                    # All planning documents
│   ├── week1-deep-planning/     # 7-day planning sprint docs
│   │   ├── day1-technical-architecture.md
│   │   ├── day2-ai-system-design.md
│   │   ├── day3-core-gameplay-loops.md
│   │   ├── day4-progression-and-balance.md
│   │   ├── day5-governance-mechanics.md
│   │   ├── day6-prototyping-roadmap.md
│   │   └── day7-master-development-plan.md
│   ├── research/                # Research materials
│   │   ├── game-analysis-research-guide.md
│   │   ├── technical-postmortems-research-guide.md
│   │   └── agent-research-prompts.md
│   ├── meta/                    # Vision and methodology
│   │   ├── societies-meta-planning.md
│   │   └── societies-comprehensive-breakdown.md
│   └── spreadsheets/            # Excel templates
│       ├── tech-stack-comparison.md
│       ├── resource-economy-balance.md
│       ├── progression-timeline.md
│       └── risk-assessment.md
│
├── src/                         # Source code
│   └── societies/               # Godot project
│       ├── project.godot        # Godot project config
│       ├── Societies.csproj     # C# project file
│       ├── scenes/              # Godot scene files
│       ├── scripts/             # C# scripts
│       │   ├── core/            # Core systems
│       │   ├── multiplayer/     # Networking
│       │   ├── simulation/      # World simulation
│       │   ├── agents/          # AI behavior
│       │   ├── economy/         # Trading/markets
│       │   ├── governance/      # Laws/politics
│       │   └── utils/           # Helpers
│       ├── assets/              # Game assets
│       └── resources/           # Godot resources
│
└── docs/                        # Documentation
    └── (generated from planning)
```

## 🚀 Quick Start

### Prerequisites

- **Godot 4.x** with C# support ([Download](https://godotengine.org/download))
- **.NET 6.0 SDK** ([Download](https://dotnet.microsoft.com/download))
- **IDE**: Visual Studio, VS Code, or Rider
- **PostgreSQL** (for production servers, optional for development)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/societies.git
   cd societies
   ```

2. **Open in Godot**
   - Launch Godot 4.x
   - Import project from `src/societies/`
   - Let Godot import assets and generate C# solution

3. **Build the project**
   - In Godot: Click "Build" button
   - Or run: `dotnet build src/societies/Societies.csproj`

4. **Run the game**
   - Press F5 in Godot
   - Or run the compiled executable

### Running Modes

**Single Player (Offline)**
```csharp
// Game automatically starts local server
GameManager.Instance.StartSinglePlayer();
```

**Multiplayer Server**
```csharp
// Start dedicated server
GameManager.Instance.StartServer(port: 7777);
```

**Multiplayer Client**
```csharp
// Connect to server
GameManager.Instance.ConnectToServer("127.0.0.1", 7777);
```

## 📖 Documentation

### Planning Documents

All planning is in `/planning/` directory:

1. **Technical Architecture** (`day1-technical-architecture.md`)
   - System design, Godot + ENet networking
   - PostgreSQL/SQLite database design
   - Performance budgets and scalability

2. **AI System Design** (`day2-ai-system-design.md`)
   - Agent decision-making architecture
   - Memory and goal systems
   - Population elasticity

3. **Core Gameplay Loops** (`day3-core-gameplay-loops.md`)
   - Moment-to-moment activities
   - Player archetypes
   - Session flows

4. **Progression & Balance** (`day4-progression-and-balance.md`)
   - Technology tree
   - Resource economy
   - Threat timeline

5. **Governance Mechanics** (`day5-governance-mechanics.md`)
   - Law systems
   - Voting and elections
   - Anti-griefing protections

6. **Prototyping Roadmap** (`day6-prototyping-roadmap.md`)
   - 6-month build plan
   - 5 prototypes leading to Alpha
   - Validation criteria

7. **Master Development Plan** (`day7-master-development-plan.md`)
   - Integration map
   - Risk management
   - Resource requirements

### Research Guides

Ready-to-use research prompts in `/planning/research/`:

- **Game Analysis**: Deep dives into Eco, Dwarf Fortress, Paradox games
- **Technical Postmortems**: GDC talks and case studies
- **Agent Prompts**: Delegation templates for research tasks

### Spreadsheets

Excel templates in `/planning/spreadsheets/` (convert .md to .xlsx):

- **Tech Stack Comparison**: Decision matrices with scoring
- **Resource Economy Balance**: Production chains, consumption rates
- **Progression Timeline**: Tech tree, milestones, server lifecycle
- **Risk Assessment**: Risk matrices, mitigation strategies

## 🛠️ Tech Stack

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| **Engine** | Godot 4.x + C# | Free, excellent multiplayer, native C# |
| **Networking** | Godot ENet | UDP-based, low latency, built-in RPC |
| **Database (Prod)** | PostgreSQL | Complex relational data, JSON support |
| **Database (Dev)** | SQLite | Zero setup, file-based |
| **Server OS** | Linux (Ubuntu) | Stable, headless Godot support |
| **Version Control** | Git + GitHub | Collaboration, CI/CD |

## 📅 Development Timeline

```
Month 1:   Prototype 1 - Basic World & Simulation
Month 2:   Prototype 2 - AI Agents & Economy
Month 3:   Prototype 3 - Basic Governance
Month 4:   Prototype 4 - Progression & Threats
Month 5:   Prototype 5 - Environmental Systems
Month 6:   Alpha - Integration & Testing
Month 7-12: Beta - Balancing & Polish
Month 13+: Release
```

See `planning/week1-deep-planning/day6-prototyping-roadmap.md` for detailed plan.

## 🤝 Contributing

This is currently a solo project, but contributions will be welcome post-Alpha.

### For Now

1. **Playtest** - Try prototypes and provide feedback
2. **Research** - Use the research guides to analyze reference games
3. **Spread the word** - Share the project with interested developers

### Future

- Open source after release
- Community mods supported
- Developer documentation

## 🎯 Current Status

**Phase**: Day 0 - Project Setup Complete ✅

**Completed**:
- ✅ Folder structure
- ✅ 7 planning document templates
- ✅ 4 Excel spreadsheet templates
- ✅ 3 research guides with agent prompts
- ✅ Godot project initialization
- ✅ Multiplayer foundation code

**Next**:
- Week 1: Fill out planning documents
- Delegate research tasks
- Begin Prototype 1 (Month 1)

## 📊 Key Metrics

**Scope**:
- World Size: 0.5-4 km²
- AI Agents: 100-200
- Players: 20-100 concurrent
- Dev Timeline: 18+ months

**Performance Targets**:
- Server: 20 ticks/second
- Client: 60+ FPS
- Network: <100ms latency
- Memory: <4GB server

## 📝 License

MIT License - See [LICENSE](LICENSE) file

## 🙏 Acknowledgments

**Inspirations**:
- **Eco** by Strange Loop Games - Environmental simulation
- **Dwarf Fortress** by Bay 12 Games - Agent simulation
- **Factorio** by Wube Software - Automation and optimization
- **Paradox Games** - Political systems

**Tools**:
- Godot Engine and community
- .NET ecosystem
- PostgreSQL team

## 📞 Contact

- **Issues**: [GitHub Issues](https://github.com/yourusername/societies/issues)
- **Discussions**: [GitHub Discussions](https://github.com/yourusername/societies/discussions)

---

## Navigation Quick Reference

**Getting Started**:
1. Read `planning/meta/societies-comprehensive-breakdown.md` for vision
2. Review `planning/week1-deep-planning/day1-technical-architecture.md` for tech
3. Check `planning/week1-deep-planning/day7-master-development-plan.md` for roadmap

**Research Tasks**:
- Delegate using `planning/research/agent-research-prompts.md`
- Track progress with research guides

**Development**:
- Code in `src/societies/`
- Use Godot 4.x with C#
- Follow planning documents

**Questions?**:
- Check planning documents first
- Look for TODO comments in code
- Create GitHub issue for discussion

---

**Ready to build the future of multiplayer simulation games.** 🚀
