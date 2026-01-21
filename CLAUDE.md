# Societies - Project Configuration for Claude Code

## Godot Engine Location
The Godot 4.x executable is located at:
```
D:\Games (SSD)\SteamLibrary\steamapps\common\Godot Engine\godot.windows.opt.tools.64.exe
```

## Running Tests
To run the test suite, use this command:
```powershell
& "D:\Games (SSD)\SteamLibrary\steamapps\common\Godot Engine\godot.windows.opt.tools.64.exe" --headless --path "C:\Users\hunte\OneDrive\Desktop\AIExperiments\games\societies" --script tests/test_runner.gd
```

## Project Structure
- `sim/` - Core simulation code (GDScript)
- `tests/` - Test suite using custom test framework
- `viz/` - Visualization/UI code

## Key Files
- `sim/sim.gd` - Main simulation class
- `sim/sim_state.gd` - Simulation state container
- `sim/agent.gd` - Agent class with AI decision-making
- `sim/brains/default_brain.gd` - Utility-based AI brain
- `tests/run_tests.gd` - Test runner entry point

## Testing Notes
- Tests verify determinism via checksum comparison
- Save/load tests check JSON serialization round-trips
- Use `SimFixture.make_sim(seed)` for deterministic test setup
