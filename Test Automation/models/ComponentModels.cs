using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Test_Automation.Models
{
    /// <summary>
    /// Base model for component input/output
    /// </summary>
    public class ComponentData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("componentName")]
        public string ComponentName { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Http component model
    /// </summary>
    public class HttpData : ComponentData
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = "GET";

        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("body")]
        public string Body { get; set; }

        [JsonPropertyName("responseStatus")]
        public int? ResponseStatus { get; set; }

        [JsonPropertyName("responseBody")]
        public string ResponseBody { get; set; }
    }

    /// <summary>
    /// SQL component model
    /// </summary>
    public class SqlData : ComponentData
    {
        [JsonPropertyName("connectionString")]
        public string ConnectionString { get; set; }

        [JsonPropertyName("query")]
        public string Query { get; set; }

        [JsonPropertyName("queryResult")]
        public List<Dictionary<string, object>> QueryResult { get; set; } = new List<Dictionary<string, object>>();
    }

    /// <summary>
    /// Dataset component model
    /// </summary>
    public class DatasetData : ComponentData
    {
        [JsonPropertyName("dataSource")]
        public string DataSource { get; set; }

        [JsonPropertyName("rows")]
        public List<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>();

        [JsonPropertyName("currentRow")]
        public int CurrentRow { get; set; } = 0;
    }

    /// <summary>
    /// Assert component model
    /// </summary>
    public class AssertData : ComponentData
    {
        [JsonPropertyName("expectedValue")]
        public object ExpectedValue { get; set; }

        [JsonPropertyName("actualValue")]
        public object ActualValue { get; set; }

        [JsonPropertyName("operator")]
        public string Operator { get; set; } = "equals";

        [JsonPropertyName("passed")]
        public bool Passed { get; set; }

        [JsonPropertyName("errorMessage")]
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Variable Extractor component model
    /// </summary>
    public class VariableExtractorData : ComponentData
    {
        [JsonPropertyName("source")]
        public string Source { get; set; }

        [JsonPropertyName("pattern")]
        public string Pattern { get; set; }

        [JsonPropertyName("variableName")]
        public string VariableName { get; set; }

        [JsonPropertyName("extractedValue")]
        public string ExtractedValue { get; set; }
    }

    /// <summary>
    /// Timer component model
    /// </summary>
    public class TimerData : ComponentData
    {
        [JsonPropertyName("delayMs")]
        public int DelayMs { get; set; }

        [JsonPropertyName("executed")]
        public bool Executed { get; set; }
    }

    /// <summary>
    /// Script component model
    /// </summary>
    public class ScriptData : ComponentData
    {
        [JsonPropertyName("scriptCode")]
        public string ScriptCode { get; set; }

        [JsonPropertyName("scriptLanguage")]
        public string ScriptLanguage { get; set; } = "csharp";

        [JsonPropertyName("executionResult")]
        public string ExecutionResult { get; set; }
    }

    /// <summary>
    /// Loop component model
    /// </summary>
    public class LoopData : ComponentData
    {
        [JsonPropertyName("iterations")]
        public int Iterations { get; set; }

        [JsonPropertyName("currentIteration")]
        public int CurrentIteration { get; set; } = 0;

        [JsonPropertyName("childComponents")]
        public List<string> ChildComponents { get; set; } = new List<string>();
    }

    /// <summary>
    /// Foreach component model
    /// </summary>
    public class ForeachData : ComponentData
    {
        [JsonPropertyName("collection")]
        public List<object> Collection { get; set; } = new List<object>();

        [JsonPropertyName("currentIndex")]
        public int CurrentIndex { get; set; } = 0;

        [JsonPropertyName("childComponents")]
        public List<string> ChildComponents { get; set; } = new List<string>();
    }

    /// <summary>
    /// If component model
    /// </summary>
    public class IfData : ComponentData
    {
        [JsonPropertyName("condition")]
        public string Condition { get; set; }

        [JsonPropertyName("conditionMet")]
        public bool ConditionMet { get; set; }

        [JsonPropertyName("trueComponents")]
        public List<string> TrueComponents { get; set; } = new List<string>();

        [JsonPropertyName("falseComponents")]
        public List<string> FalseComponents { get; set; } = new List<string>();
    }

    /// <summary>
    /// Config component model
    /// </summary>
    public class ConfigData : ComponentData
    {
        [JsonPropertyName("configurations")]
        public Dictionary<string, object> Configurations { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// TestPlan component model
    /// </summary>
    public class TestPlanData : ComponentData
    {
        [JsonPropertyName("testPlanName")]
        public string TestPlanName { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("components")]
        public List<string> Components { get; set; } = new List<string>();

        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public DateTime EndTime { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "pending";
    }

    /// <summary>
    /// Threads component model
    /// </summary>
    public class ThreadsData : ComponentData
    {
        [JsonPropertyName("threadCount")]
        public int ThreadCount { get; set; } = 1;

        [JsonPropertyName("rampUpTime")]
        public int RampUpTime { get; set; } = 0;

        [JsonPropertyName("childComponents")]
        public List<string> ChildComponents { get; set; } = new List<string>();
    }
}
