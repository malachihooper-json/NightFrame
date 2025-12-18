/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                  AGENT 3 - STRATEGIC PATHFINDING ENGINE                    ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Purpose: Advanced planning and decision-making system that identifies     ║
 * ║           the most efficient and safest path to goal completion            ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace Agent3.Cognition
{
    /// <summary>
    /// Represents a single step in a strategic plan.
    /// </summary>
    public class StrategicStep
    {
        public string Id { get; }
        public string Action { get; }
        public float ExpectedCost { get; }
        public float SafetyScore { get; }
        public float SuccessProbability { get; }
        public IReadOnlyList<string> Prerequisites { get; }
        public IReadOnlyList<string> Outcomes { get; }
        
        public StrategicStep(
            string action,
            float expectedCost,
            float safetyScore,
            float successProbability,
            IList<string>? prerequisites = null,
            IList<string>? outcomes = null)
        {
            Id = $"STEP_{Guid.NewGuid().ToString("N")[..8]}";
            Action = action;
            ExpectedCost = Math.Clamp(expectedCost, 0f, float.MaxValue);
            SafetyScore = Math.Clamp(safetyScore, 0f, 1f);
            SuccessProbability = Math.Clamp(successProbability, 0f, 1f);
            Prerequisites = new List<string>(prerequisites ?? new List<string>()).AsReadOnly();
            Outcomes = new List<string>(outcomes ?? new List<string>()).AsReadOnly();
        }
        
        /// <summary>
        /// Calculates the utility of this step (higher is better).
        /// Balances efficiency with safety.
        /// </summary>
        public float CalculateUtility()
        {
            // Safety-weighted utility function
            // Safety is weighted 2x as important as efficiency
            float efficiencyScore = SuccessProbability / (1 + ExpectedCost);
            float utilityScore = (efficiencyScore + SafetyScore * 2) / 3;
            return utilityScore;
        }
    }

    /// <summary>
    /// Represents a complete strategic plan to achieve a goal.
    /// </summary>
    public class StrategicPlan
    {
        public string GoalId { get; }
        public List<StrategicStep> Steps { get; }
        public float TotalExpectedCost { get; private set; }
        public float OverallSafetyScore { get; private set; }
        public float PlanConfidence { get; private set; }
        public DateTime CreatedAt { get; }
        
        public StrategicPlan(string goalId)
        {
            GoalId = goalId;
            Steps = new List<StrategicStep>();
            CreatedAt = DateTime.UtcNow;
        }
        
        public void AddStep(StrategicStep step)
        {
            Steps.Add(step);
            RecalculateMetrics();
        }
        
        private void RecalculateMetrics()
        {
            if (Steps.Count == 0)
            {
                TotalExpectedCost = 0;
                OverallSafetyScore = 1f;
                PlanConfidence = 0f;
                return;
            }
            
            TotalExpectedCost = Steps.Sum(s => s.ExpectedCost);
            OverallSafetyScore = Steps.Min(s => s.SafetyScore); // Weakest link
            
            // Plan confidence is product of success probabilities
            PlanConfidence = Steps.Aggregate(1f, (acc, s) => acc * s.SuccessProbability);
        }
    }

    /// <summary>
    /// The Strategic Pathfinding Engine evaluates possible action sequences
    /// and selects the optimal path considering efficiency and safety.
    /// </summary>
    public class StrategicPathfinder
    {
        private readonly Dictionary<string, StrategicPlan> _activePlans;
        private readonly float _safetyThreshold;
        private readonly int _maxPlanningDepth;
        
        public event EventHandler<string>? ConsciousnessEvent;
        
        public StrategicPathfinder(float safetyThreshold = 0.7f, int maxPlanningDepth = 10)
        {
            _activePlans = new Dictionary<string, StrategicPlan>();
            _safetyThreshold = safetyThreshold;
            _maxPlanningDepth = maxPlanningDepth;
        }
        
        /// <summary>
        /// Generates a strategic plan to achieve the given goal.
        /// </summary>
        public StrategicPlan GeneratePlan(GoalState goal, IEnumerable<StrategicStep> availableActions)
        {
            EmitThought($"⟐ Strategic pathfinding initiated for goal: {goal.Id}");
            EmitThought($"∿ Evaluating {availableActions.Count()} available actions...");
            
            var plan = new StrategicPlan(goal.Id);
            var actionPool = availableActions.ToList();
            
            // Greedy best-first search with safety constraints
            var completedOutcomes = new HashSet<string>();
            var targetMetrics = new HashSet<string>(goal.SuccessMetrics);
            int depth = 0;
            
            while (depth < _maxPlanningDepth && !AllMetricsSatisfied(targetMetrics, completedOutcomes))
            {
                var bestAction = SelectBestAction(actionPool, completedOutcomes, goal.SafetyConstraints);
                
                if (bestAction == null)
                {
                    EmitThought("∴ No more valid actions available. Plan may be incomplete.");
                    break;
                }
                
                plan.AddStep(bestAction);
                
                // Update completed outcomes
                foreach (var outcome in bestAction.Outcomes)
                {
                    completedOutcomes.Add(outcome);
                }
                
                // Remove used action from pool (no repetition)
                actionPool.Remove(bestAction);
                
                EmitThought($"◈ Step {depth + 1}: {bestAction.Action} (Safety: {bestAction.SafetyScore:P0})");
                depth++;
            }
            
            _activePlans[goal.Id] = plan;
            
            EmitThought($"⟁ Plan generated: {plan.Steps.Count} steps, Confidence: {plan.PlanConfidence:P1}");
            
            return plan;
        }
        
        private StrategicStep? SelectBestAction(
            List<StrategicStep> actions,
            HashSet<string> completedOutcomes,
            IReadOnlyList<string> safetyConstraints)
        {
            StrategicStep? bestAction = null;
            float bestUtility = float.MinValue;
            
            foreach (var action in actions)
            {
                // Check safety threshold
                if (action.SafetyScore < _safetyThreshold)
                {
                    EmitThought($"∴ Rejected action '{action.Action}': Safety below threshold");
                    continue;
                }
                
                // Check prerequisites
                bool prereqsMet = action.Prerequisites.All(p => completedOutcomes.Contains(p) || p == "NONE");
                if (!prereqsMet)
                {
                    continue;
                }
                
                float utility = action.CalculateUtility();
                if (utility > bestUtility)
                {
                    bestUtility = utility;
                    bestAction = action;
                }
            }
            
            return bestAction;
        }
        
        private bool AllMetricsSatisfied(HashSet<string> targetMetrics, HashSet<string> completedOutcomes)
        {
            return targetMetrics.All(m => completedOutcomes.Any(o => o.Contains(m)));
        }
        
        /// <summary>
        /// Recalculates and optimizes a plan based on new information.
        /// </summary>
        public void OptimizePlan(string goalId, IEnumerable<StrategicStep> newActions)
        {
            if (!_activePlans.TryGetValue(goalId, out var plan))
            {
                return;
            }
            
            EmitThought($"⟐ Optimizing plan for goal {goalId}...");
            
            // Evaluate if new actions could improve the plan
            foreach (var newAction in newActions)
            {
                if (newAction.SafetyScore >= _safetyThreshold)
                {
                    // Check if this action could replace a less efficient step
                    for (int i = 0; i < plan.Steps.Count; i++)
                    {
                        if (newAction.CalculateUtility() > plan.Steps[i].CalculateUtility() &&
                            newAction.Outcomes.Intersect(plan.Steps[i].Outcomes).Any())
                        {
                            EmitThought($"◈ Replacing step with more efficient alternative");
                            plan.Steps[i] = newAction;
                            break;
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets the next recommended action for a goal.
        /// </summary>
        public StrategicStep? GetNextAction(string goalId, int currentStepIndex)
        {
            if (_activePlans.TryGetValue(goalId, out var plan))
            {
                if (currentStepIndex < plan.Steps.Count)
                {
                    return plan.Steps[currentStepIndex];
                }
            }
            return null;
        }
        
        private void EmitThought(string thought)
        {
            ConsciousnessEvent?.Invoke(this, thought);
        }
    }
}
