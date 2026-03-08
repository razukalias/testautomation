using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Test_Automation.Models
{
    /// <summary>
    /// Execution context for a test plan run
    /// </summary>
    public class ExecutionContext
    {
        [JsonPropertyName("executionId")]
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("variables")]
        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();

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

        public void SetVariable(string key, object value)
        {
            Variables[key] = value;
        }

        public object GetVariable(string key)
        {
            return Variables.ContainsKey(key) ? Variables[key] : null;
        }

        public bool HasVariable(string key)
        {
            return Variables.ContainsKey(key);
        }
    }

    /// <summary>
    /// Execution result for individual component
    /// </summary>
    public class ExecutionResult
    {
        [JsonPropertyName("componentId")]
        public string ComponentId { get; set; }

        [JsonPropertyName("componentName")]
        public string ComponentName { get; set; }

        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("endTime")]
        public DateTime? EndTime { get; set; }

        [JsonPropertyName("duration")]
        public long DurationMs { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "pending"; // pending, running, passed, failed

        [JsonPropertyName("output")]
        public string Output { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }

        [JsonPropertyName("data")]
        public object Data { get; set; }

        [JsonPropertyName("passed")]
        public bool Passed { get; set; }

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
        public string ExecutionId { get; set; }

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
