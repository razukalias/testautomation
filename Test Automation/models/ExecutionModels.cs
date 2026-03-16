using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Threading;

namespace Test_Automation.Models
{
    /// <summary>
    /// Execution context for a test plan run
    /// </summary>
    public class ExecutionContext
    {
        private ConcurrentDictionary<string, object> _variables = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource _stopSource = new CancellationTokenSource();

        [JsonPropertyName("executionId")]
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("variables")]
        public ConcurrentDictionary<string, object> Variables
        {
            get => _variables;
            set => _variables = value != null
                ? new ConcurrentDictionary<string, object>(value, StringComparer.OrdinalIgnoreCase)
                : new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("endTime")]
        public DateTime? EndTime { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "running";

        [JsonPropertyName("results")]
        public List<ExecutionResult> Results { get; set; } = new List<ExecutionResult>();

        [JsonPropertyName("isRunning")]
        public bool IsRunning { get; set; } = true;

        [JsonIgnore]
        public CancellationToken StopToken => _stopSource.Token;

        public void RequestStop()
        {
            IsRunning = false;
            Status = "stopping";
            if (!_stopSource.IsCancellationRequested)
            {
                _stopSource.Cancel();
            }
        }

        public void ResetStopRequest()
        {
            if (_stopSource.IsCancellationRequested)
            {
                _stopSource.Dispose();
                _stopSource = new CancellationTokenSource();
            }

            IsRunning = true;
            if (string.Equals(Status, "stopping", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Status, "stopped", StringComparison.OrdinalIgnoreCase))
            {
                Status = "running";
            }
        }

        public void SetVariable(string key, object value)
        {
            var normalizedKey = key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return;
            }

            Variables[normalizedKey] = value;
        }

        public object? GetVariable(string key)
        {
            var normalizedKey = key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return null;
            }

            return Variables.TryGetValue(normalizedKey, out var value) ? value : null;
        }

        public bool HasVariable(string key)
        {
            var normalizedKey = key?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(normalizedKey) && Variables.ContainsKey(normalizedKey);
        }
    }

    /// <summary>
    /// Execution result for individual component
    /// </summary>
    public class AssertionEvaluationResult
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "Assert";

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("jsonPath")]
        public string JsonPath { get; set; } = string.Empty;

        [JsonPropertyName("condition")]
        public string Condition { get; set; } = string.Empty;

        [JsonPropertyName("expected")]
        public string Expected { get; set; } = string.Empty;

        [JsonPropertyName("actual")]
        public string Actual { get; set; } = string.Empty;

        [JsonPropertyName("passed")]
        public bool Passed { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    public class ExecutionResult
    {
        [JsonPropertyName("componentId")]
        public string ComponentId { get; set; } = string.Empty;

        [JsonPropertyName("componentName")]
        public string ComponentName { get; set; } = string.Empty;

        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("endTime")]
        public DateTime? EndTime { get; set; }

        [JsonPropertyName("duration")]
        public long DurationMs { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "pending"; // pending, running, passed, failed

        [JsonPropertyName("output")]
        public string Output { get; set; } = string.Empty;

        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public object? Data { get; set; }

        [JsonPropertyName("passed")]
        public bool Passed { get; set; }

        [JsonPropertyName("threadIndex")]
        public int ThreadIndex { get; set; }

        [JsonPropertyName("threadGroupId")]
        public string ThreadGroupId { get; set; } = string.Empty;

        [JsonPropertyName("assertions")]
        public List<AssertionEvaluationResult> AssertionResults { get; set; } = new List<AssertionEvaluationResult>();

        [JsonPropertyName("assertFailedCount")]
        public int AssertFailedCount { get; set; }

        [JsonPropertyName("expectFailedCount")]
        public int ExpectFailedCount { get; set; }

        [JsonPropertyName("assertPassedCount")]
        public int AssertPassedCount { get; set; }

        public void MarkAsCompleted(bool success = true)
        {
            EndTime = DateTime.UtcNow;
            DurationMs = (long)(EndTime.Value - StartTime).TotalMilliseconds;
            Status = success ? "passed" : "failed";
            Passed = success;
        }
    }

    /// <summary>
    /// Test execution summary
    /// </summary>
    public class ExecutionSummary
    {
        [JsonPropertyName("executionId")]
        public string ExecutionId { get; set; } = string.Empty;

        [JsonPropertyName("totalComponents")]
        public int TotalComponents { get; set; }

        [JsonPropertyName("passedComponents")]
        public int PassedComponents { get; set; }

        [JsonPropertyName("failedComponents")]
        public int FailedComponents { get; set; }

        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public DateTime EndTime { get; set; }

        [JsonPropertyName("totalDurationMs")]
        public long TotalDurationMs { get; set; }

        [JsonPropertyName("successRate")]
        public double SuccessRate => TotalComponents > 0 ? (double)PassedComponents / TotalComponents * 100 : 0;

        [JsonPropertyName("status")]
        public string Status => FailedComponents == 0 ? "passed" : "failed";
    }
}
