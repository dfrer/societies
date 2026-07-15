using Godot;
using Societies.Simulation;
using System.Reflection;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class PrototypeDirectiveTests
    {
        [Fact]
        public void DirectiveBonus_IsConstantAndAffinityGated()
        {
            PrototypeWorkOrder food = Order("food", 760, PrototypeDirectiveAffinity.FoodAndFuel, "meal production");
            PrototypeWorkOrder shelter = Order("shelter", 760, PrototypeDirectiveAffinity.Shelter, "construction lumber");

            Assert.Equal(0.0f, PrototypeSettlementDirectiveCatalog.GetAssignmentScoreBonus(PrototypeSettlementDirective.Neutral, food));
            Assert.Equal(PrototypeSettlementDirectiveCatalog.AssignmentScoreBonus,
                PrototypeSettlementDirectiveCatalog.GetAssignmentScoreBonus(PrototypeSettlementDirective.FoodAndFuel, food));
            Assert.Equal(0.0f, PrototypeSettlementDirectiveCatalog.GetAssignmentScoreBonus(PrototypeSettlementDirective.FoodAndFuel, shelter));
            Assert.Equal(PrototypeSettlementDirectiveCatalog.AssignmentScoreBonus,
                PrototypeSettlementDirectiveCatalog.GetAssignmentScoreBonus(PrototypeSettlementDirective.Shelter, shelter));
        }

        [Fact]
        public void DirectiveAdjustedFrontier_RetainsBoostedOrder()
        {
            List<PrototypeWorkOrder> orders = Enumerable.Range(0, 50)
                .Select(index => Order($"neutral.{index:D2}", 700, PrototypeDirectiveAffinity.None, string.Empty))
                .Append(Order("shelter", 640, PrototypeDirectiveAffinity.Shelter, "construction lumber"))
                .ToList();

            List<PrototypeWorkOrder> raw = PrototypeExtractionPlanningMath.ApplyFrontierLimit(orders, 50, orders.Count);
            List<PrototypeWorkOrder> adjusted = PrototypeExtractionPlanningMath.ApplyFrontierLimit(
                orders,
                50,
                orders.Count,
                order => order.Priority + PrototypeSettlementDirectiveCatalog.GetAssignmentScoreBonus(
                    PrototypeSettlementDirective.Shelter,
                    order));

            Assert.DoesNotContain(raw, order => order.OrderId == "shelter");
            Assert.Contains(adjusted, order => order.OrderId == "shelter");
        }

        [Fact]
        public void DirectiveAdjustedExtractionBound_PreventsUnsoundClassOmission()
        {
            int[] existingPriorities = Enumerable.Repeat(700, 50).ToArray();
            string[] eligibleOrderIds = { "extract.logs_1" };
            HashSet<string> claimed = new(StringComparer.Ordinal);

            Assert.True(PrototypeExtractionPlanningMath.TryComputeWholeResourceClassOmission(
                existingPriorities,
                50,
                priorityUpperBound: 640,
                eligibleOrderIds,
                claimed,
                desiredUnits: 1,
                out int rawOmitted));
            Assert.Equal(1, rawOmitted);

            Assert.False(PrototypeExtractionPlanningMath.TryComputeWholeResourceClassOmission(
                existingPriorities,
                50,
                priorityUpperBound: 640 + (int)PrototypeSettlementDirectiveCatalog.AssignmentScoreBonus,
                eligibleOrderIds,
                claimed,
                desiredUnits: 1,
                out int adjustedOmitted));
            Assert.Equal(0, adjustedOmitted);
        }

        [Fact]
        public void DirectiveSelection_LabelsCauseOnlyWhenModifierChangesWinner()
        {
            PrototypeSettlementSimulation simulation = CreateSimulation(out _);
            PrototypeWorkerState citizen = simulation.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal).First();
            citizen.Position = citizen.HomePosition;

            PrototypeWorkOrder foodWinner = Order("food", 900, PrototypeDirectiveAffinity.FoodAndFuel, "meal production", citizen.Position);
            PrototypeWorkOrder shelterLoser = Order("shelter", 760, PrototypeDirectiveAffinity.Shelter, "construction lumber", citizen.Position);
            (string unchangedId, string unchangedReason) = Select(
                simulation,
                citizen,
                new[] { foodWinner, shelterLoser },
                PrototypeSettlementDirective.FoodAndFuel);

            Assert.Equal("food", unchangedId);
            Assert.DoesNotContain("Why:", unchangedReason, StringComparison.Ordinal);

            PrototypeWorkOrder shelterWinner = Order("shelter", 900, PrototypeDirectiveAffinity.Shelter, "construction lumber", citizen.Position);
            PrototypeWorkOrder foodLoser = Order("food", 760, PrototypeDirectiveAffinity.FoodAndFuel, "meal production", citizen.Position);
            (string changedId, string changedReason) = Select(
                simulation,
                citizen,
                new[] { shelterWinner, foodLoser },
                PrototypeSettlementDirective.FoodAndFuel);

            Assert.Equal("food", changedId);
            Assert.Contains("Why: Food & Fuel — meal production", changedReason, StringComparison.Ordinal);

            (changedId, changedReason) = Select(
                simulation,
                citizen,
                new[] { foodWinner, shelterLoser },
                PrototypeSettlementDirective.Shelter);

            Assert.Equal("shelter", changedId);
            Assert.Contains("Why: Shelter — construction lumber", changedReason, StringComparison.Ordinal);
        }

        [Fact]
        public void WoodYardProcessingCandidates_KeepFuelPriorityAndAllowShelterToChooseTimber()
        {
            PrototypeSettlementSimulation simulation = CreateSimulation(out _);
            PrototypeWorkerState citizen = simulation.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal).First();
            citizen.Position = citizen.HomePosition;

            PrototypeWorkOrder firewood = Order("process.wood_yard_1.firewood", 930, PrototypeDirectiveAffinity.FoodAndFuel, "fuel supply", citizen.Position);
            PrototypeWorkOrder timber = Order("process.wood_yard_1.timber", 760, PrototypeDirectiveAffinity.Shelter, "construction lumber", citizen.Position);

            Assert.Equal("process.wood_yard_1.firewood", Select(
                simulation,
                citizen,
                new[] { firewood, timber },
                PrototypeSettlementDirective.Neutral).OrderId);
            Assert.Equal("process.wood_yard_1.firewood", Select(
                simulation,
                citizen,
                new[] { firewood, timber },
                PrototypeSettlementDirective.FoodAndFuel).OrderId);
            Assert.Equal("process.wood_yard_1.timber", Select(
                simulation,
                citizen,
                new[] { firewood, timber },
                PrototypeSettlementDirective.Shelter).OrderId);
        }

        [Theory]
        [InlineData(PrototypeSettlementDirective.FoodAndFuel)]
        [InlineData(PrototypeSettlementDirective.Shelter)]
        public void CriticalNutrition_RemainsDominantOverDirectiveWork(PrototypeSettlementDirective directive)
        {
            PrototypeSettlementSimulation simulation = CreateSimulation(out List<PrototypeResourceSiteState> resources);
            PrototypeWorkerState citizen = IsolateFirstCitizen(simulation);
            citizen.Needs.Nutrition = 1.0f;
            citizen.Needs.Fatigue = 0.0f;

            simulation.Advance(resources, 8.0f, PrototypeWeather.Clear, directive: directive);

            Assert.Equal(PrototypeWorkOrderKind.Eat, citizen.CurrentOrderKind);
            Assert.DoesNotContain("Why:", citizen.CurrentOrderReason, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(PrototypeSettlementDirective.FoodAndFuel)]
        [InlineData(PrototypeSettlementDirective.Shelter)]
        public void CriticalFatigue_RemainsDominantOverDirectiveWork(PrototypeSettlementDirective directive)
        {
            PrototypeSettlementSimulation simulation = CreateSimulation(out List<PrototypeResourceSiteState> resources);
            PrototypeWorkerState citizen = IsolateFirstCitizen(simulation);
            citizen.Needs.Nutrition = 100.0f;
            citizen.Needs.Fatigue = 100.0f;

            simulation.Advance(resources, 22.0f, PrototypeWeather.Clear, directive: directive);

            Assert.Equal(PrototypeWorkOrderKind.Sleep, citizen.CurrentOrderKind);
            Assert.DoesNotContain("Why:", citizen.CurrentOrderReason, StringComparison.Ordinal);
        }

        [Fact]
        public void DirectiveChange_DoesNotCancelActiveWork()
        {
            PrototypeSettlementSimulation simulation = CreateSimulation(out List<PrototypeResourceSiteState> resources);
            simulation.Advance(resources, 8.0f, PrototypeWeather.Clear, directive: PrototypeSettlementDirective.Neutral);
            PrototypeWorkerState citizen = simulation.Workers.First(worker => !string.IsNullOrWhiteSpace(worker.CurrentOrderId));
            string orderId = citizen.CurrentOrderId;
            PrototypeWorkerPhase phase = citizen.Phase;

            simulation.Advance(resources, 8.01f, PrototypeWeather.Clear, directive: PrototypeSettlementDirective.Shelter);

            Assert.Equal(orderId, citizen.CurrentOrderId);
            Assert.Equal(phase, citizen.Phase);
        }

        [Fact]
        public void FixedSeedDirectives_ProduceMateriallyDifferentRelevantAssignmentMixes()
        {
            AssignmentMix neutral = RunAssignmentMix(PrototypeSettlementDirective.Neutral, 200);
            AssignmentMix foodAndFuel = RunAssignmentMix(PrototypeSettlementDirective.FoodAndFuel, 200);
            AssignmentMix shelter = RunAssignmentMix(PrototypeSettlementDirective.Shelter, 200);

            Assert.True(
                foodAndFuel.FoodAndFuelAssignments >= neutral.FoodAndFuelAssignments + 3,
                $"Expected Food & Fuel to materially increase relevant work; neutral={neutral}, food={foodAndFuel}.");
            Assert.True(
                shelter.ShelterAssignments >= neutral.ShelterAssignments + 3,
                $"Expected Shelter to materially increase relevant work; neutral={neutral}, shelter={shelter}.");
            Assert.NotEqual(foodAndFuel, shelter);
        }

        private static AssignmentMix RunAssignmentMix(PrototypeSettlementDirective directive, int ticks)
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("empty_stores");
            PrototypeRuntimeSession session = new(
                scenario,
                bundle.RoleQuotas.Roles,
                resourceDefinitions: bundle.Resources.Resources);
            session.Initialize(8.0f);
            session.SetDirective(directive);
            Dictionary<string, string> previousOrders = session.Workers
                .ToDictionary(worker => worker.WorkerId, _ => string.Empty, StringComparer.Ordinal);
            int foodAndFuelAssignments = 0;
            int shelterAssignments = 0;
            int totalAssignments = 0;

            for (int tick = 0; tick < ticks; tick++)
            {
                session.Advance((float)PrototypeSimulationTime.TickIntervalSeconds, 600.0f);
                foreach (PrototypeWorkerState worker in session.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(worker.CurrentOrderId) ||
                        string.Equals(previousOrders[worker.WorkerId], worker.CurrentOrderId, StringComparison.Ordinal))
                    {
                        previousOrders[worker.WorkerId] = worker.CurrentOrderId;
                        continue;
                    }

                    previousOrders[worker.WorkerId] = worker.CurrentOrderId;
                    totalAssignments++;
                    if (IsFoodAndFuelOrder(worker.CurrentOrderId))
                    {
                        foodAndFuelAssignments++;
                    }
                    if (IsShelterOrder(worker.CurrentOrderId))
                    {
                        shelterAssignments++;
                    }
                }
            }

            return new AssignmentMix(foodAndFuelAssignments, shelterAssignments, totalAssignments);
        }

        private static bool IsFoodAndFuelOrder(string orderId)
        {
            return orderId.StartsWith("refuel", StringComparison.Ordinal) ||
                orderId.Contains("berries", StringComparison.Ordinal) ||
                orderId.Contains("meals", StringComparison.Ordinal) ||
                orderId.Contains("firewood", StringComparison.Ordinal);
        }

        private static bool IsShelterOrder(string orderId)
        {
            return orderId.Contains("logs", StringComparison.Ordinal) ||
                orderId.Contains("timber", StringComparison.Ordinal) ||
                orderId.Contains("reeds", StringComparison.Ordinal) ||
                orderId.Contains("thatch", StringComparison.Ordinal) ||
                orderId.Contains("hut", StringComparison.Ordinal);
        }

        private readonly record struct AssignmentMix(
            int FoodAndFuelAssignments,
            int ShelterAssignments,
            int TotalAssignments);

        private static PrototypeWorkerState IsolateFirstCitizen(PrototypeSettlementSimulation simulation)
        {
            PrototypeWorkerState citizen = simulation.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal).First();
            foreach (PrototypeWorkerState other in simulation.Workers.Where(worker => worker != citizen))
            {
                other.Phase = PrototypeWorkerPhase.Incapacitated;
            }

            citizen.Phase = PrototypeWorkerPhase.Idle;
            return citizen;
        }

        private static PrototypeWorkOrder Order(
            string orderId,
            int priority,
            PrototypeDirectiveAffinity affinity,
            string cause,
            Vector3? target = null)
        {
            return new PrototypeWorkOrder
            {
                OrderId = orderId,
                Kind = PrototypeWorkOrderKind.Repath,
                Priority = priority,
                Reason = $"reason for {orderId}",
                DirectiveAffinity = affinity,
                DirectiveCause = cause,
                TargetPosition = target ?? Vector3.Zero
            };
        }

        private static (string OrderId, string CurrentOrderReason) Select(
            PrototypeSettlementSimulation simulation,
            PrototypeWorkerState citizen,
            IReadOnlyList<PrototypeWorkOrder> orders,
            PrototypeSettlementDirective directive)
        {
            MethodInfo method = typeof(PrototypeSettlementSimulation).GetMethod(
                "SelectDirectiveOrderForTesting",
                BindingFlags.Instance | BindingFlags.NonPublic) ??
                throw new MissingMethodException("SelectDirectiveOrderForTesting");
            object probe = method.Invoke(simulation, new object[] { citizen, orders, directive }) ??
                throw new InvalidOperationException("Directive selection probe returned null.");
            Type probeType = probe.GetType();
            return (
                (string)(probeType.GetProperty("OrderId")?.GetValue(probe) ?? string.Empty),
                (string)(probeType.GetProperty("CurrentOrderReason")?.GetValue(probe) ?? string.Empty));
        }

        private static PrototypeSettlementSimulation CreateSimulation(out List<PrototypeResourceSiteState> resources)
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            resources = world.ResourceSpawns
                .GroupBy(spawn => spawn.ResourceId)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .SelectMany(group => group.Select((spawn, index) => new PrototypeResourceSiteState(
                    $"{spawn.ResourceId}_{index + 1}",
                    spawn.ResourceId,
                    spawn.Position,
                    spawn.UnitsRemaining,
                    spawn.ClusterId)))
                .ToList();
            return new PrototypeSettlementSimulation(scenario, bundle.RoleQuotas.Roles, world);
        }

        private static PrototypeCatalogBundle LoadCatalogs()
        {
            string? current = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, "src", "societies", "data");
                if (Directory.Exists(candidate))
                {
                    return PrototypeCatalogLoader.LoadFromDirectory(candidate);
                }

                current = Directory.GetParent(current)?.FullName;
            }

            throw new DirectoryNotFoundException("Could not find src/societies/data.");
        }
    }
}
