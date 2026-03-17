using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Test_Automation.Componentes;
using Test_Automation.Models;
using Test_Automation.Models.Editor;
using ExecutionContext = Test_Automation.Models.ExecutionContext;

namespace Test_Automation.Services
{
    public class VariableService : IVariableService
    {
        public Dictionary<string, string> ResolveSettings(Dictionary<string, string> settings, ExecutionContext context)
        {
            if (settings == null || settings.Count == 0)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var resolvedSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var setting in settings)
            {
                resolvedSettings[setting.Key] = ResolveText(setting.Value, context);
            }
            return resolvedSettings;
        }

        public List<VariableExtractionRule> ResolveExtractors(List<VariableExtractionRule> extractors, ExecutionContext context)
        {
            if (extractors == null || extractors.Count == 0)
            {
                return new List<VariableExtractionRule>();
            }

            return extractors.Select(e => new VariableExtractionRule(
                ResolveText(e.Source, context),
                ResolveText(e.JsonPath, context),
                ResolveText(e.VariableName, context)
            )).ToList();
        }

        public void ApplyVariableExtractors(Component component, ExecutionContext context, ComponentData? componentData, Action<string> trace)
        {
            if (component.Extractors == null || component.Extractors.Count == 0 || componentData == null)
            {
                return;
            }

            trace($"Applying {component.Extractors.Count} variable extractors for {component.Name}.");

            foreach (var extractor in component.Extractors)
            {
                if (string.IsNullOrWhiteSpace(extractor.VariableName))
                {
                    trace($"Skipping extractor with empty variable name for {component.Name}.");
                    continue;
                }

                // Handle Variable source - get value from context variables
                if (extractor.Source.StartsWith("Variable.", StringComparison.OrdinalIgnoreCase))
                {
                    var varName = extractor.Source.Substring("Variable.".Length);
                    var value = context.GetVariable(varName);
                    trace($"Extractor '{extractor.VariableName}': Variable source '{varName}' = {value}");
                    context.SetVariable(extractor.VariableName, value ?? string.Empty);
                    continue;
                }

                var sourceValue = GetSourceValue(extractor.Source, componentData);
                
                trace($"Extractor '{extractor.VariableName}': GetSourceValue returned type={sourceValue?.GetType().Name ?? "null"}");
                
                if (sourceValue == null)
                {
                    trace($"Extractor for '{extractor.VariableName}': source '{extractor.Source}' returned null. Component type: {componentData.GetType().Name}");
                    continue;
                }

                var extractedValue = ExtractValue(sourceValue, extractor.JsonPath);

                if (extractedValue == null)
                {
                    // JSON path didn't match - try to use the raw source value
                    trace($"Extractor for '{extractor.VariableName}': jsonPath='{extractor.JsonPath}' returned null. Using source value directly.");
                    trace($"  Source value type: {sourceValue.GetType().Name}, preview: {(sourceValue.ToString()?.Substring(0, Math.Min(100, sourceValue.ToString()?.Length ?? 0)))}");
                    context.SetVariable(extractor.VariableName, sourceValue);
                    trace($"  Set variable '{extractor.VariableName}' to: {sourceValue}");
                }
                else
                {
                    trace($"Extractor for '{extractor.VariableName}': extracted value type={extractedValue.GetType().Name}, preview={(extractedValue.ToString()?.Substring(0, Math.Min(50, extractedValue.ToString()?.Length ?? 0)))}");
                    context.SetVariable(extractor.VariableName, extractedValue);
                }
            }
        }

        private string ResolveText(string? text, ExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return Regex.Replace(text, "\\$\\{([^}]+)\\}", match =>
            {
                var key = match.Groups[1].Value.Trim();
                if (!context.HasVariable(key))
                {
                    return match.Value;
                }
                return ConvertVariableToText(context.GetVariable(key));
            });
        }

        private object? GetSourceValue(string source, ComponentData componentData)
        {
            // Handle Variable source - read from component's variables (set during execution)
            if (source.StartsWith("Variable.", StringComparison.OrdinalIgnoreCase))
            {
                // This will be handled at execution time with the actual context
                // For now, return the variable name as the value
                return source.Substring("Variable.".Length);
            }

            // Handle UI source names (PreviewResponse, PreviewRequest, etc.)
            if (string.Equals(source, "PreviewResponse", StringComparison.OrdinalIgnoreCase))
            {
                return componentData switch
                {
                    HttpData http => http.ResponseBody,
                    GraphQlData gql => gql.ResponseBody,
                    SqlData sql => SerializeQueryResult(sql.QueryResult),
                    DatasetData dataset => SerializeDatasetRowsWithPath(dataset.Rows, null),
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

        private string SerializeDatasetRowsWithPath(List<Dictionary<string, object>>? rows, string? jsonPath)
        {
            if (rows == null || rows.Count == 0)
            {
                return "[]";
            }

            // If no jsonPath specified or path is empty, return all rows
            if (string.IsNullOrWhiteSpace(jsonPath) || jsonPath == "$")
            {
                try
                {
                    return System.Text.Json.JsonSerializer.Serialize(rows);
                }
                catch
                {
                    return "[]";
                }
            }

            // Try to apply the JSON path
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(rows);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetPropertyByJsonPath(jsonPath, out var element))
                {
                    return element.GetRawText();
                }
            }
            catch
            {
                // If path doesn't match, return all rows
            }

            // Fallback to all rows
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

            var sourceText = ConvertVariableToText(sourceValue);
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

            // If JSON path didn't match, return the original source value
            return sourceValue;
        }

        private string ConvertVariableToText(object? value)
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

    public static class JsonElementExtensions
    {
        public static bool TryGetPropertyByJsonPath(this JsonElement element, string jsonPath, out JsonElement value)
        {
            value = default;
            var segments = jsonPath.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            var current = element;

            foreach (var segment in segments)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
                {
                    return false;
                }
                current = next;
            }

            value = current;
            return true;
        }
    }
}
