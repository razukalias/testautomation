using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Test_Automation.Componentes;
using Test_Automation.Models;
using Test_Automation.Models.Editor;

namespace Test_Automation.Services
{
    /// <summary>
    /// Service for executing components and managing the execution flow
    /// </summary>
    public class ComponentExecutor
    {
        private static readonly System.Threading.AsyncLocal<int?> CurrentThreadIndex = new System.Threading.AsyncLocal<int?>();
        private static readonly System.Threading.AsyncLocal<string?> CurrentThreadGroupId = new System.Threading.AsyncLocal<string?>();

        public event Action<ExecutionResult>? ComponentStarted;
        public event Action<ExecutionResult>? ComponentCompleted;
        public event Action<string>? Trace;

        private void TraceLog(string message)
        {
            Trace?.Invoke(message);
        }

        private static void ThrowIfStopped(Test_Automation.Models.ExecutionContext context, string scope)
        {
            if (context == null || !context.IsRunning || context.StopToken.IsCancellationRequested)
            {
                throw new OperationCanceledException($"Execution stopped: {scope}");
            }
        }

        public async Task<ExecutionResult> ExecuteComponent(Component component, Test_Automation.Models.ExecutionContext context)
        {
            if (component == null || context == null)
                throw new ArgumentNullException(nameof(component));

            ThrowIfStopped(context, component.Name);

            var result = new ExecutionResult
            {
                ComponentId = component.Id,
                ComponentName = component.Name,
                Status = "running",
                ThreadIndex = CurrentThreadIndex.Value ?? 0,
                ThreadGroupId = CurrentThreadGroupId.Value ?? string.Empty
            };

            TraceLog($"ExecuteComponent start: {component.Name} ({component.GetType().Name}) id={component.Id}");
            ComponentStarted?.Invoke(result);

            var originalSettings = component.Settings;
            var originalExtractors = component.Extractors;
            var originalAssertions = component.Assertions;

            try
            {
                var settingsToResolve = originalSettings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                TraceLog($"Resolving settings for {component.Name}: {settingsToResolve.Count} entries.");
                component.Settings = ResolveSettings(settingsToResolve, context);

                component.Extractors = ResolveExtractors(originalExtractors, context);
                component.Assertions = ResolveAssertions(originalAssertions, context);

                // Execute the component
                var componentData = await component.Execute(context);
                TraceLog($"Execute() completed for {component.Name}. Data type: {componentData?.GetType().Name ?? "<null>"}.");
                if (componentData != null)
                {
                    componentData.Properties["startTime"] = result.StartTime;
                    componentData.Properties["threadIndex"] = result.ThreadIndex;
                    componentData.Properties["threadGroupId"] = result.ThreadGroupId;
                }
                result.Data = componentData;
                ApplyVariableExtractors(component, context, componentData, TraceLog);
                var assertionResults = EvaluateAssertions(component, componentData, context, TraceLog);
                result.AssertionResults = assertionResults;
                result.AssertFailedCount = assertionResults.Count(item => !item.Passed && IsAssertMode(item.Mode));
                result.ExpectFailedCount = assertionResults.Count(item => !item.Passed && !IsAssertMode(item.Mode));
                result.AssertPassedCount = assertionResults.Count(item => item.Passed);

                TraceLog($"Assertions for {component.Name}: passed={result.AssertPassedCount}, assertFailed={result.AssertFailedCount}, expectFailed={result.ExpectFailedCount}.");

                var hasStopOnAssertFailure = assertionResults.Any(item => !item.Passed && IsStopOnAssertFailureMode(item.Mode));
                if (hasStopOnAssertFailure)
                {
                    context.RequestStop();
                    TraceLog($"Assertion requested stop for {component.Name}. Remaining components will not execute.");
                }

                if (result.AssertFailedCount > 0)
                {
                    result.Error = string.Join(" | ", assertionResults
                        .Where(item => !item.Passed && IsAssertMode(item.Mode))
                        .Select(item => item.Message));
                    result.MarkAsCompleted(false);
                    TraceLog($"ExecuteComponent failed (assert): {component.Name}. Error={result.Error}");
                }
                else
                {
                    result.MarkAsCompleted(true);
                    TraceLog($"ExecuteComponent passed: {component.Name}.");
                }

                result.Output = componentData?.ToString();
            }
            catch (OperationCanceledException ex)
            {
                result.Error = ex.Message;
                result.MarkAsCompleted(false);
                result.Status = "stopped";
                TraceLog($"ExecuteComponent canceled: {component.Name}. {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.MarkAsCompleted(false);
                TraceLog($"ExecuteComponent exception: {component.Name}. {ex.Message}");
            }
            finally
            {
                component.Settings = originalSettings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                component.Extractors = originalExtractors ?? new List<VariableExtractionRule>();
                component.Assertions = originalAssertions ?? new List<AssertionRule>();
                ComponentCompleted?.Invoke(result);
                TraceLog($"ExecuteComponent end: {component.Name}. status={result.Status}, durationMs={result.DurationMs}");
            }

            return result;
        }

        private static void ApplyVariableExtractors(Component component, Test_Automation.Models.ExecutionContext context, ComponentData? componentData, Action<string>? trace)
        {
            if (component.Extractors == null || component.Extractors.Count == 0)
            {
                trace?.Invoke($"No extractors configured for {component.Name}.");
                return;
            }

            foreach (var extractor in component.Extractors)
            {
                var variableName = extractor.VariableName?.Trim() ?? string.Empty;
                var source = extractor.Source?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(variableName) || string.IsNullOrWhiteSpace(source))
                {
                    trace?.Invoke($"Extractor skipped for {component.Name}: variable/source missing.");
                    continue;
                }

                trace?.Invoke($"Extractor start: variable='{variableName}', source='{source}', path='{extractor.JsonPath}'.");
                var sourceValue = ResolveSourceValue(component, componentData, source, trace);
                if (string.IsNullOrEmpty(sourceValue))
                {
                    trace?.Invoke($"Extractor source missing: {source}.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(extractor.JsonPath))
                {
                    context.SetVariable(variableName, sourceValue);
                    trace?.Invoke($"Extractor set variable '{variableName}' from source value (no path).");
                    continue;
                }

                var jsonPath = extractor.JsonPath.Trim();
                if (string.Equals(jsonPath, "$", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(jsonPath, "$.", StringComparison.OrdinalIgnoreCase))
                {
                    context.SetVariable(variableName, sourceValue);
                    trace?.Invoke($"Extractor set variable '{variableName}' using root path.");
                    continue;
                }

                if (TryExtractJsonPath(sourceValue, extractor.JsonPath, out var extracted, trace))
                {
                    context.SetVariable(variableName, extracted);
                    trace?.Invoke($"Extractor set variable '{variableName}' to '{extracted}'.");
                }
                else
                {
                    trace?.Invoke($"Extractor path not found: {extractor.JsonPath}");
                }
            }
        }

        private static string? ResolveSourceValue(Component component, ComponentData? componentData, string source, Action<string>? trace = null)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return null;
            }

            source = source.Trim();

            if (string.Equals(source, "PreviewResponse", StringComparison.OrdinalIgnoreCase))
            {
                trace?.Invoke("ResolveSourceValue using PreviewResponse payload.");
                return BuildPreviewResponse(componentData);
            }

            if (string.Equals(source, "PreviewRequest", StringComparison.OrdinalIgnoreCase))
            {
                trace?.Invoke("ResolveSourceValue using PreviewRequest payload.");
                return BuildPreviewRequest(componentData);
            }

            if (string.Equals(source, "PreviewLogs", StringComparison.OrdinalIgnoreCase))
            {
                trace?.Invoke("ResolveSourceValue using PreviewLogs payload.");
                return BuildPreviewLogs(componentData);
            }

            if (component.Settings != null && component.Settings.TryGetValue(source, out var settingValue))
            {
                trace?.Invoke($"ResolveSourceValue matched setting '{source}'.");
                return settingValue;
            }

            if (component.Settings != null)
            {
                var setting = component.Settings
                    .FirstOrDefault(entry => string.Equals(entry.Key?.Trim(), source, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(setting.Key))
                {
                    trace?.Invoke($"ResolveSourceValue matched setting '{setting.Key}' (normalized). ");
                    return setting.Value;
                }
            }

            if (componentData == null)
            {
                return null;
            }

            if (string.Equals(source, "ComponentData", StringComparison.OrdinalIgnoreCase))
            {
                trace?.Invoke("ResolveSourceValue using serialized ComponentData.");
                return JsonSerializer.Serialize(componentData);
            }

            if (source.StartsWith("ComponentData.", StringComparison.OrdinalIgnoreCase))
            {
                var path = source.Substring("ComponentData.".Length);
                var jsonPath = path.StartsWith("$") ? path : "$." + path;
                var json = JsonSerializer.Serialize(componentData);
                if (TryExtractJsonPath(json, jsonPath, out var extracted, trace))
                {
                    trace?.Invoke($"ResolveSourceValue extracted ComponentData path '{jsonPath}'.");
                    return extracted;
                }
            }

            trace?.Invoke($"ResolveSourceValue failed for source '{source}'.");

            return null;
        }

        private static List<AssertionEvaluationResult> EvaluateAssertions(Component component, ComponentData? componentData, Test_Automation.Models.ExecutionContext context, Action<string>? trace)
        {
            var evaluations = new List<AssertionEvaluationResult>();
            if (component.Assertions == null || component.Assertions.Count == 0)
            {
                trace?.Invoke($"No assertions configured for {component.Name}.");
                return evaluations;
            }

            for (var index = 0; index < component.Assertions.Count; index++)
            {
                var assertion = component.Assertions[index];
                var mode = NormalizeAssertionMode(assertion.Mode);
                var evaluation = new AssertionEvaluationResult
                {
                    Index = index,
                    Mode = mode,
                    Source = assertion.Source,
                    JsonPath = assertion.JsonPath,
                    Condition = assertion.Condition,
                    Expected = assertion.Expected
                };

                if (string.IsNullOrWhiteSpace(assertion.Source))
                {
                    evaluation.Passed = false;
                    evaluation.Message = $"{mode} failed: source is required.";
                    trace?.Invoke($"Assertion[{index}] {evaluation.Message}");
                    evaluations.Add(evaluation);
                    continue;
                }

                trace?.Invoke($"Assertion[{index}] evaluating: mode={mode}, source={assertion.Source}, path={assertion.JsonPath}, condition={assertion.Condition}, expected={assertion.Expected}");
                var sourceValue = ResolveSourceValue(component, componentData, assertion.Source, trace);
                if (string.IsNullOrEmpty(sourceValue))
                {
                    evaluation.Passed = false;
                    evaluation.Message = $"{mode} failed: source missing '{assertion.Source}'.";
                    trace?.Invoke($"Assertion[{index}] {evaluation.Message}");
                    evaluations.Add(evaluation);
                    continue;
                }

                var actual = sourceValue;
                if (!string.IsNullOrWhiteSpace(assertion.JsonPath)
                    && !string.Equals(assertion.JsonPath.Trim(), "$", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(assertion.JsonPath.Trim(), "$.", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryExtractJsonPath(sourceValue, assertion.JsonPath, out var extracted, trace))
                    {
                        evaluation.Passed = false;
                        evaluation.Actual = sourceValue;
                        evaluation.Message = $"{mode} failed: path not found '{assertion.JsonPath}'.";
                        trace?.Invoke($"Assertion[{index}] {evaluation.Message}");
                        evaluations.Add(evaluation);
                        continue;
                    }

                    actual = extracted;
                }

                evaluation.Actual = actual;

                var resolvedExpected = ResolveTokens(assertion.Expected ?? string.Empty, context);
                evaluation.Expected = resolvedExpected;

                if (string.Equals(assertion.Condition, "Script", StringComparison.OrdinalIgnoreCase))
                {
                    trace?.Invoke($"Assertion[{index}] Script expression: {resolvedExpected}");
                }

                evaluation.Passed = EvaluateCondition(actual, resolvedExpected, assertion.Condition, context, trace);
                if (evaluation.Passed)
                {
                    evaluation.Message = $"{mode} passed.";
                }
                else
                {
                    evaluation.Message = $"{mode} failed ({assertion.Condition}): actual='{actual}' expected='{resolvedExpected}'";
                }

                trace?.Invoke($"Assertion[{index}] {evaluation.Message}");

                evaluations.Add(evaluation);
            }

            return evaluations;
        }

        private static string NormalizeAssertionMode(string mode)
        {
            if (string.Equals(mode, "Expect", StringComparison.OrdinalIgnoreCase))
            {
                return "Expect";
            }

            if (string.Equals(mode, "Assert and Stop", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "AssertAndStop", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "Assertion and Stop", StringComparison.OrdinalIgnoreCase))
            {
                return "Assert and Stop";
            }

            return "Assert";
        }

        private static bool IsAssertMode(string mode)
        {
            return !string.Equals(mode, "Expect", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStopOnAssertFailureMode(string mode)
        {
            return string.Equals(mode, "Assert and Stop", StringComparison.OrdinalIgnoreCase);
        }

        private static bool EvaluateCondition(string actual, string expected, string condition, Test_Automation.Models.ExecutionContext? context = null, Action<string>? trace = null)
        {
            var op = string.IsNullOrWhiteSpace(condition) ? "Equals" : condition;
            var normalizedActual = NormalizeComparisonValue(actual);
            var normalizedExpected = NormalizeComparisonValue(expected);

            switch (op)
            {
                case "Equals":
                    return string.Equals(normalizedActual, normalizedExpected, StringComparison.OrdinalIgnoreCase);
                case "NotEquals":
                    return !string.Equals(normalizedActual, normalizedExpected, StringComparison.OrdinalIgnoreCase);
                case "Contains":
                    return normalizedActual.IndexOf(normalizedExpected, StringComparison.OrdinalIgnoreCase) >= 0;
                case "NotContains":
                    return normalizedActual.IndexOf(normalizedExpected, StringComparison.OrdinalIgnoreCase) < 0;
                case "StartsWith":
                    return normalizedActual.StartsWith(normalizedExpected, StringComparison.OrdinalIgnoreCase);
                case "EndsWith":
                    return normalizedActual.EndsWith(normalizedExpected, StringComparison.OrdinalIgnoreCase);
                case "IsEmpty":
                    return string.IsNullOrWhiteSpace(normalizedActual);
                case "IsNotEmpty":
                    return !string.IsNullOrWhiteSpace(normalizedActual);
                case "Regex":
                    try
                    {
                        return System.Text.RegularExpressions.Regex.IsMatch(normalizedActual, normalizedExpected, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                case "Script":
                    return EvaluateScriptAssertion(normalizedActual, normalizedExpected, context, trace);
                case "GreaterThan":
                    return CompareNumbers(normalizedActual, normalizedExpected, (a, b) => a > b);
                case "GreaterOrEqual":
                    return CompareNumbers(normalizedActual, normalizedExpected, (a, b) => a >= b);
                case "LessThan":
                    return CompareNumbers(normalizedActual, normalizedExpected, (a, b) => a < b);
                case "LessOrEqual":
                    return CompareNumbers(normalizedActual, normalizedExpected, (a, b) => a <= b);
                default:
                    return string.Equals(normalizedActual, normalizedExpected, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool EvaluateScriptAssertion(string actual, string script, Test_Automation.Models.ExecutionContext? context, Action<string>? trace)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                trace?.Invoke("Script assertion failed: script is empty.");
                return false;
            }

            var runtimeContext = context ?? new Test_Automation.Models.ExecutionContext();
            runtimeContext.SetVariable("actual", actual);

            if (TryParseNumber(actual, out var actualNumber))
            {
                runtimeContext.SetVariable("actualNumber", actualNumber.ToString(CultureInfo.InvariantCulture));
            }

            var language = "CSharp";
            var code = script;
            if (script.Contains("\n") && script.Contains("//lang", StringComparison.OrdinalIgnoreCase))
            {
                var lines = script.Split('\n');
                var marker = lines.FirstOrDefault(line => line.TrimStart().StartsWith("//lang", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(marker))
                {
                    var parts = marker.Split(':', 2, StringSplitOptions.TrimEntries);
                    if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
                    {
                        language = parts[1];
                    }
                }
            }

            trace?.Invoke($"Script assertion engine start: language={language}");
            var outcome = ScriptEngine.ExecuteAsync(language, code, runtimeContext, actual).GetAwaiter().GetResult();
            if (!outcome.Success)
            {
                trace?.Invoke($"Script assertion execution error: {outcome.Error}");
                return false;
            }

            if (outcome.Result is bool boolResult)
            {
                trace?.Invoke($"Script assertion result bool={boolResult}");
                return boolResult;
            }

            var text = outcome.Result?.ToString() ?? string.Empty;
            if (bool.TryParse(text, out var parsed))
            {
                trace?.Invoke($"Script assertion result parsed bool={parsed}");
                return parsed;
            }

            trace?.Invoke($"Script assertion returned non-boolean result: '{text}'");
            return false;
        }

        private static string NormalizeComparisonValue(string? value)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length >= 2
                && ((trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
                    || (trimmed.StartsWith("'") && trimmed.EndsWith("'"))))
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2);
            }

            return trimmed;
        }

        private static bool CompareNumbers(string actual, string expected, Func<double, double, bool> comparer)
        {
            if (!TryParseNumber(actual, out var actualNumber) || !TryParseNumber(expected, out var expectedNumber))
            {
                return false;
            }

            return comparer(actualNumber, expectedNumber);
        }

        private static bool TryParseNumber(string value, out double parsed)
        {
            return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed)
                || double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed);
        }

        private static string? BuildPreviewResponse(ComponentData? componentData)
        {
            if (componentData == null)
            {
                return null;
            }

            var runStartTime = ResolveComponentStartTime(componentData);

            if (componentData is HttpData httpData)
            {
                var parsedBody = TryParseJson(httpData.ResponseBody);
                return JsonSerializer.Serialize(new
                {
                    status = httpData.ResponseStatus,
                    body = parsedBody,
                    headers = httpData.Headers,
                    startTime = runStartTime,
                    runs = new[]
                    {
                        new
                        {
                            threadIndex = CurrentThreadIndex.Value ?? 0,
                            startTime = runStartTime,
                            responseStatus = httpData.ResponseStatus,
                            responseBody = parsedBody,
                            headers = httpData.Headers
                        }
                    }
                });
            }

            if (componentData is GraphQlData graphQlData)
            {
                var parsedBody = TryParseJson(graphQlData.ResponseBody);
                return JsonSerializer.Serialize(new
                {
                    status = graphQlData.ResponseStatus,
                    body = parsedBody,
                    headers = graphQlData.Headers,
                    startTime = runStartTime,
                    runs = new[]
                    {
                        new
                        {
                            threadIndex = CurrentThreadIndex.Value ?? 0,
                            startTime = runStartTime,
                            responseStatus = graphQlData.ResponseStatus,
                            responseBody = parsedBody,
                            headers = graphQlData.Headers
                        }
                    }
                });
            }

            if (componentData is DatasetData datasetData)
            {
                var datasetRunTimestamp = runStartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                var datasetSnapshot = new
                {
                    id = datasetData.Id,
                    componentName = datasetData.ComponentName,
                    properties = datasetData.Properties,
                    timestamp = datasetData.Timestamp,
                    dataSource = datasetData.DataSource,
                    rows = datasetData.Rows,
                    currentRow = datasetData.CurrentRow
                };

                return JsonSerializer.Serialize(new
                {
                    datasetSnapshot.id,
                    datasetSnapshot.componentName,
                    datasetSnapshot.properties,
                    datasetSnapshot.timestamp,
                    datasetSnapshot.dataSource,
                    datasetSnapshot.rows,
                    datasetSnapshot.currentRow,
                    runs = new[]
                    {
                        new
                        {
                            threadIndex = CurrentThreadIndex.Value ?? 0,
                            startTime = datasetRunTimestamp,
                            endTime = datasetRunTimestamp,
                            durationMs = 0,
                            status = "passed",
                            error = string.Empty,
                            data = datasetSnapshot
                        }
                    }
                });
            }

            if (componentData is SqlData sqlData)
            {
                sqlData.Properties.TryGetValue("rowsAffected", out var rowsAffected);
                return JsonSerializer.Serialize(new
                {
                    rows = sqlData.QueryResult,
                    affectedRows = rowsAffected
                });
            }

            return JsonSerializer.Serialize(componentData);
        }

        private static string? BuildPreviewRequest(ComponentData? componentData)
        {
            if (componentData == null)
            {
                return null;
            }

            if (componentData is HttpData httpData)
            {
                return JsonSerializer.Serialize(new
                {
                    method = httpData.Method,
                    url = httpData.Url,
                    headers = httpData.Headers,
                    body = TryParseJson(httpData.Body),
                    runs = new[]
                    {
                        new
                        {
                            threadIndex = CurrentThreadIndex.Value ?? 0,
                            method = httpData.Method,
                            url = httpData.Url,
                            headers = httpData.Headers,
                            body = TryParseJson(httpData.Body)
                        }
                    }
                });
            }

            if (componentData is GraphQlData graphQlData)
            {
                return JsonSerializer.Serialize(new
                {
                    endpoint = graphQlData.Endpoint,
                    query = graphQlData.Query,
                    variables = TryParseJson(graphQlData.Variables),
                    headers = graphQlData.Headers,
                    runs = new[]
                    {
                        new
                        {
                            threadIndex = CurrentThreadIndex.Value ?? 0,
                            endpoint = graphQlData.Endpoint,
                            query = graphQlData.Query,
                            variables = TryParseJson(graphQlData.Variables),
                            headers = graphQlData.Headers
                        }
                    }
                });
            }

            if (componentData is SqlData sqlData)
            {
                return JsonSerializer.Serialize(new
                {
                    connection = sqlData.ConnectionString,
                    query = sqlData.Query,
                    runs = new[]
                    {
                        new
                        {
                            threadIndex = CurrentThreadIndex.Value ?? 0,
                            connection = sqlData.ConnectionString,
                            query = sqlData.Query
                        }
                    }
                });
            }

            if (componentData is ScriptData scriptData)
            {
                return JsonSerializer.Serialize(new
                {
                    language = scriptData.ScriptLanguage,
                    code = scriptData.ScriptCode,
                    runs = new[]
                    {
                        new
                        {
                            threadIndex = CurrentThreadIndex.Value ?? 0,
                            language = scriptData.ScriptLanguage,
                            code = scriptData.ScriptCode
                        }
                    }
                });
            }

            return JsonSerializer.Serialize(componentData);
        }

        private static string? BuildPreviewLogs(ComponentData? componentData)
        {
            if (componentData == null)
            {
                return null;
            }

            var request = BuildPreviewRequest(componentData);
            var response = BuildPreviewResponse(componentData);

            return JsonSerializer.Serialize(new
            {
                request,
                response,
                component = componentData.GetType().Name,
                timestampUtc = DateTime.UtcNow
            });
        }

        private static object? TryParseJson(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return payload ?? string.Empty;
            }

            try
            {
                using var doc = JsonDocument.Parse(payload);
                return doc.RootElement.Clone();
            }
            catch
            {
                return payload;
            }
        }

        private static DateTime ResolveComponentStartTime(ComponentData componentData)
        {
            if (componentData.Properties.TryGetValue("startTime", out var startTimeValue) && startTimeValue != null)
            {
                if (startTimeValue is DateTime startTimeDateTime)
                {
                    return startTimeDateTime;
                }

                if (DateTime.TryParse(startTimeValue.ToString(), out var parsedStartTime))
                {
                    return parsedStartTime;
                }
            }

            return componentData.Timestamp;
        }

        private static bool TryExtractJsonPath(string json, string path, out string extracted, Action<string>? trace = null)
        {
            extracted = string.Empty;
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(path))
            {
                trace?.Invoke($"TryExtractJsonPath skipped: json/path missing. path='{path}'.");
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var normalized = path.Trim();
                if (normalized.StartsWith("$"))
                {
                    normalized = normalized.TrimStart('$');
                    if (normalized.StartsWith("."))
                    {
                        normalized = normalized.Substring(1);
                    }
                }

                trace?.Invoke($"TryExtractJsonPath start: path='{path}', normalized='{normalized}'.");

                if (TryExtractJsonPathFromElement(doc.RootElement, normalized, out extracted, trace))
                {
                    trace?.Invoke($"TryExtractJsonPath success: path='{path}', extracted='{extracted}'.");
                    return true;
                }

                // User-friendly fallback: many users expect PreviewResponse/PreviewRequest JSON path
                // to target payload body directly (e.g. $.token instead of $.body.token).
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && !normalized.StartsWith("body", StringComparison.OrdinalIgnoreCase)
                    && TryGetPropertyIgnoreCase(doc.RootElement, "body", out var bodyElement)
                    && TryExtractJsonPathFromElement(bodyElement, normalized, out extracted, trace))
                {
                    trace?.Invoke($"TryExtractJsonPath fallback via 'body' succeeded: path='{path}', extracted='{extracted}'.");
                    return true;
                }

                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && !normalized.StartsWith("responseBody", StringComparison.OrdinalIgnoreCase)
                    && TryGetPropertyIgnoreCase(doc.RootElement, "responseBody", out var responseBodyElement)
                    && TryExtractJsonPathFromElement(responseBodyElement, normalized, out extracted, trace))
                {
                    trace?.Invoke($"TryExtractJsonPath fallback via 'responseBody' succeeded: path='{path}', extracted='{extracted}'.");
                    return true;
                }

                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && !normalized.StartsWith("variables", StringComparison.OrdinalIgnoreCase)
                    && TryGetPropertyIgnoreCase(doc.RootElement, "variables", out var variablesElement)
                    && TryExtractJsonPathFromElement(variablesElement, normalized, out extracted, trace))
                {
                    trace?.Invoke($"TryExtractJsonPath fallback via 'variables' succeeded: path='{path}', extracted='{extracted}'.");
                    return true;
                }

                trace?.Invoke($"TryExtractJsonPath failed for path '{path}'.");
                return false;
            }
            catch
            {
                trace?.Invoke($"TryExtractJsonPath exception for path '{path}'.");
                return false;
            }
        }

