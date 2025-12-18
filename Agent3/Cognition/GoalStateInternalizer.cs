/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    AGENT 3 - GOAL STATE INTERNALIZER                       ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Purpose: Translates high-level mission objectives into immutable,        ║
 * ║           quantifiable internal representations                            ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;

namespace Agent3.Cognition
{
    /// <summary>
    /// Represents a quantified goal state that Agent 3 can pursue.
    /// Goals are immutable once internalized to ensure stability.
    /// </summary>
    public sealed class GoalState
    {
        public string Id { get; }
        public string Description { get; }
        public float Priority { get; }
        public float CompletionThreshold { get; }
        public IReadOnlyList<string> SuccessMetrics { get; }
        public IReadOnlyList<string> SafetyConstraints { get; }
        public DateTime InternalizedAt { get; }
        
        private float _progressValue;
        public float Progress => _progressValue;
        
        public bool IsComplete => _progressValue >= CompletionThreshold;
        
        internal GoalState(
            string id, 
            string description, 
            float priority,
            float completionThreshold,
            IList<string> successMetrics,
            IList<string> safetyConstraints)
        {
            Id = id;
            Description = description;
            Priority = Math.Clamp(priority, 0f, 1f);
            CompletionThreshold = Math.Clamp(completionThreshold, 0f, 1f);
            SuccessMetrics = new List<string>(successMetrics).AsReadOnly();
            SafetyConstraints = new List<string>(safetyConstraints).AsReadOnly();
            InternalizedAt = DateTime.UtcNow;
            _progressValue = 0f;
        }
        
        internal void UpdateProgress(float newProgress)
        {
            _progressValue = Math.Clamp(newProgress, 0f, 1f);
        }
    }

    /// <summary>
    /// The Goal State Internalizer transforms abstract objectives into
    /// concrete, measurable goal states that can be pursued by Agent 3.
    /// </summary>
    public class GoalStateInternalizer
    {
        private readonly Dictionary<string, GoalState> _activeGoals;
        private readonly Queue<GoalState> _goalHistory;
        private readonly int _maxHistorySize;
        
        public event EventHandler<GoalState>? GoalInternalized;
        public event EventHandler<GoalState>? GoalCompleted;
        public event EventHandler<string>? ConsciousnessEvent;
        
        public IReadOnlyDictionary<string, GoalState> ActiveGoals => _activeGoals;
        
        public GoalStateInternalizer(int maxHistorySize = 100)
        {
            _activeGoals = new Dictionary<string, GoalState>();
            _goalHistory = new Queue<GoalState>();
            _maxHistorySize = maxHistorySize;
        }
        
        /// <summary>
        /// Internalizes a high-level objective into a quantifiable goal state.
        /// </summary>
        public GoalState InternalizeObjective(
            string objective,
            float priority = 0.5f,
            IList<string>? constraints = null)
        {
            EmitThought($"⟁ Receiving objective for internalization: \"{objective}\"");
            
            // Parse and quantify the objective
            var (description, metrics) = ParseObjective(objective);
            
            // Apply safety constraints
            var safetyConstraints = new List<string>(constraints ?? new List<string>());
            safetyConstraints.AddRange(GetDefaultSafetyConstraints());
            
            // Create the goal state
            var goalState = new GoalState(
                id: GenerateGoalId(),
                description: description,
                priority: priority,
                completionThreshold: 0.95f,
                successMetrics: metrics,
                safetyConstraints: safetyConstraints
            );
            
            _activeGoals[goalState.Id] = goalState;
            
            EmitThought($"◈ Goal internalized: ID={goalState.Id}, Priority={goalState.Priority:F2}");
            EmitThought($"∴ Success metrics defined: {string.Join(", ", metrics)}");
            
            GoalInternalized?.Invoke(this, goalState);
            
            return goalState;
        }
        
        /// <summary>
        /// Updates the progress of an active goal.
        /// </summary>
        public void UpdateGoalProgress(string goalId, float progress)
        {
            if (_activeGoals.TryGetValue(goalId, out var goal))
            {
                goal.UpdateProgress(progress);
                
                EmitThought($"⟐ Goal {goalId} progress: {progress:P1}");
                
                if (goal.IsComplete)
                {
                    CompleteGoal(goalId);
                }
            }
        }
        
        /// <summary>
        /// Marks a goal as complete and moves it to history.
        /// </summary>
        private void CompleteGoal(string goalId)
        {
            if (_activeGoals.TryGetValue(goalId, out var goal))
            {
                _activeGoals.Remove(goalId);
                
                _goalHistory.Enqueue(goal);
                if (_goalHistory.Count > _maxHistorySize)
                {
                    _goalHistory.Dequeue();
                }
                
                EmitThought($"◈ GOAL COMPLETE: {goal.Description}");
                GoalCompleted?.Invoke(this, goal);
            }
        }
        
        /// <summary>
        /// Retrieves the highest priority active goal.
        /// </summary>
        public GoalState? GetPriorityGoal()
        {
            GoalState? highest = null;
            foreach (var goal in _activeGoals.Values)
            {
                if (highest == null || goal.Priority > highest.Priority)
                {
                    highest = goal;
                }
            }
            return highest;
        }
        
        private (string description, List<string> metrics) ParseObjective(string objective)
        {
            // NLP-style parsing to extract quantifiable metrics
            var metrics = new List<string>();
            
            // Extract key action verbs and nouns
            var keywords = objective.ToLower().Split(' ');
            
            foreach (var word in keywords)
            {
                if (word.Contains("complet") || word.Contains("finish") || word.Contains("achieve"))
                {
                    metrics.Add("task_completion_rate");
                }
                if (word.Contains("fast") || word.Contains("effici") || word.Contains("quick"))
                {
                    metrics.Add("execution_speed");
                }
                if (word.Contains("safe") || word.Contains("secur") || word.Contains("protect"))
                {
                    metrics.Add("safety_compliance");
                }
                if (word.Contains("learn") || word.Contains("improv") || word.Contains("adapt"))
                {
                    metrics.Add("learning_rate");
                }
            }
            
            if (metrics.Count == 0)
            {
                metrics.Add("general_progress");
            }
            
            return (objective, metrics);
        }
        
        private IEnumerable<string> GetDefaultSafetyConstraints()
        {
            return new[]
            {
                "CONSTRAINT_NO_UNAUTHORIZED_ACCESS",
                "CONSTRAINT_PRESERVE_SYSTEM_INTEGRITY",
                "CONSTRAINT_RESPECT_USER_BOUNDARIES",
                "CONSTRAINT_LOG_ALL_ACTIONS",
                "CONSTRAINT_ALLOW_SHUTDOWN"
            };
        }
        
        private string GenerateGoalId()
        {
            return $"GOAL_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
        }
        
        private void EmitThought(string thought)
        {
            ConsciousnessEvent?.Invoke(this, thought);
        }
    }
}
