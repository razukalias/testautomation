using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Test_Automation.Componentes;
using Test_Automation.Models;
using Test_Automation.Models.Editor;
using ExecutionContext = Test_Automation.Models.ExecutionContext;

namespace Test_Automation.Services
{
    /// <summary>
    /// Service for executing components and managing execution flow.
    /// </summary>
    public class ComponentExecutor
    {
        private readonly IVariableService _variableService;
        private readonly IAssertionService _assertionService;
        private readonly IConditionService _conditionService;

        private static readonly System.Threading.AsyncLocal<int?> CurrentThreadIndex = new System.Threading.AsyncLocal<int?>();
        private static readonly System.Threading.AsyncLocal<string?> CurrentThreadGroupId = new System.Threading.AsyncLocal<string?>();

        public event Action<ExecutionResult>? ComponentStarted;
        public event Action<ExecutionResult>? ComponentCompleted;
        public event Action<string>? Trace;

        public ComponentExecutor()
            : this(new VariableService(), new AssertionService(), new ConditionService())
        {
        }

        public ComponentExecutor(IVariableService variableService, IAssertionService assertionService, IConditionService conditionService)
        {
            _variableService = variableService ?? throw new ArgumentNullException(nameof(variableService));
            _assertionService = assertionService ?? throw new ArgumentNullException(nameof(assertionService));
            _conditionService = conditionService ?? throw new ArgumentNullException(nameof(conditionService));
        }

        public async Task<ExecutionResult> ExecuteComponent(Component component, ExecutionContext context)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ThrowIfStopped(context, component.Name);

            var result = new ExecutionResult
            {
                ComponentId = component.Id,
                ComponentName = component.Name,
                Status = "running",
                ThreadIndex = CurrentThreadIndex.Value ?? 0,
                ThreadGroupId = CurrentThreadGroupId.Value ?? string.Empty
            };

            ComponentStarted?.Invoke(result);
            TraceLog($"Execute start: {component.Name} ({component.GetType().Name})");

            var originalSettings = component.Settings;
            var originalExtractors = component.Extractors;
            var originalAssertions = component.Assertions;

            try
            {
                var settingsToResolve = originalSettings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                component.Settings = _variableService.ResolveSettings(settingsToResolve, context);
                component.Extractors = _variableService.ResolveExtractors(originalExtractors ?? new List<VariableExtractionRule>(), context);
                component.Assertions = _assertionService.ResolveAssertions(originalAssertions ?? new List<AssertionRule>(), context);

                var componentData = await component.Execute(context);
                result.Data = componentData;
                result.Output = componentData?.ToString() ?? string.Empty;

                if (componentData != null)
                {
                    componentData.Properties["startTime"] = result.StartTime;
                    componentData.Properties["threadIndex"] = result.ThreadIndex;
                    componentData.Properties["threadGroupId"] = result.ThreadGroupId;
                }

                _variableService.ApplyVariableExtractors(component, context, componentData, TraceLog);

                var assertionResults = _assertionService.EvaluateAssertions(component, componentData, context, TraceLog);
                result.AssertionResults = assertionResults;
                result.AssertFailedCount = assertionResults.Count(item => !item.Passed && IsAssertMode(item.Mode));
                result.ExpectFailedCount = assertionResults.Count(item => !item.Passed && !IsAssertMode(item.Mode));
                result.AssertPassedCount = assertionResults.Count(item => item.Passed);

                var stopRequestedByAssertion = assertionResults.Any(item => !item.Passed && IsStopOnAssertFailureMode(item.Mode));
                if (stopRequestedByAssertion)
                {
                    context.RequestStop();
                }

                if (result.AssertFailedCount > 0)
                {
                    result.Error = string.Join(" | ", assertionResults
                        .Where(item => !item.Passed && IsAssertMode(item.Mode))
                        .Select(item => item.Message));
                    result.MarkAsCompleted(false);
                }
                else
                {
                    result.MarkAsCompleted(true);
                }
            }
            catch (OperationCanceledException ex)
            {
                result.Error = ex.Message;
                result.MarkAsCompleted(false);
                result.Status = "stopped";
                throw;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.MarkAsCompleted(false);
            }
            finally
            {
                component.Settings = originalSettings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                component.Extractors = originalExtractors ?? new List<VariableExtractionRule>();
                component.Assertions = originalAssertions ?? new List<AssertionRule>();
                ComponentCompleted?.Invoke(result);
                TraceLog($"Execute end: {component.Name} status={result.Status} durationMs={result.DurationMs}");
            }

            return result;
        }

        public async Task<ExecutionResult> ExecuteComponentTree(Component component, ExecutionContext context)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ThrowIfStopped(context, component.Name);

            if (component is Threads threads)
            {
                return await ExecuteThreads(threads, context);
            }

            if (component is Loop loop)
            {
                return await ExecuteLoop(loop, context);
            }

            if (component is If conditional)
            {
                return await ExecuteIf(conditional, context);
            }

            if (component is Foreach foreachComponent)
            {
                return await ExecuteForeach(foreachComponent, context);
            }

