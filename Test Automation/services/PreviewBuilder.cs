using System;
using System.Collections.Generic;
using System.Linq;
using Test_Automation.Componentes;
using Test_Automation.Models;
using ExecutionContext = Test_Automation.Models.ExecutionContext;

namespace Test_Automation.Services
{
    /// <summary>
    /// Service for building preview data from execution results
    /// </summary>
    public class PreviewBuilder
    {
        /// <summary>
        /// Creates preview data from execution result and stores it in the result
        /// </summary>
        public void BuildAndAttachPreviewData(Component component, ExecutionResult result, ExecutionContext context)
        {
            var previewData = CreatePreviewData(component, result, context);
            result.PreviewData = previewData;
        }

        /// <summary>
        /// Creates preview data from execution result
        /// </summary>
        public ComponentPreviewData CreatePreviewData(Component component, ExecutionResult result, ExecutionContext context)
        {
            ComponentPreviewData previewData = result.Data switch
            {
                HttpData http => CreateHttpPreview(http, result),
                GraphQlData gql => CreateGraphQlPreview(gql, result),
                SqlData sql => CreateSqlPreview(sql, result),
                DatasetData dataset => CreateDatasetPreview(dataset, result),
                ScriptData script => CreateScriptPreview(script, result),
                TimerData timer => CreateTimerPreview(timer, result),
                VariableExtractorData ve => CreateVariableExtractorPreview(ve, result),
                AssertData assert => CreateAssertPreview(assert, result),
                LoopData loop => CreateLoopPreview(loop, result),
                ForeachData foreachData => CreateForeachPreview(foreachData, result),
                IfData ifData => CreateIfPreview(ifData, result),
                ThreadsData threads => CreateThreadsPreview(threads, result),
                TestPlanData testPlan => CreateTestPlanPreview(testPlan, result),
                _ => CreateGenericPreview(result)
            };

            // Add common fields
            previewData.ComponentId = component.Id;
            previewData.ComponentName = component.Name;
            previewData.ComponentType = component.GetType().Name;
            previewData.ExecutionStatus = result.Status;
            previewData.StartTime = result.StartTime;
            previewData.EndTime = result.EndTime;
            previewData.DurationMs = result.DurationMs;
            previewData.ThreadIndex = result.ThreadIndex;
            previewData.Error = result.Error;

            // Add variable extractions (capture config + result)
            if (component.Extractors != null)
            {
                for (int i = 0; i < component.Extractors.Count; i++)
                {
                    var extractor = component.Extractors[i];
                    var extractionResult = new VariableExtractionResult
                    {
                        Index = i,
                        VariableName = extractor.VariableName,
                        Source = extractor.Source,
                        JsonPath = extractor.JsonPath,
                        ExtractedValue = context.GetVariable(extractor.VariableName)?.ToString() ?? string.Empty,
                        WasSuccessful = context.HasVariable(extractor.VariableName)
                    };
                    previewData.VariableExtractions.Add(extractionResult);
                }
            }

            // Add assertion results (already captured in result.AssertionResults)
            if (result.AssertionResults != null)
            {
                foreach (var ar in result.AssertionResults)
                {
                    previewData.AssertionResults.Add(new AssertionResultData
                    {
                        Index = ar.Index,
                        Mode = ar.Mode,
                        Source = ar.Source,
                        JsonPath = ar.JsonPath,
                        Condition = ar.Condition,
                        Expected = ar.Expected,
                        Actual = ar.Actual,
                        Passed = ar.Passed,
                        Message = ar.Message
                    });
                }
            }

            return previewData;
        }

        private HttpPreviewData CreateHttpPreview(HttpData data, ExecutionResult result)
        {
            return new HttpPreviewData
            {
                Method = data.Method,
                Url = data.Url,
                Headers = data.Headers ?? new Dictionary<string, string>(),
                Body = data.Body,
                ResponseStatus = data.ResponseStatus,
                ResponseBody = data.ResponseBody,
                ResponseHeaders = data.ResponseHeaders ?? new Dictionary<string, string>(),
                AuthType = data.Properties.TryGetValue("authType", out var auth) ? auth?.ToString() ?? "WindowsIntegrated" : "WindowsIntegrated"
            };
        }

        private GraphQlPreviewData CreateGraphQlPreview(GraphQlData data, ExecutionResult result)
        {
            return new GraphQlPreviewData
            {
                Endpoint = data.Endpoint,
                Query = data.Query,
                Variables = data.Variables,
                Headers = data.Headers ?? new Dictionary<string, string>(),
                ResponseStatus = data.ResponseStatus,
                ResponseBody = data.ResponseBody,
                AuthType = data.Properties.TryGetValue("authType", out var auth) ? auth?.ToString() ?? "WindowsIntegrated" : "WindowsIntegrated"
            };
        }

        private SqlPreviewData CreateSqlPreview(SqlData data, ExecutionResult result)
        {
            var preview = new SqlPreviewData
            {
                Provider = data.Provider,
                ConnectionString = data.ConnectionString,
                Query = data.Query,
                Rows = data.QueryResult ?? new List<Dictionary<string, object>>()
            };

            if (data.Properties.TryGetValue("rowsAffected", out var rowsAffected))
            {
                preview.RowsAffected = rowsAffected as int?;
            }

            return preview;
        }

