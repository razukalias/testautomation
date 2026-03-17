using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Test_Automation.Models;
using ExecutionContext = Test_Automation.Models.ExecutionContext;

namespace Test_Automation.Services
{
    public class ConditionService : IConditionService
    {
        private static readonly Regex ConditionRegex = new Regex(
            @"^\s*(?<left>.+?)\s*(?<op>==|!=|>=|<=|>|<)\s*(?<right>.+?)\s*$",
            RegexOptions.Compiled);

        public bool Evaluate(string? condition, ExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                return false;
            }

            var resolvedCondition = ResolveText(condition, context);
            var match = ConditionRegex.Match(resolvedCondition);

            if (match.Success)
            {
                var left = match.Groups["left"].Value.Trim();
                var op = match.Groups["op"].Value;
                var right = match.Groups["right"].Value.Trim();

                // Handle numeric comparison
                if (decimal.TryParse(left, NumberStyles.Any, CultureInfo.InvariantCulture, out var leftNum) &&
                    decimal.TryParse(right, NumberStyles.Any, CultureInfo.InvariantCulture, out var rightNum))
                {
                    return CompareNumbers(leftNum, op, rightNum);
                }

                // Handle string comparison (unquote strings)
                var leftStr = Unquote(left);
                var rightStr = Unquote(right);
                return CompareStrings(leftStr, op, rightStr);
            }

            // Fallback for simple boolean check
            if (bool.TryParse(resolvedCondition, out var result))
            {
                return result;
            }

            return !string.IsNullOrWhiteSpace(resolvedCondition);
        }

        private bool CompareNumbers(decimal left, string op, decimal right)
        {
            return op switch
            {
                "==" => left == right,
                "!=" => left != right,
                ">" => left > right,
                ">=" => left >= right,
                "<" => left < right,
                "<=" => left <= right,
                _ => false
            };
        }

        private bool CompareStrings(string left, string op, string right)
        {
            var comparison = StringComparison.Ordinal;
            return op switch
            {
                "==" => string.Equals(left, right, comparison),
                "!=" => !string.Equals(left, right, comparison),
                _ => false // Other operators are not valid for strings in this context
            };
        }

        private string Unquote(string value)
        {
            if (value.Length > 1 && value.StartsWith("\"") && value.EndsWith("\""))
            {
                return value.Substring(1, value.Length - 2);
            }
            return value;
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
                return ConvertVariableToText(context.GetVariable(key));
            });
        }

        private string ConvertVariableToText(object? value)
        {
            if (value == null) return string.Empty;
            if (value is System.Text.Json.JsonElement json)
            {
                return json.ValueKind == System.Text.Json.JsonValueKind.String
                    ? json.GetString() ?? string.Empty
                    : json.GetRawText();
            }
            return value.ToString() ?? string.Empty;
        }
    }
}
