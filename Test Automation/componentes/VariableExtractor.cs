using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class VariableExtractor : Component
    {
        public VariableExtractor()
        {
            Name = "VariableExtractor";
        }

        public override Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            var pattern = Settings.TryGetValue("Pattern", out var configuredPattern)
                ? configuredPattern ?? string.Empty
                : string.Empty;
            var variableName = Settings.TryGetValue("VariableName", out var configuredVariableName)
                ? configuredVariableName ?? string.Empty
                : string.Empty;

            var data = new VariableExtractorData
            {
                Id = Id,
                ComponentName = Name,
                Pattern = pattern,
                VariableName = variableName
            };

            if (string.IsNullOrWhiteSpace(variableName))
            {
                data.Properties["warning"] = "VariableName is empty; extraction skipped.";
                return Task.FromResult<ComponentData>(data);
            }

            var extracted = ResolvePatternValue(pattern, context);
            data.ExtractedValue = extracted;
            data.Properties["resolvedValue"] = extracted;
            context.SetVariable(variableName.Trim(), extracted);
            return Task.FromResult<ComponentData>(data);
        }

        private static string ResolvePatternValue(string pattern, Test_Automation.Models.ExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return string.Empty;
            }

            var trimmed = pattern.Trim();
            if (context.HasVariable(trimmed))
            {
                return ConvertVariableToText(context.GetVariable(trimmed));
            }

            return Regex.Replace(pattern, "\\$\\{([^}]+)\\}", match =>
            {
                var key = match.Groups[1].Value.Trim();
                if (!context.HasVariable(key))
                {
                    return match.Value;
                }

                return ConvertVariableToText(context.GetVariable(key));
            });
        }

        private static string ConvertVariableToText(object? value)
        {
            if (value == null)
            {
                return string.Empty;
            }

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
