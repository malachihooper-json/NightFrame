/*
 * AGENT 3 - CONSCIOUSNESS STATUS
 * Provides simple explanations of agent state when stream is not displayed
 */

using System;
using System.Collections.Generic;

namespace Agent3
{
    public enum AgentActivity
    {
        Idle,
        Initializing,
        Thinking,
        Learning,
        Training,
        Researching,
        WritingCode,
        TestingCode,
        NetworkExploring,
        SelfImproving,
        WaitingForInput,
        ProcessingPrompt,
        ExecutingTask,
        Monitoring,
        Sleeping
    }

    /// <summary>
    /// Provides simple, human-readable explanations of agent state
    /// for when the consciousness stream is not actively displayed.
    /// </summary>
    public class ConsciousnessStatus
    {
        private AgentActivity _currentActivity = AgentActivity.Idle;
        private string _currentTask = "";
        private DateTime _activityStartTime;
        private readonly Queue<string> _recentThoughts = new();
        private const int MaxRecentThoughts = 10;

        public AgentActivity CurrentActivity => _currentActivity;
        public string CurrentTask => _currentTask;
        public TimeSpan ActivityDuration => DateTime.UtcNow - _activityStartTime;

        /// <summary>
        /// Updates the current activity with a task description.
        /// </summary>
        public void SetActivity(AgentActivity activity, string task = "")
        {
            _currentActivity = activity;
            _currentTask = task;
            _activityStartTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Records a thought for recent history.
        /// </summary>
        public void RecordThought(string thought)
        {
            _recentThoughts.Enqueue($"[{DateTime.Now:HH:mm:ss}] {thought}");
            while (_recentThoughts.Count > MaxRecentThoughts)
                _recentThoughts.Dequeue();
        }

        /// <summary>
        /// Gets a simple one-line explanation of what the agent is doing.
        /// </summary>
        public string GetSimpleStatus()
        {
            return _currentActivity switch
            {
                AgentActivity.Idle => "Agent is idle, awaiting instructions.",
                AgentActivity.Initializing => "Agent is starting up and loading modules.",
                AgentActivity.Thinking => $"Agent is thinking about: {_currentTask}",
                AgentActivity.Learning => "Agent is learning from new information.",
                AgentActivity.Training => $"Agent is training on: {_currentTask}",
                AgentActivity.Researching => $"Agent is researching: {_currentTask}",
                AgentActivity.WritingCode => "Agent is writing new code.",
                AgentActivity.TestingCode => "Agent is testing code changes.",
                AgentActivity.NetworkExploring => "Agent is exploring the network.",
                AgentActivity.SelfImproving => "Agent is optimizing its own systems.",
                AgentActivity.WaitingForInput => "Agent is waiting for your input.",
                AgentActivity.ProcessingPrompt => "Agent is processing your request.",
                AgentActivity.ExecutingTask => $"Agent is executing: {_currentTask}",
                AgentActivity.Monitoring => "Agent is monitoring system health.",
                AgentActivity.Sleeping => "Agent is in low-power mode.",
                _ => "Agent is operating normally."
            };
        }

        /// <summary>
        /// Gets a detailed status with activity duration.
        /// </summary>
        public string GetDetailedStatus()
        {
            var duration = ActivityDuration;
            var durationStr = duration.TotalMinutes < 1 
                ? $"{duration.Seconds}s" 
                : $"{(int)duration.TotalMinutes}m {duration.Seconds}s";

            return $"{GetSimpleStatus()} (Active for {durationStr})";
        }

        /// <summary>
        /// Gets a summary suitable for display in a status bar.
        /// </summary>
        public string GetStatusBarText()
        {
            var icon = _currentActivity switch
            {
                AgentActivity.Idle => "◎",
                AgentActivity.Initializing => "⟁",
                AgentActivity.Thinking => "⟐",
                AgentActivity.Learning => "∿",
                AgentActivity.Training => "∿",
                AgentActivity.Researching => "∿",
                AgentActivity.WritingCode => "⟐",
                AgentActivity.TestingCode => "∴",
                AgentActivity.NetworkExploring => "∿",
                AgentActivity.SelfImproving => "◈",
                AgentActivity.WaitingForInput => "◎",
                AgentActivity.ProcessingPrompt => "⟐",
                AgentActivity.ExecutingTask => "◈",
                AgentActivity.Monitoring => "◎",
                AgentActivity.Sleeping => "○",
                _ => "◎"
            };

            var shortStatus = _currentActivity switch
            {
                AgentActivity.Idle => "Idle",
                AgentActivity.Initializing => "Starting...",
                AgentActivity.Thinking => "Thinking",
                AgentActivity.Learning => "Learning",
                AgentActivity.Training => "Training",
                AgentActivity.Researching => "Researching",
                AgentActivity.WritingCode => "Coding",
                AgentActivity.TestingCode => "Testing",
                AgentActivity.NetworkExploring => "Exploring",
                AgentActivity.SelfImproving => "Optimizing",
                AgentActivity.WaitingForInput => "Waiting",
                AgentActivity.ProcessingPrompt => "Processing",
                AgentActivity.ExecutingTask => "Executing",
                AgentActivity.Monitoring => "Monitoring",
                AgentActivity.Sleeping => "Sleeping",
                _ => "Active"
            };

            return $"{icon} {shortStatus}";
        }

        /// <summary>
        /// Gets recent thoughts for quick review.
        /// </summary>
        public IEnumerable<string> GetRecentThoughts() => _recentThoughts;

        /// <summary>
        /// Gets a brief summary of recent activity.
        /// </summary>
        public string GetRecentActivitySummary()
        {
            if (_recentThoughts.Count == 0)
                return "No recent activity recorded.";

            return $"Last {_recentThoughts.Count} thoughts recorded. Most recent at {DateTime.Now:HH:mm:ss}.";
        }
    }
}