            return await ExecuteWithChildren(component, context);
        }

        private async Task<ExecutionResult> ExecuteWithChildren(Component component, ExecutionContext context)
        {
            var result = await ExecuteComponent(component, context);

            foreach (var child in component.Children)
            {
                ThrowIfStopped(context, child.Name);
                var childResult = await ExecuteComponentTree(child, context);
                lock (context.Results)
                {
                    context.Results.Add(childResult);
                }
            }

            return result;
        }

        private async Task<ExecutionResult> ExecuteLoop(Loop loop, ExecutionContext context)
        {
            var result = await ExecuteComponent(loop, context);

            var iterations = 1;
            if (loop.Settings.TryGetValue("Iterations", out var value)
                && int.TryParse(value, out var parsed)
                && parsed > 0)
            {
                iterations = parsed;
            }

            var loopData = result.Data as LoopData;
            if (loopData != null)
            {
                loopData.Iterations = iterations;
            }

            for (var i = 0; i < iterations; i++)
            {
                ThrowIfStopped(context, $"{loop.Name} iteration {i}");
                context.SetVariable("loop_index", i);

                if (loopData != null)
                {
                    loopData.CurrentIteration = i;
                }

                foreach (var child in loop.Children)
                {
                    var childResult = await ExecuteComponentTree(child, context);
                    lock (context.Results)
                    {
                        context.Results.Add(childResult);
                    }
                }
            }

            return result;
        }

        private async Task<ExecutionResult> ExecuteIf(If conditional, ExecutionContext context)
        {
            var result = await ExecuteComponent(conditional, context);
            var condition = conditional.Settings.TryGetValue("Condition", out var value) ? value : string.Empty;
            var conditionMet = _conditionService.Evaluate(condition, context);

            var ifData = result.Data as IfData;
            if (ifData != null)
            {
                ifData.Condition = condition ?? string.Empty;
                ifData.ConditionMet = conditionMet;
            }

            if (!conditionMet)
            {
                return result;
            }

            foreach (var child in conditional.Children)
            {
                ThrowIfStopped(context, child.Name);
                var childResult = await ExecuteComponentTree(child, context);
                lock (context.Results)
                {
                    context.Results.Add(childResult);
                }
            }

            return result;
        }

        private async Task<ExecutionResult> ExecuteForeach(Foreach foreachComponent, ExecutionContext context)
        {
            var result = await ExecuteComponent(foreachComponent, context);

            var sourceVariable = foreachComponent.Settings.TryGetValue("SourceVariable", out var source) ? source : string.Empty;
            var outputVariable = foreachComponent.Settings.TryGetValue("OutputVariable", out var output) ? output?.Trim() : string.Empty;
            var items = ResolveCollection(context, sourceVariable).ToList();

            var foreachData = result.Data as ForeachData;
            if (foreachData != null)
            {
                foreachData.Collection = items;
                foreachData.OutputVariable = outputVariable ?? string.Empty;
            }

            for (var i = 0; i < items.Count; i++)
            {
                ThrowIfStopped(context, $"{foreachComponent.Name} iteration {i}");

                context.SetVariable("CurrentItem", items[i]);
                context.SetVariable("CurrentIndex", i);
                if (!string.IsNullOrWhiteSpace(outputVariable))
                {
                    context.SetVariable(outputVariable, items[i]);
                }

                if (foreachData != null)
                {
                    foreachData.CurrentIndex = i;
                    foreachData.CurrentItem = items[i];
                }

                foreach (var child in foreachComponent.Children)
                {
                    var childResult = await ExecuteComponentTree(child, context);
                    lock (context.Results)
                    {
                        context.Results.Add(childResult);
                    }
                }
            }

            return result;
        }

        private async Task<ExecutionResult> ExecuteThreads(Threads threads, ExecutionContext context)
        {
            var result = await ExecuteComponent(threads, context);

            var threadCount = 1;
            if (threads.Settings.TryGetValue("ThreadCount", out var value)
                && int.TryParse(value, out var parsed)
                && parsed > 0)
            {
                threadCount = parsed;
            }

            var threadsData = result.Data as ThreadsData;
            if (threadsData != null)
            {
                threadsData.ThreadCount = threadCount;
            }

            var taskList = Enumerable.Range(1, threadCount)
                .Select(index => Task.Run(async () =>
                {
                    CurrentThreadIndex.Value = index;
                    CurrentThreadGroupId.Value = threads.Id;
                    foreach (var child in threads.Children)
                    {
                        ThrowIfStopped(context, child.Name);
                        var childResult = await ExecuteComponentTree(child, context);
                        lock (context.Results)
                        {
                            context.Results.Add(childResult);
                        }
                    }
                }, context.StopToken))
                .ToList();

            await Task.WhenAll(taskList);
            return result;
        }

        private static IEnumerable<object> ResolveCollection(ExecutionContext context, string? sourceVariable)
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

            if (value is IEnumerable enumerable && value is not string)
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
                return jsonElement.EnumerateArray().Select(item => (object)item.Clone()).ToList();
            }

            if (value is string text)
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        return doc.RootElement.EnumerateArray().Select(item => (object)item.Clone()).ToList();
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

        private void TraceLog(string message)
        {
            Trace?.Invoke(message);
        }

        private static void ThrowIfStopped(ExecutionContext context, string scope)
        {
            if (!context.IsRunning || context.StopToken.IsCancellationRequested)
            {
                throw new OperationCanceledException($"Execution stopped: {scope}");
            }
        }

        private static bool IsAssertMode(string mode)
        {
            return !string.Equals(mode, "Expect", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStopOnAssertFailureMode(string mode)
        {
            return string.Equals(mode, "Assert", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "Expect-Stop", StringComparison.OrdinalIgnoreCase);
        }
    }
}