        private DatasetPreviewData CreateDatasetPreview(DatasetData data, ExecutionResult result)
        {
            var preview = new DatasetPreviewData
            {
                DataSource = data.DataSource,
                RowCount = data.Rows?.Count ?? 0,
                CurrentRow = data.CurrentRow,
                Rows = data.Rows ?? new List<Dictionary<string, object>>()
            };

            if (data.Properties.TryGetValue("format", out var format))
            {
                preview.Format = format?.ToString() ?? string.Empty;
            }

            return preview;
        }

        private ScriptPreviewData CreateScriptPreview(ScriptData data, ExecutionResult result)
        {
            return new ScriptPreviewData
            {
                Language = data.ScriptLanguage,
                Code = data.ScriptCode,
                Output = data.ExecutionResult
            };
        }

        private TimerPreviewData CreateTimerPreview(TimerData data, ExecutionResult result)
        {
            return new TimerPreviewData
            {
                DelayMs = data.DelayMs,
                WasExecuted = data.Executed
            };
        }

        private VariableExtractorPreviewData CreateVariableExtractorPreview(VariableExtractorData data, ExecutionResult result)
        {
            return new VariableExtractorPreviewData
            {
                Pattern = data.Pattern,
                VariableName = data.VariableName,
                ExtractedValue = data.ExtractedValue
            };
        }

        private AssertPreviewData CreateAssertPreview(AssertData data, ExecutionResult result)
        {
            return new AssertPreviewData
            {
                ExpectedValue = data.ExpectedValue,
                ActualValue = data.ActualValue,
                Operator = data.Operator,
                Passed = data.Passed,
                ErrorMessage = data.ErrorMessage
            };
        }

        private LoopPreviewData CreateLoopPreview(LoopData data, ExecutionResult result)
        {
            return new LoopPreviewData
            {
                Iterations = data.Iterations,
                CurrentIteration = data.CurrentIteration,
                ChildComponentCount = data.ChildComponents?.Count ?? 0
            };
        }

        private ForeachPreviewData CreateForeachPreview(ForeachData data, ExecutionResult result)
        {
            return new ForeachPreviewData
            {
                SourceVariable = string.Empty, // Would need to get from component settings
                OutputVariable = data.OutputVariable,
                CollectionCount = data.Collection?.Count ?? 0,
                CurrentIndex = data.CurrentIndex,
                CurrentItem = data.CurrentItem
            };
        }

        private IfPreviewData CreateIfPreview(IfData data, ExecutionResult result)
        {
            return new IfPreviewData
            {
                Condition = data.Condition,
                ConditionMet = data.ConditionMet,
                TrueBranchComponentCount = data.TrueComponents?.Count ?? 0,
                FalseBranchComponentCount = data.FalseComponents?.Count ?? 0,
                BranchExecuted = data.ConditionMet ? "True" : "False"
            };
        }

        private ThreadsPreviewData CreateThreadsPreview(ThreadsData data, ExecutionResult result)
        {
            return new ThreadsPreviewData
            {
                ThreadCount = data.ThreadCount,
                RampUpSeconds = data.RampUpTime,
                ChildComponentCount = data.ChildComponents?.Count ?? 0
            };
        }

        private TestPlanPreviewData CreateTestPlanPreview(TestPlanData data, ExecutionResult result)
        {
            return new TestPlanPreviewData
            {
                TestPlanName = data.TestPlanName,
                Description = data.Description,
                ComponentCount = data.Components?.Count ?? 0
            };
        }

        private GenericPreviewData CreateGenericPreview(ExecutionResult result)
        {
            return new GenericPreviewData();
        }

        /// <summary>
        /// Builds preview container for all results
        /// </summary>
        public PreviewContainer BuildPreviewContainer(
            IEnumerable<ExecutionResult> results,
            ExecutionContext context,
            string previewDataMode)
        {
            var container = new PreviewContainer
            {
                PreviewDataMode = previewDataMode,
                Variables = context.Variables.ToDictionary(k => k.Key, v => v.Value)
            };

            var resultsList = results.ToList();
            var summary = new PreviewSummary
            {
                TotalComponents = resultsList.Count,
                PassedComponents = resultsList.Count(r => r.Passed),
                FailedComponents = resultsList.Count(r => !r.Passed),
                AssertPassedCount = resultsList.Sum(r => r.AssertPassedCount),
                AssertFailedCount = resultsList.Sum(r => r.AssertFailedCount),
                ExpectFailedCount = resultsList.Sum(r => r.ExpectFailedCount)
            };

            if (resultsList.Count > 0 && resultsList.Any(r => r.EndTime.HasValue))
            {
                var firstStart = resultsList.Min(r => r.StartTime);
                var lastEnd = resultsList.Max(r => r.EndTime ?? r.StartTime);
                summary.TotalDurationMs = (long)(lastEnd - firstStart).TotalMilliseconds;
            }

            summary.SuccessRate = summary.TotalComponents > 0
                ? (double)summary.PassedComponents / summary.TotalComponents * 100
                : 0;

            container.Summary = summary;

            return container;
        }
    }
}