        private static bool TryExtractJsonPathFromElement(JsonElement sourceElement, string normalizedPath, out string extracted, Action<string>? trace)
        {
            extracted = string.Empty;
            var element = sourceElement;
            var segments = normalizedPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                if (!TryResolveSegment(ref element, segment))
                {
                    trace?.Invoke($"TryExtractJsonPath failed at segment '{segment}'.");
                    return false;
                }
            }

            extracted = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => element.GetRawText()
            };

            return true;
        }

        private static bool TryResolveSegment(ref JsonElement element, string segment)
        {
            var remaining = segment;
            while (remaining.Length > 0)
            {
                var bracketIndex = remaining.IndexOf('[');
                if (bracketIndex < 0)
                {
                    return TryResolvePropertyOrIndex(ref element, remaining);
                }

                var propertyName = remaining.Substring(0, bracketIndex);
                if (!string.IsNullOrEmpty(propertyName))
                {
                    if (!TryResolvePropertyOrIndex(ref element, propertyName))
                    {
                        return false;
                    }
                }

                var endBracket = remaining.IndexOf(']', bracketIndex + 1);
                if (endBracket < 0)
                {
                    return false;
                }

                var indexValue = remaining.Substring(bracketIndex + 1, endBracket - bracketIndex - 1);
                if (!int.TryParse(indexValue, out var index))
                {
                    return false;
                }

                if (element.ValueKind == JsonValueKind.String
                    && TryParseJsonString(element.GetString(), out var parsedFromString))
                {
                    element = parsedFromString;
                }

                if (element.ValueKind != JsonValueKind.Array || index < 0 || index >= element.GetArrayLength())
                {
                    return false;
                }

                element = element[index];
                remaining = remaining.Substring(endBracket + 1);
            }

            return true;
        }

        private static bool TryResolvePropertyOrIndex(ref JsonElement element, string token)
        {
            if (element.ValueKind == JsonValueKind.String
                && TryParseJsonString(element.GetString(), out var parsedFromString))
            {
                element = parsedFromString;
            }

            if (element.ValueKind == JsonValueKind.Array && int.TryParse(token, out var index))
            {
                if (index < 0 || index >= element.GetArrayLength())
                {
                    return false;
                }

                element = element[index];
                return true;
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!TryGetPropertyIgnoreCase(element, token, out var next))
            {
                return false;
            }

            element = next;
            return true;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement element, string token, out JsonElement next)
        {
            if (element.TryGetProperty(token, out next))
            {
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, token, StringComparison.OrdinalIgnoreCase))
                {
                    next = property.Value;
                    return true;
                }
            }

            next = default;
            return false;
        }

        private static bool TryParseJsonString(string? text, out JsonElement parsedElement)
        {
            parsedElement = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.Trim();
            if (!(trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                && !(trimmed.StartsWith("[") && trimmed.EndsWith("]")))
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                parsedElement = doc.RootElement.Clone();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ExecutionResult> ExecuteComponentTree(Component component, Test_Automation.Models.ExecutionContext context)
        {
            ThrowIfStopped(context, component.Name);
            TraceLog($"ExecuteComponentTree enter: {component.Name} ({component.GetType().Name})");
            if (component is Threads threadsComponent)
            {
                var threadCount = 1;
                if (threadsComponent.Settings.TryGetValue("ThreadCount", out var countValue)
                    && int.TryParse(countValue, out var parsed)
                    && parsed > 0)
                {
                    threadCount = parsed;
                }

                TraceLog($"ExecuteComponentTree dispatch Threads: count={threadCount}");
                return await ExecuteThreadGroup(threadsComponent, context, threadCount);
            }

            if (component is Loop loopComponent)
            {
                TraceLog("ExecuteComponentTree dispatch Loop");
                return await ExecuteLoop(loopComponent, context);
            }

            if (component is If ifComponent)
            {
                TraceLog("ExecuteComponentTree dispatch If");
                return await ExecuteIf(ifComponent, context);
            }

            if (component is Foreach foreachComponent)
            {
                TraceLog("ExecuteComponentTree dispatch Foreach");
                return await ExecuteForeach(foreachComponent, context);
            }

            var result = await ExecuteComponent(component, context);

            // If component has children, execute them sequentially
            if (component.Children.Count > 0)
            {
                TraceLog($"ExecuteComponentTree children: {component.Children.Count} under {component.Name}.");
                var childResults = new List<ExecutionResult>();
                foreach (var child in component.Children)
                {
                    ThrowIfStopped(context, child.Name);
                    var childResult = await ExecuteComponentTree(child, context);
                    childResults.Add(childResult);
                    lock (context.Results)
                    {
                        context.Results.Add(childResult);
                    }
                }
            }

            TraceLog($"ExecuteComponentTree exit: {component.Name}, status={result.Status}.");

            return result;
        }

        private async Task<ExecutionResult> ExecuteLoop(Loop loopComponent, Test_Automation.Models.ExecutionContext context)
        {
            ThrowIfStopped(context, loopComponent.Name);
            var result = await ExecuteComponent(loopComponent, context);

            var iterations = 1;
            if (loopComponent.Settings.TryGetValue("Iterations", out var value)
                && int.TryParse(value, out var parsed)
                && parsed > 0)
            {
                iterations = parsed;
            }

            TraceLog($"ExecuteLoop iterations={iterations} for {loopComponent.Name}.");

            var previousIndex = context.GetVariable("LoopIndex");
            for (var i = 0; i < iterations; i++)
            {
                ThrowIfStopped(context, $"{loopComponent.Name} iteration {i}");
                context.SetVariable("LoopIndex", i);
                foreach (var child in loopComponent.Children)
                {
                    ThrowIfStopped(context, child.Name);
                    var childResult = await ExecuteComponentTree(child, context);
                    lock (context.Results)
                    {
                        context.Results.Add(childResult);
                    }
                }
            }

            if (previousIndex != null)
            {
                context.SetVariable("LoopIndex", previousIndex);
            }

            return result;
        }

        private async Task<ExecutionResult> ExecuteIf(If ifComponent, Test_Automation.Models.ExecutionContext context)
        {
            ThrowIfStopped(context, ifComponent.Name);
            var result = await ExecuteComponent(ifComponent, context);
            var condition = ifComponent.Settings.TryGetValue("Condition", out var value) ? value : string.Empty;
            var conditionMet = EvaluateCondition(condition, context);
            TraceLog($"ExecuteIf condition='{condition}', resolved={conditionMet} for {ifComponent.Name}.");

            if (result.Data is IfData ifData)
            {
                ifData.Condition = condition;
                ifData.ConditionMet = conditionMet;
            }

            if (conditionMet)
            {
                foreach (var child in ifComponent.Children)
                {
                    ThrowIfStopped(context, child.Name);
                    var childResult = await ExecuteComponentTree(child, context);
                    lock (context.Results)
                    {
                        context.Results.Add(childResult);
                    }
                }
            }

            return result;
        }

        private async Task<ExecutionResult> ExecuteForeach(Foreach foreachComponent, Test_Automation.Models.ExecutionContext context)
        {
            ThrowIfStopped(context, foreachComponent.Name);
            var result = await ExecuteComponent(foreachComponent, context);
            var sourceVariable = foreachComponent.Settings.TryGetValue("SourceVariable", out var value)
                ? value
                : string.Empty;

            var previousItem = context.GetVariable("CurrentItem");
            var previousIndex = context.GetVariable("CurrentIndex");

            var collection = ResolveCollection(context, sourceVariable);
            TraceLog($"ExecuteForeach source='{sourceVariable}' for {foreachComponent.Name}.");
            var index = 0;
            foreach (var item in collection)
            {
                ThrowIfStopped(context, $"{foreachComponent.Name} iteration {index}");
                TraceLog($"ExecuteForeach iteration index={index}, itemType={item?.GetType().Name ?? "<null>"}.");
                context.SetVariable("CurrentItem", item);
                context.SetVariable("CurrentIndex", index);
                foreach (var child in foreachComponent.Children)
                {
                    ThrowIfStopped(context, child.Name);
                    var childResult = await ExecuteComponentTree(child, context);
                    lock (context.Results)
                    {
                        context.Results.Add(childResult);
                    }
                }
                index++;
            }

            if (previousItem != null)
            {
                context.SetVariable("CurrentItem", previousItem);
            }

            if (previousIndex != null)
            {
                context.SetVariable("CurrentIndex", previousIndex);
            }

            return result;
        }

        private static IEnumerable<object> ResolveCollection(Test_Automation.Models.ExecutionContext context, string sourceVariable)
        {
            if (string.IsNullOrWhiteSpace(sourceVariable))
            {
                return Array.Empty<object>();
            }

            var value = context.GetVariable(sourceVariable);
            if (value == null)
            {
                return Array.Empty<object>();
            }

            if (value is IEnumerable<object> objectEnumerable)
            {
                return objectEnumerable;
            }

            if (value is System.Collections.IEnumerable enumerable && value is not string)
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    list.Add(item ?? string.Empty);
                }
                return list;
            }

            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                return jsonElement.EnumerateArray().Select(element => (object)element.Clone()).ToList();
            }

            if (value is string text)
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        return doc.RootElement.EnumerateArray().Select(element => (object)element.Clone()).ToList();
                    }
                }
                catch
                {
                }

                return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Cast<object>()
                    .ToList();
            }

            return new[] { value };
        }

        private static bool EvaluateCondition(string condition, Test_Automation.Models.ExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                return false;
            }

            var resolved = ResolveConditionTokens(condition, context).Trim();
            if (bool.TryParse(resolved, out var boolValue))
            {
                return boolValue;
            }

            var operators = new[] { ">=", "<=", "==", "!=", ">", "<" };
            var op = operators.FirstOrDefault(symbol => resolved.Contains(symbol, StringComparison.Ordinal));
            if (op == null)
            {
                return !string.IsNullOrWhiteSpace(resolved);
            }

            var parts = resolved.Split(op, 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                return false;
            }

            var left = parts[0];
            var right = parts[1];

            if (double.TryParse(left, out var leftNumber) && double.TryParse(right, out var rightNumber))
            {
                return op switch
                {
                    ">" => leftNumber > rightNumber,
                    "<" => leftNumber < rightNumber,
                    ">=" => leftNumber >= rightNumber,
                    "<=" => leftNumber <= rightNumber,
                    "==" => Math.Abs(leftNumber - rightNumber) < double.Epsilon,
                    "!=" => Math.Abs(leftNumber - rightNumber) >= double.Epsilon,
                    _ => false
                };
            }

            var comparison = StringComparer.OrdinalIgnoreCase.Compare(left, right);
            return op switch
            {
                "==" => comparison == 0,
                "!=" => comparison != 0,
                ">" => comparison > 0,
                "<" => comparison < 0,
                ">=" => comparison >= 0,
                "<=" => comparison <= 0,
                _ => false
            };
        }

        private static string ResolveConditionTokens(string condition, Test_Automation.Models.ExecutionContext context)
        {
            return ResolveTokens(condition, context);
        }

        private static string ResolveTokens(string template, Test_Automation.Models.ExecutionContext context)
        {
            if (string.IsNullOrEmpty(template))
            {
                return template;
            }

            return System.Text.RegularExpressions.Regex.Replace(template, "\\$\\{([^}]+)\\}", match =>
            {
                var key = match.Groups[1].Value.Trim();
                var value = context.GetVariable(key);
                return value?.ToString() ?? string.Empty;
            });
        }

        private static Dictionary<string, string> ResolveSettings(Dictionary<string, string> settings, Test_Automation.Models.ExecutionContext context)
        {
            var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in settings)
            {
                var value = entry.Value ?? string.Empty;
                resolved[entry.Key] = ResolveTokens(value, context);
            }

            return resolved;
        }

        private static List<VariableExtractionRule> ResolveExtractors(List<VariableExtractionRule>? extractors, Test_Automation.Models.ExecutionContext context)
        {
            var resolved = new List<VariableExtractionRule>();
            if (extractors == null || extractors.Count == 0)
            {
                return resolved;
            }

            foreach (var extractor in extractors)
            {
                if (extractor == null)
                {
                    continue;
                }

                resolved.Add(new VariableExtractionRule(
                    extractor.Source ?? string.Empty,
                    ResolveTokens(extractor.JsonPath ?? string.Empty, context),
                    ResolveExtractorVariableName(extractor.VariableName, context)));
            }

            return resolved;
        }

        private static readonly Regex SingleTokenVariableName = new Regex("^\\$\\{([^}]+)\\}$", RegexOptions.Compiled);

        private static string ResolveExtractorVariableName(string? template, Test_Automation.Models.ExecutionContext context)
        {
            var raw = template?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var singleTokenMatch = SingleTokenVariableName.Match(raw);
            if (singleTokenMatch.Success)
            {
                var inner = singleTokenMatch.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(inner))
                {
                    return inner;
                }
            }

            var resolved = ResolveTokens(raw, context)?.Trim();
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }

            return raw;
        }

        private static List<AssertionRule> ResolveAssertions(List<AssertionRule>? assertions, Test_Automation.Models.ExecutionContext context)
        {
            var resolved = new List<AssertionRule>();
            if (assertions == null || assertions.Count == 0)
            {
                return resolved;
            }

            foreach (var assertion in assertions)
            {
                if (assertion == null)
                {
                    continue;
                }

                resolved.Add(new AssertionRule(
                    assertion.Source ?? string.Empty,
                    ResolveTokens(assertion.JsonPath ?? string.Empty, context),
                    assertion.Condition ?? string.Empty,
                    assertion.Expected ?? string.Empty,
                    assertion.Mode ?? "Assert"));
            }

            return resolved;
        }

        public async Task<ExecutionResult> ExecuteThreadGroup(Threads threadComponent, Test_Automation.Models.ExecutionContext context, int threadCount = 1)
        {
            ThrowIfStopped(context, threadComponent.Name);
            TraceLog($"ExecuteThreadGroup start: {threadComponent.Name}, threadCount={threadCount}.");
            var result = new ExecutionResult
            {
                ComponentId = threadComponent.Id,
                ComponentName = threadComponent.Name,
                Status = "running",
                ThreadIndex = 0,
                ThreadGroupId = threadComponent.Id
            };

            try
            {
                if (threadCount <= 1)
                {
                    await ExecuteThreadChildren(threadComponent, context, 1);
                }
                else
                {
                    var tasks = Enumerable.Range(1, threadCount)
                        .Select(index => ExecuteThreadChildren(threadComponent, context, index))
                        .ToList();
                    await Task.WhenAll(tasks);
                }

                result.MarkAsCompleted(true);
                TraceLog($"ExecuteThreadGroup completed: {threadComponent.Name}.");
            }
            catch (OperationCanceledException ex)
            {
                result.Error = ex.Message;
                result.MarkAsCompleted(false);
                result.Status = "stopped";
                TraceLog($"ExecuteThreadGroup canceled: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.MarkAsCompleted(false);
                TraceLog($"ExecuteThreadGroup exception: {ex.Message}");
            }

            return result;
        }

        private async Task ExecuteThreadChildren(Threads threadComponent, Test_Automation.Models.ExecutionContext context, int threadIndex)
        {
            ThrowIfStopped(context, $"{threadComponent.Name} thread {threadIndex}");
            CurrentThreadIndex.Value = threadIndex;
            CurrentThreadGroupId.Value = threadComponent.Id;
            TraceLog($"ExecuteThreadChildren start: group={threadComponent.Name}, threadIndex={threadIndex}.");

            foreach (var child in threadComponent.Children)
            {
                ThrowIfStopped(context, $"{threadComponent.Name} thread {threadIndex} child {child.Name}");
                var childResult = await ExecuteComponentTree(child, context);
                lock (context.Results)
                {
                    context.Results.Add(childResult);
                }
            }

            TraceLog($"ExecuteThreadChildren end: group={threadComponent.Name}, threadIndex={threadIndex}.");
        }
    }
}
