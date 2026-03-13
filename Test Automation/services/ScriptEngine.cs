using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Linq;
using Test_Automation.Models;

namespace Test_Automation.Services
{
    public sealed class ScriptExecutionOutcome
    {
        public bool Success { get; init; }
        public object? Result { get; init; }
        public string Error { get; init; } = string.Empty;
    }

    public static class ScriptEngine
    {
        private static readonly ScriptOptions DefaultOptions = ScriptOptions.Default
            .WithImports("System", "System.Linq", "System.Collections.Generic", "System.Text.Json")
            .WithReferences(
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(System.Text.Json.JsonSerializer).Assembly,
                typeof(ScriptGlobals).Assembly);

        public static async Task<ScriptExecutionOutcome> ExecuteAsync(
            string language,
            string code,
            Test_Automation.Models.ExecutionContext context,
            string? actual = null)
        {
            if (!string.Equals(language, "CSharp", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(language, "csharp", StringComparison.OrdinalIgnoreCase))
            {
                return new ScriptExecutionOutcome
                {
                    Success = false,
                    Error = $"Unsupported script language: {language}"
                };
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return new ScriptExecutionOutcome
                {
                    Success = true,
                    Result = null
                };
            }

            try
            {
                var globals = new ScriptGlobals(context, actual);
                var result = await CSharpScript.EvaluateAsync<object?>(code, DefaultOptions, globals);
                return new ScriptExecutionOutcome
                {
                    Success = true,
                    Result = result
                };
            }
            catch (Exception ex)
            {
                return new ScriptExecutionOutcome
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }
    }

    public sealed class ScriptGlobals
    {
        public IReadOnlyDictionary<string, object> Vars { get; }
        public string Actual { get; }
        public double? ActualNumber { get; }
        public string actual => Actual;
        public double? actualNumber => ActualNumber;

        public ScriptGlobals(Test_Automation.Models.ExecutionContext context, string? actual)
        {
            Vars = context?.Variables as IReadOnlyDictionary<string, object>
                ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Actual = actual ?? string.Empty;
            ActualNumber = double.TryParse(Actual, out var numeric) ? numeric : null;
        }

        public object? Var(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return Vars.TryGetValue(name, out var value) ? value : null;
        }

        public string? VarText(string name)
        {
            return Var(name)?.ToString();
        }
    }
}
