using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Test_Automation.Models
{
    /// <summary>
    /// Result of a variable extraction - captures both configuration and result
    /// </summary>
    public class VariableExtractionResult
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("variableName")]
        public string VariableName { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("jsonPath")]
        public string JsonPath { get; set; } = string.Empty;

        [JsonPropertyName("extractedValue")]
        public string ExtractedValue { get; set; } = string.Empty;

        [JsonPropertyName("wasSuccessful")]
        public bool WasSuccessful { get; set; }

        [JsonPropertyName("errorMessage")]
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Assertion result with full configuration - captures both assertion config and result
    /// </summary>
    public class AssertionResultData
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
        public string Condition { get; set; } = "Equals";

        [JsonPropertyName("expected")]
        public string Expected { get; set; } = string.Empty;

        [JsonPropertyName("actual")]
        public string Actual { get; set; } = string.Empty;

        [JsonPropertyName("passed")]
        public bool Passed { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Base class for all component preview data
    /// </summary>
    public abstract class ComponentPreviewData
    {
        [JsonPropertyName("componentId")]
        public string ComponentId { get; set; } = string.Empty;

        [JsonPropertyName("componentName")]
        public string ComponentName { get; set; } = string.Empty;

        [JsonPropertyName("componentType")]
        public string ComponentType { get; set; } = string.Empty;

        [JsonPropertyName("executionStatus")]
        public string ExecutionStatus { get; set; } = "pending";

        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public DateTime? EndTime { get; set; }

        [JsonPropertyName("durationMs")]
        public long DurationMs { get; set; }

        [JsonPropertyName("threadIndex")]
        public int ThreadIndex { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;

        [JsonPropertyName("variableExtractions")]
        public List<VariableExtractionResult> VariableExtractions { get; set; } = new List<VariableExtractionResult>();

        [JsonPropertyName("assertionResults")]
        public List<AssertionResultData> AssertionResults { get; set; } = new List<AssertionResultData>();

        [JsonPropertyName("childResults")]
        public List<ComponentPreviewData> ChildResults { get; set; } = new List<ComponentPreviewData>();

        [JsonPropertyName("previewType")]
        public abstract string PreviewType { get; }
    }

    /// <summary>
    /// HTTP component preview data
    /// </summary>
    public class HttpPreviewData : ComponentPreviewData
    {
        public override string PreviewType => "Http";

        [JsonPropertyName("method")]
        public string Method { get; set; } = "GET";

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("responseStatus")]
        public int? ResponseStatus { get; set; }

        [JsonPropertyName("responseBody")]
        public string ResponseBody { get; set; } = string.Empty;

        [JsonPropertyName("responseHeaders")]
        public Dictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("authType")]
        public string AuthType { get; set; } = "WindowsIntegrated";
    }

    /// <summary>
    /// GraphQL component preview data
    /// </summary>
    public class GraphQlPreviewData : ComponentPreviewData
    {
        public override string PreviewType => "GraphQl";

        [JsonPropertyName("endpoint")]
        public string Endpoint { get; set; } = string.Empty;

        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        [JsonPropertyName("variables")]
        public string Variables { get; set; } = "{}";

        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("responseStatus")]
        public int? ResponseStatus { get; set; }

        [JsonPropertyName("responseBody")]
        public string ResponseBody { get; set; } = string.Empty;

        [JsonPropertyName("authType")]
        public string AuthType { get; set; } = "WindowsIntegrated";
    }

    /// <summary>
    /// SQL component preview data
    /// </summary>
    public class SqlPreviewData : ComponentPreviewData
    {
        public override string PreviewType => "Sql";

        [JsonPropertyName("provider")]
        public string Provider { get; set; } = "SqlServer";

        [JsonPropertyName("connectionString")]
        public string ConnectionString { get; set; } = string.Empty;

        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        [JsonPropertyName("rows")]
        public List<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>();

        [JsonPropertyName("rowsAffected")]
        public int? RowsAffected { get; set; }
    }

    /// <summary>
    /// Dataset component preview data
    /// </summary>
    public class DatasetPreviewData : ComponentPreviewData
    {
        public override string PreviewType => "Dataset";

        [JsonPropertyName("dataSource")]
        public string DataSource { get; set; } = string.Empty;

        [JsonPropertyName("format")]
        public string Format { get; set; } = string.Empty;

        [JsonPropertyName("rowCount")]
        public int RowCount { get; set; }

        [JsonPropertyName("currentRow")]
        public int CurrentRow { get; set; }

        [JsonPropertyName("rows")]
        public List<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>();
    }

    /// <summary>
    /// Script component preview data
    /// </summary>
    public class ScriptPreviewData : ComponentPreviewData
    {
        public override string PreviewType => "Script";

        [JsonPropertyName("language")]
        public string Language { get; set; } = "CSharp";

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("output")]
        public string Output { get; set; } = string.Empty;
    }

    /// <summary>
    /// Timer component preview data
    /// </summary>
    public class TimerPreviewData : ComponentPreviewData
    {
        public override string PreviewType => "Timer";

        [JsonPropertyName("delayMs")]
        public int DelayMs { get; set; }

        [JsonPropertyName("wasExecuted")]
        public bool WasExecuted { get; set; }
    }

    /// <summary>
    /// Variable Extractor component preview data
    /// </summary>
    public class VariableExtractorPreviewData : ComponentPreviewData
    {
        public override string PreviewType => "VariableExtractor";

        [JsonPropertyName("pattern")]
        public string Pattern { get; set; } = string.Empty;

        [JsonPropertyName("variableName")]
        public string VariableName { get; set; } = string.Empty;

        [JsonPropertyName("extractedValue")]
        public string ExtractedValue { get; set; } = string.Empty;
    }

    /// <summary>
    /// Assert component preview data
    /// </summary>
    public class AssertPreviewData : ComponentPreviewData
    {
        public override string PreviewType => "Assert";

        [JsonPropertyName("expectedValue")]
        public object? ExpectedValue { get; set; }

        [JsonPropertyName("actualValue")]
        public object? ActualValue { get; set; }

        [JsonPropertyName("operator")]
        public string Operator { get; set; } = "Equals";

        [JsonPropertyName("passed")]
        public bool Passed { get; set; }

        [JsonPropertyName("errorMessage")]
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Loop component preview data
    /// </summary>
    public class LoopPreviewData : ComponentPreviewData
    {
        public override string PreviewType => "Loop";

        [JsonPropertyName("iterations")]
        public int Iterations { get; set; }

        [JsonPropertyName("currentIteration")]
        public int CurrentIteration { get; set; }

        [JsonPropertyName("childComponentCount")]
        public int ChildComponentCount { get; set; }
    }

    /// <summary>
    /// Foreach component preview data
    /// </summary>
    public class ForeachPreviewData : ComponentPreviewData
    {
        public override string PreviewType => "Foreach";

        [JsonPropertyName("sourceVariable")]
        public string SourceVariable { get; set; } = string.Empty;

        [JsonPropertyName("outputVariable")]
        public string OutputVariable { get; set; } = string.Empty;

        [JsonPropertyName("collectionCount")]
        public int CollectionCount { get; set; }

        [JsonPropertyName("currentIndex")]
        public int CurrentIndex { get; set; }

        [JsonPropertyName("currentItem")]
        public object? CurrentItem { get; set; }
    }

    /// <summary>
    /// If component preview data
    /// </summary>
    public class IfPreviewData : ComponentPreviewData
    {
        public override string PreviewType => "If";

        [JsonPropertyName("condition")]
        public string Condition { get; set; } = string.Empty;

        [JsonPropertyName("conditionMet")]
        public bool ConditionMet { get; set; }

        [JsonPropertyName("trueBranchComponentCount")]
        public int TrueBranchComponentCount { get; set; }

        [JsonPropertyName("falseBranchComponentCount")]
        public int FalseBranchComponentCount { get; set; }

        [JsonPropertyName("branchExecuted")]
        public string BranchExecuted { get; set; } = string.Empty;
    }

    /// <summary>
    /// Threads component preview data
    /// </summary>
    public class ThreadsPreviewData : ComponentPreviewData
    {
        public override string PreviewType => "Threads";

        [JsonPropertyName("threadCount")]
        public int ThreadCount { get; set; }

        [JsonPropertyName("rampUpSeconds")]
        public int RampUpSeconds { get; set; }

        [JsonPropertyName("childComponentCount")]
        public int ChildComponentCount { get; set; }
    }

    /// <summary>
    /// TestPlan component preview data
    /// </summary>
    public class TestPlanPreviewData : ComponentPreviewData
    {
        public override string PreviewType => "TestPlan";

        [JsonPropertyName("testPlanName")]
        public string TestPlanName { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("componentCount")]
        public int ComponentCount { get; set; }

        [JsonPropertyName("totalComponents")]
        public int TotalComponents { get; set; }

        [JsonPropertyName("passedComponents")]
        public int PassedComponents { get; set; }

        [JsonPropertyName("failedComponents")]
        public int FailedComponents { get; set; }

        [JsonPropertyName("successRate")]
        public double SuccessRate { get; set; }
    }

    /// <summary>
    /// Project component preview data
    /// </summary>
    public class ProjectPreviewData : ComponentPreviewData
    {
        public override string PreviewType => "Project";

        [JsonPropertyName("projectName")]
        public string ProjectName { get; set; } = string.Empty;

        [JsonPropertyName("testPlanCount")]
        public int TestPlanCount { get; set; }

        [JsonPropertyName("totalComponents")]
        public int TotalComponents { get; set; }

        [JsonPropertyName("passedComponents")]
        public int PassedComponents { get; set; }

        [JsonPropertyName("failedComponents")]
        public int FailedComponents { get; set; }

        [JsonPropertyName("successRate")]
        public double SuccessRate { get; set; }
    }

    /// <summary>
    /// Generic/fallback preview data for unknown component types
    /// </summary>
    public class GenericPreviewData : ComponentPreviewData
    {
        public override string PreviewType => "Generic";

        [JsonPropertyName("customData")]
        public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Container for all preview data - used for JSON serialization
    /// </summary>
    public class PreviewContainer
    {
        [JsonPropertyName("previewDataMode")]
        public string PreviewDataMode { get; set; } = "Last Run";

        [JsonPropertyName("previewData")]
        public List<ComponentPreviewData> PreviewData { get; set; } = new List<ComponentPreviewData>();

        [JsonPropertyName("variables")]
        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();

        [JsonPropertyName("summary")]
        public PreviewSummary Summary { get; set; } = new PreviewSummary();
    }

    /// <summary>
    /// Summary of execution results
    /// </summary>
    public class PreviewSummary
    {
        [JsonPropertyName("totalComponents")]
        public int TotalComponents { get; set; }

        [JsonPropertyName("passedComponents")]
        public int PassedComponents { get; set; }

        [JsonPropertyName("failedComponents")]
        public int FailedComponents { get; set; }

        [JsonPropertyName("assertPassedCount")]
        public int AssertPassedCount { get; set; }

        [JsonPropertyName("assertFailedCount")]
        public int AssertFailedCount { get; set; }

        [JsonPropertyName("expectFailedCount")]
        public int ExpectFailedCount { get; set; }

        [JsonPropertyName("totalDurationMs")]
        public long TotalDurationMs { get; set; }

        [JsonPropertyName("successRate")]
        public double SuccessRate { get; set; }
    }
}
