---
name: test-godot
description: Test the Godot simulation game. Use this skill when the user asks to test, run, or validate the simulation, check for errors, or verify the visualizer works correctly.
allowed-tools: Bash, Read, Glob, Grep
---

# Test Godot Simulation

This skill runs the Godot simulation project to test for errors and validate functionality.

## Godot Configuration

- **Godot Path**: `D:\Games (SSD)\SteamLibrary\steamapps\common\Godot Engine\godot.windows.opt.tools.64.exe`
- **Project Path**: `C:\Users\hunte\OneDrive\Desktop\AIExperiments\games\societies\viz`

## Instructions

When testing the simulation, follow these steps:

### 1. Check for Parse Errors (Quick Validation)

Run Godot in headless mode to check for GDScript parse errors:

```bash
"D:/Games (SSD)/SteamLibrary/steamapps/common/Godot Engine/godot.windows.opt.tools.64.exe" --headless --path "C:/Users/hunte/OneDrive/Desktop/AIExperiments/games/societies/viz" --quit 2>&1
```

This will output any script parsing errors without launching the full editor.

### 2. Run Script Validation

To validate all scripts compile correctly:

```bash
"D:/Games (SSD)/SteamLibrary/steamapps/common/Godot Engine/godot.windows.opt.tools.64.exe" --headless --path "C:/Users/hunte/OneDrive/Desktop/AIExperiments/games/societies/viz" --check-only --quit 2>&1
```

### 3. Run the Headless Simulation

To run the simulation without GUI (tests the sim/ code):

```bash
"D:/Games (SSD)/SteamLibrary/steamapps/common/Godot Engine/godot.windows.opt.tools.64.exe" --headless --path "C:/Users/hunte/OneDrive/Desktop/AIExperiments/games/societies" --script res://sim/tests/run_headless.gd 2>&1
```

### 4. Launch the Visualizer (Interactive)

To launch the full visualizer for manual testing:

```bash
"D:/Games (SSD)/SteamLibrary/steamapps/common/Godot Engine/godot.windows.opt.tools.64.exe" --path "C:/Users/hunte/OneDrive/Desktop/AIExperiments/games/societies/viz" res://visualizer_main.tscn 2>&1
```

Note: This opens a window - use for interactive testing only.

## Common Error Patterns

When analyzing output, look for:

- **Parser Error**: GDScript syntax issues - check line numbers and fix
- **Cannot infer type**: Need explicit type annotations with `Dictionary.get()`
- **Could not resolve**: Missing class definitions or circular dependencies
- **Expected indented block**: Empty if/else/for blocks need a `pass` statement

## Testing Workflow

1. First run parse validation (step 1)
2. If errors found, report them and suggest fixes
3. If no errors, run headless simulation (step 3) to test sim logic
4. Report results including any runtime errors

## Notes

- The viz/ folder is the Godot project root for the visualizer
- The sim/ folder contains headless simulation code (no Node dependencies)
- Always capture stderr with `2>&1` to see error messages
