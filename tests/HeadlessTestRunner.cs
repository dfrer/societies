using Godot;
using System;
using System.Reflection;

namespace Societies.Tests
{
    /// <summary>
    /// Headless test runner for Godot 4.x
    /// Run with: godot --headless --script tests/HeadlessTestRunner.cs
    /// </summary>
    public partial class HeadlessTestRunner : Node
    {
        private int _passed = 0;
        private int _failed = 0;

        public override void _Ready()
        {
            GD.Print("╔══════════════════════════════════════════════════════════╗");
            GD.Print("║       Societies Headless Test Runner                     ║");
            GD.Print("║       Godot 4.x + C# Testing Framework                   ║");
            GD.Print("╚══════════════════════════════════════════════════════════╝");
            GD.Print("");

            try
            {
                RunAllTests();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Test runner crashed: {ex}");
                _failed++;
            }

            // Report results
            GD.Print("");
            GD.Print("═══════════════════════════════════════════════════════════");
            GD.Print($"Test Results: {_passed} passed, {_failed} failed");
            GD.Print("═══════════════════════════════════════════════════════════");

            // Exit with appropriate code
            int exitCode = _failed > 0 ? 1 : 0;
            GD.Print($"Exiting with code: {exitCode}");
            
            GetTree().Quit(exitCode);
        }

        private void RunAllTests()
        {
            // Test 1: Entity State Serialization
            Test_EntityState_Serialization();
            
            // Test 2: Vector3 Operations
            Test_Vector3_Operations();
            
            // Test 3: Node Creation
            Test_Node_Creation();
            
            // Test 4: Scene Tree Access
            Test_SceneTree_Access();
        }

        private void Test_EntityState_Serialization()
        {
            try
            {
                var state = new EntityState
                {
                    EntityId = "test-entity",
                    EntityType = "player",
                    Position = new Vector3(10, 5, 20),
                    Rotation = new Vector3(0, 90, 0),
                    Velocity = new Vector3(1, 0, 1),
                    Timestamp = DateTime.UtcNow.Ticks
                };

                Assert(state.EntityId == "test-entity", "Entity ID mismatch");
                Assert(state.Position == new Vector3(10, 5, 20), "Position mismatch");
                
                _passed++;
                GD.Print("✓ Test_EntityState_Serialization");
            }
            catch (Exception ex)
            {
                _failed++;
                GD.PrintErr($"✗ Test_EntityState_Serialization: {ex.Message}");
            }
        }

        private void Test_Vector3_Operations()
        {
            try
            {
                var v1 = new Vector3(10, 20, 30);
                var v2 = new Vector3(5, 10, 15);
                var sum = v1 + v2;
                
                Assert(sum == new Vector3(15, 30, 45), "Vector addition failed");
                
                var lerped = v1.Lerp(v2, 0.5f);
                Assert(lerped == new Vector3(7.5f, 15, 22.5f), "Vector lerp failed");
                
                _passed++;
                GD.Print("✓ Test_Vector3_Operations");
            }
            catch (Exception ex)
            {
                _failed++;
                GD.PrintErr($"✗ Test_Vector3_Operations: {ex.Message}");
            }
        }

        private void Test_Node_Creation()
        {
            try
            {
                var node = new Node();
                node.Name = "TestNode";
                
                Assert(node.Name == "TestNode", "Node name mismatch");
                Assert(node.IsInsideTree() == false, "Node should not be in tree yet");
                
                AddChild(node);
                
                Assert(node.IsInsideTree() == true, "Node should be in tree after adding");
                Assert(node.GetParent() == this, "Parent should be this runner");
                
                node.QueueFree();
                
                _passed++;
                GD.Print("✓ Test_Node_Creation");
            }
            catch (Exception ex)
            {
                _failed++;
                GD.PrintErr($"✗ Test_Node_Creation: {ex.Message}");
            }
        }

        private void Test_SceneTree_Access()
        {
            try
            {
                var tree = GetTree();
                Assert(tree != null, "Scene tree should be accessible");
                
                var root = tree.Root;
                Assert(root != null, "Root node should exist");
                
                var currentScene = tree.CurrentScene;
                // In headless mode with --script, there might not be a current scene
                // This is expected behavior
                
                _passed++;
                GD.Print("✓ Test_SceneTree_Access");
            }
            catch (Exception ex)
            {
                _failed++;
                GD.PrintErr($"✗ Test_SceneTree_Access: {ex.Message}");
            }
        }

        private void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"Assertion failed: {message}");
            }
        }
    }
}
