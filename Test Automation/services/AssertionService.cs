using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Test_Automation.Componentes;
using Test_Automation.Models;
using Test_Automation.Models.Editor;
using ExecutionContext = Test_Automation.Models.ExecutionContext;

namespace Test_Automation.Services
{
    public class AssertionService : IAssertionService
    {
        public List<AssertionRule> ResolveAssertions(List<AssertionRule> assertions, ExecutionContext context)
        {
            if (assertions == null || assertions.Count == 0)
            {
                return new List<AssertionRule>();
            }

            return assertions.Select(a => new AssertionRule(
                a.Source,
                a.JsonPath,
                a.Condition,
                ResolveText(a.Expected, context),
                a.Mode
            )).ToList();
        }

        public List<AssertionEvaluationResult> EvaluateAssertions(Component component, ComponentData? componentData, ExecutionContext context, Action<string> trace)
        {
            var results = new List<AssertionEvaluationResult>();
            if (component.Assertions == null || componentData == null)
            {
                return results;
            }

            trace($"Evaluating {component.Assertions.Count} assertions for {component.Name}.");

            for (var index = 0; index < component.Assertions.Count; index++)
            {
                var assertion = component.Assertions[index];
                var sourceValue = GetSourceValue(assertion.Source, componentData);
                var actualValue = ExtractValue(sourceValue, assertion.JsonPath);
                var (passed, message) = Compare(actualValue, assertion.Condition, assertion.Expected);

                var result = new AssertionEvaluationResult
                {
                    Index = index,
                    Passed = passed,
                    Message = message,
                    Mode = assertion.Mode,
                    Source = assertion.Source,
                    JsonPath = assertion.JsonPath,
                    Condition = assertion.Condition,
                    Expected = assertion.Expected,
                    Actual = ConvertToText(actualValue)
                };
                results.Add(result);

                trace($"Assertion '{assertion.Condition}' on '{assertion.Source}': expected='{assertion.Expected}', actual='{actualValue}'. Passed: {passed}.");
            }

            return results;
        }

        private (bool Passed, string Message) Compare(object? actual, string condition, string expected)
        {
            var actualText = ConvertToText(actual);

            switch (condition)
            {
                case "Equals":
                    var passed = string.Equals(actualText, expected, StringComparison.Ordinal);
                    return (passed, $"Expected '{expected}', but got '{actualText}'.");
                case "NotEquals":
                    passed = !string.Equals(actualText, expected, StringComparison.Ordinal);
                    return (passed, $"Expected value not to be '{expected}', but it was.");
                case "Contains":
                    passed = actualText?.Contains(expected, StringComparison.Ordinal) ?? false;
                    return (passed, $"Expected value to contain '{expected}', but it did not.");
                case "NotContains":
                    passed = !(actualText?.Contains(expected, StringComparison.Ordinal) ?? false);
                    return (passed, $"Expected value not to contain '{expected}', but it did.");
                case "GreaterThan":
                    if (decimal.TryParse(actualText, NumberStyles.Any, CultureInfo.InvariantCulture, out var actualNum) &&
                        decimal.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out var expectedNum))
                    {
                        return (actualNum > expectedNum, $"Expected value to be greater than '{expected}', but got '{actualText}'.");
                    }
                    return (false, "Cannot compare non-numeric values for GreaterThan.");
                case "LessThan":
                     if (decimal.TryParse(actualText, NumberStyles.Any, CultureInfo.InvariantCulture, out actualNum) &&
                        decimal.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out expectedNum))
                    {
                        return (actualNum < expectedNum, $"Expected value to be less than '{expected}', but got '{actualText}'.");
                    }
                    return (false, "Cannot compare non-numeric values for LessThan.");
                default:
                    return (false, $"Unsupported assertion condition: {condition}");
            }
        }

        private object? GetSourceValue(string source, ComponentData componentData)
        {
            // Handle UI source names (PreviewResponse, PreviewRequest, etc.)
            if (string.Equals(source, "PreviewResponse", StringComparison.OrdinalIgnoreCase))
            {
                return componentData switch
                {
                    HttpData http => http.ResponseBody,
                    GraphQlData gql => gql.ResponseBody,
                    SqlData sql => SerializeQueryResult(sql.QueryResult),
                    DatasetData dataset => SerializeDatasetRows(dataset.Rows),
                    _ => null
                };
            }

            if (string.Equals(source, "PreviewRequest", StringComparison.OrdinalIgnoreCase))
            {
                return componentData switch
                {
                    HttpData http => http.Body,
                    GraphQlData gql => gql.Query,
                    SqlData sql => sql.Query,
                    _ => null
                };
            }

            // Handle Dataset-specific source
            if (string.Equals(source, "DatasetRows", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, "Rows", StringComparison.OrdinalIgnoreCase))
            {
                if (componentData is DatasetData dataset)
                {
                    return dataset.Rows;
                }
                return null;
            }

            if (string.Equals(source, "Body", StringComparison.OrdinalIgnoreCase))
            {
                return componentData switch
                {
                    HttpData http => http.ResponseBody,
                    GraphQlData gql => gql.ResponseBody,
                    _ => null
                };
            }

            if (string.Equals(source, "Status", StringComparison.OrdinalIgnoreCase))
            {
                return componentData switch
                {
                    HttpData http => http.ResponseStatus,
                    GraphQlData gql => gql.ResponseStatus,
                    _ => null
                };
            }
            
            if (componentData.Properties.TryGetValue(source, out var propValue))
            {
                return propValue;
            }

            return null;
        }

        private string SerializeQueryResult(List<Dictionary<string, object>>? queryResult)
        {
            if (queryResult == null || queryResult.Count == 0)
            {
                return "[]";
            }

            try
            {
                return System.Text.Json.JsonSerializer.Serialize(queryResult);
            }
            catch
            {
                return "[]";
            }
        }

        private string SerializeDatasetRows(List<Dictionary<string, object>>? rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return "[]";
            }

            try
            {
                return System.Text.Json.JsonSerializer.Serialize(rows);
            }
            catch
            {
                return "[]";
            }
        }

        private object? ExtractValue(object? sourceValue, string jsonPath)
        {
            if (sourceValue == null) return null;

            var sourceText = ConvertToText(sourceValue);
            if (string.IsNullOrWhiteSpace(jsonPath) || string.IsNullOrWhiteSpace(sourceText))
            {
                return sourceValue;
            }

            try
            {
                using var doc = JsonDocument.Parse(sourceText);
                if (doc.RootElement.TryGetPropertyByJsonPath(jsonPath, out var element))
                {
                    return element.ValueKind switch
                    {
                        JsonValueKind.String => element.GetString(),
                        JsonValueKind.Number => element.GetDecimal(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => element.GetRawText()
                    };
                }
            }
            catch (JsonException)
            {
                // Not a valid JSON, so cannot apply JsonPath
            }

            return null;
        }
        
        private string ResolveText(string? text, ExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return System.Text.RegularExpressions.Regex.Replace(text, "\\$\\{([^}]+)\\}", match =>
            {
                var key = match.Groups[1].Value.Trim();
                if (!context.HasVariable(key))
                {
                    return match.Value;
                }
                return ConvertToText(context.GetVariable(key));
            });
        }

        private string ConvertToText(object? value)
        {
            if (value == null) return string.Empty;
            if (value is JsonElement json)
            {
                return json.ValueKind == JsonValueKind.String
                    ? json.GetString() ?? string.Empty
                    : json.GetRawText();
            }
            return value.ToString() ?? string.Empty;
        }
    }
}
