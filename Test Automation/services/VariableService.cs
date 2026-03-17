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

                var sourceValue = GetSourceValue(extractor.Source, componentData);
                var extractedValue = ExtractValue(sourceValue, extractor.JsonPath);

                trace($"Extractor for '{extractor.VariableName}': source='{extractor.Source}', jsonPath='{extractor.JsonPath}', extracted='{extractedValue}'.");
                context.SetVariable(extractor.VariableName, extractedValue ?? string.Empty);
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

            return null;
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
