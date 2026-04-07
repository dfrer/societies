
using Godot;
using Societies.Core;
using System;
using System.Collections.Generic;
using System.IO;

namespace Societies.Tests
{
    /// <summary>
    /// Dedicated perf runner for the authoritative Godot runtime.
    /// Run with: godot --headless --path src/societies res://tests/PerfRunner.tscn
    ///
    /// Outputs:
    /// - perf-frame-timings.csv in the run output directory
    /// - perf-summary.txt with session total stats
    /// - prints to stdout for pipeline capture
    /// </summary>
    public partial class PerfRunner : Node
    {
        public override void _Ready()
        {
            GD.Print("=== Societies Runtime Perf Runner ===");

            // Enable perf metrics regardless of env var
            RuntimeFrameMetrics.Instance.IsEnabled = true;
            GD.Print("Perf metrics enabled");

            // Create a simple test: load main scene, run 300 ticks, save, report
            RunPerfTestAsync();
        }

        private async void RunPerfTestAsync()
        {
            try
            {
                var outputDir = CreateOutputDir();
                System.Environment.SetEnvironmentVariable("SOCIETIES_RUN_OUTPUT_DIR", outputDir);

                GD.Print($"Output directory: {outputDir}");

                // Load the main scene
                var packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                if (packedScene == null)
                {
                    GD.PrintErr("ERROR: Failed to load main.tscn");
                    GetTree().Quit(1);
                    return;
                }

                var scene = packedScene.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                var manager = scene as GameManager;
                if (manager == null)
                {
                    GD.PrintErr("ERROR: Main scene root is not GameManager");
                    GetTree().Quit(1);
                    return;
                }

                // Enable perf metrics on the manager side (env var may have worked)
                RuntimeFrameMetrics.Instance.IsEnabled = true;

                GD.Print($"Scenario: {manager.CurrentScenarioId}, Scenario: {manager.CurrentWorldSeed}");
                GD.Print($"Starting tick: {manager.SimulationTick}");

                // Run the soak: 300 ticks (short enough to complete, long enough for evidence)
                int tickCount = 300;
                GD.Print($"Running {tickCount} simulation ticks...");

                var timerStart = Time.GetTicksMsec();
                manager.StepSimulationTicks(tickCount);
                var timerElapsed = Time.GetTicksMsec() - timerStart;

                GD.Print($"Completed {tickCount} ticks in {timerElapsed}ms wall clock");
                GD.Print($"Current tick: {manager.SimulationTick}");

                // Capture snapshot (this triggers perf CSV export)
                string snapshotPath = manager.SaveSnapshotToDisk();
                GD.Print($"Snapshot saved: {snapshotPath}");

                // Print the RuntimeFrameMetrics summary
                GD.Print(RuntimeFrameMetrics.Instance.BuildSessionSummary());

                // Print the last frame summary for detail
                GD.Print(RuntimeFrameMetrics.Instance.BuildFrameSummary());

                GD.Print($"=== Perf Runner Complete ===");
                GD.Print($"Pass: {tickCount} ticks in {timerElapsed}ms");

                // Export per-tick spike diagnostics
                var diagnosticsCsvPath = Path.Combine(outputDir, "tick-diagnostics.csv");
                manager.ExportTickDiagnostics(diagnosticsCsvPath);
                GD.Print($"Tick diagnostics CSV: {diagnosticsCsvPath} (exists={File.Exists(diagnosticsCsvPath)})");

                if (File.Exists(diagnosticsCsvPath))
                {
                    var diagLines = File.ReadAllLines(diagnosticsCsvPath);
                    GD.Print($"=== tick-diagnostics.csv ({diagLines.Length} lines) ===");
                    // Print header + first 5 data lines + any spike lines
                    for (int i = 0; i < Math.Min(6, diagLines.Length); i++)
                    {
                        GD.Print(diagLines[i]);
                    }
                    // Print all ticks where nav_invalidated=true or tick_wall_ms > 100
                    GD.Print("--- Spike ticks (nav_invalidated=true OR tick_wall_ms > 100ms) ---");
                    for (int i = 1; i < diagLines.Length; i++)
                    {
                        string line = diagLines[i];
                        string[] parts = line.Split(',');
                        if (parts.Length >= 3)
                        {
                            bool navInvalid = parts[2] == "True";
                            bool slowTick = double.TryParse(parts[1], out double wallMs) && wallMs > 100.0;
                            if (navInvalid || slowTick)
                            {
                                GD.Print($"  Line {i}: {line}");
                            }
                        }
                    }
                }

                // Check perf output file exists (if the metrics are enabled and the singleton captured data)
                var perfCsvPath = Path.Combine(outputDir, "perf-frame-timings.csv");
                var perfTxtPath = Path.Combine(outputDir, "perf-summary.txt");
                GD.Print($"Perf CSV exists: {File.Exists(perfCsvPath)} at {perfCsvPath}");
                GD.Print($"Perf TXT exists: {File.Exists(perfTxtPath)} at {perfTxtPath}");

                // If the file was written, print the first few lines
                if (File.Exists(perfTxtPath))
                {
                    var txt = File.ReadAllText(perfTxtPath);
                    GD.Print($"=== perf-summary.txt contents ===");
                    GD.Print(txt.Trim());
                }

                if (File.Exists(perfCsvPath))
                {
                    var csvLines = File.ReadAllLines(perfCsvPath);
                    GD.Print($"=== perf-frame-timings.csv ({csvLines.Length} lines) ===");
                    for (int i = 0; i < Math.Min(5, csvLines.Length); i++)
                    {
                        GD.Print(csvLines[i]);
                    }
                }

                GetTree().Quit(0);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Perf runner crashed: {ex.Message}");
                GD.PrintErr(ex.StackTrace);
                GetTree().Quit(1);
            }
        }

        private static string CreateOutputDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "societies-perf-godot");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
