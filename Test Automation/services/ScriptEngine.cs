using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
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
        public int? Line { get; init; }
        public int? Column { get; init; }
        public List<ScriptDiagnosticInfo> Diagnostics { get; init; } = new();
    }

    public sealed class ScriptDiagnosticInfo
    {
        public string Severity { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public int? Line { get; init; }
        public int? Column { get; init; }
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
            string? actual = null,
            Action<string>? trace = null)
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
                var globals = new ScriptGlobals(context, actual, trace);
                var result = await CSharpScript.EvaluateAsync<object?>(code, DefaultOptions, globals);
                return new ScriptExecutionOutcome
                {
                    Success = true,
                    Result = result
                };
            }
            catch (CompilationErrorException cex)
            {
                var diagnostics = cex.Diagnostics
                    .Where(item => item.Severity == DiagnosticSeverity.Error)
                    .Select(MapDiagnostic)
                    .ToList();
                var first = diagnostics.FirstOrDefault();
                return new ScriptExecutionOutcome
                {
                    Success = false,
                    Error = string.Join("\n", diagnostics.Select(item => FormatDiagnostic(item))),
                    Line = first?.Line,
                    Column = first?.Column,
                    Diagnostics = diagnostics
                };
            }
            catch (Exception ex)
            {
                var line = TryGetLineFromStackTrace(ex.StackTrace);
                return new ScriptExecutionOutcome
                {
                    Success = false,
                    Error = ex.Message,
                    Line = line,
                    Column = line.HasValue ? 1 : null
                };
            }
        }

        public static ScriptExecutionOutcome Validate(string language, string code)
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
                    Success = false,
                    Error = "Script is empty."
                };
            }

            try
            {
                var script = CSharpScript.Create<object?>(code, DefaultOptions, typeof(ScriptGlobals));
                var diagnostics = script.Compile();
                var errors = diagnostics
                    .Where(item => item.Severity == DiagnosticSeverity.Error)
                    .Select(MapDiagnostic)
                    .ToList();

                if (errors.Count > 0)
                {
                    var first = errors[0];
                    return new ScriptExecutionOutcome
                    {
                        Success = false,
                        Error = string.Join("\n", errors.Select(FormatDiagnostic)),
                        Line = first.Line,
                        Column = first.Column,
                        Diagnostics = errors
                    };
                }

                return new ScriptExecutionOutcome
                {
                    Success = true
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

        private static ScriptDiagnosticInfo MapDiagnostic(Diagnostic diagnostic)
        {
            int? line = null;
            int? column = null;
            if (diagnostic.Location != Location.None && diagnostic.Location.IsInSource)
            {
                var span = diagnostic.Location.GetLineSpan();
                line = span.StartLinePosition.Line + 1;
                column = span.StartLinePosition.Character + 1;
            }

            return new ScriptDiagnosticInfo
            {
                Severity = diagnostic.Severity.ToString(),
                Code = diagnostic.Id,
                Message = diagnostic.GetMessage(),
                Line = line,
                Column = column
            };
        }

        private static string FormatDiagnostic(ScriptDiagnosticInfo diagnostic)
        {
            var location = diagnostic.Line.HasValue
                ? $"(line {diagnostic.Line}, col {diagnostic.Column ?? 1}) "
                : string.Empty;
            return $"{diagnostic.Severity} {diagnostic.Code} {location}{diagnostic.Message}";
        }

        private static int? TryGetLineFromStackTrace(string? stackTrace)
        {
            if (string.IsNullOrWhiteSpace(stackTrace))
            {
                return null;
            }

            var match = Regex.Match(stackTrace, @"line\s+(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return int.TryParse(match.Groups[1].Value, out var parsed) ? parsed : null;
        }
    }

    public sealed class ScriptGlobals
    {
        private readonly Test_Automation.Models.ExecutionContext _context;
        private readonly Action<string>? _trace;
        public IReadOnlyDictionary<string, object> Vars { get; }
        public string Actual { get; }
        public double? ActualNumber { get; }
        public string actual => Actual;
        public double? actualNumber => ActualNumber;

        public ScriptGlobals(Test_Automation.Models.ExecutionContext context, string? actual, Action<string>? trace = null)
        {
            _context = context ?? new Test_Automation.Models.ExecutionContext();
            _trace = trace;
            Vars = _context.Variables as IReadOnlyDictionary<string, object>
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

        public void SetVar(string name, object? value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            _context.SetVariable(name, value ?? string.Empty);
        }

        public void log(string message)
        {
            _trace?.Invoke(message ?? string.Empty);
        }

        public void Log(string message)
        {
            log(message);
        }
    }
}
