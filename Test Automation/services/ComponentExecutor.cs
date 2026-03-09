using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Test_Automation.Componentes;
using Test_Automation.Models;

namespace Test_Automation.Services
{
    /// <summary>
    /// Service for executing components and managing the execution flow
    /// </summary>
    public class ComponentExecutor
    {
        private static readonly System.Threading.AsyncLocal<int?> CurrentThreadIndex = new System.Threading.AsyncLocal<int?>();
        private static readonly System.Threading.AsyncLocal<string?> CurrentThreadGroupId = new System.Threading.AsyncLocal<string?>();

        public async Task<ExecutionResult> ExecuteComponent(Component component, Test_Automation.Models.ExecutionContext context)
        {
            if (component == null || context == null)
                throw new ArgumentNullException(nameof(component));

            var result = new ExecutionResult
            {
                ComponentId = component.Id,
                ComponentName = component.Name,
                Status = "running",
                ThreadIndex = CurrentThreadIndex.Value ?? 0,
                ThreadGroupId = CurrentThreadGroupId.Value ?? string.Empty
            };

            try
            {
                // Execute the component
                var componentData = await component.Execute(context);
                result.Data = componentData;
                ApplyVariableExtractors(component, context, componentData);
                result.MarkAsCompleted(true);
                result.Output = componentData?.ToString();
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.MarkAsCompleted(false);
            }

            return result;
        }

        private static void ApplyVariableExtractors(Component component, Test_Automation.Models.ExecutionContext context, ComponentData? componentData)
        {
            if (component.Extractors == null || component.Extractors.Count == 0)
            {
                return;
            }

            foreach (var extractor in component.Extractors)
            {
                if (string.IsNullOrWhiteSpace(extractor.VariableName) || string.IsNullOrWhiteSpace(extractor.Source))
                {
                    continue;
                }

                var sourceValue = ResolveSourceValue(component, componentData, extractor.Source);
                if (string.IsNullOrEmpty(sourceValue))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(extractor.JsonPath))
                {
                    context.SetVariable(extractor.VariableName, sourceValue);
                    continue;
                }

                var jsonPath = extractor.JsonPath.Trim();
                if (string.Equals(jsonPath, "$", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(jsonPath, "$.", StringComparison.OrdinalIgnoreCase))
                {
                    context.SetVariable(extractor.VariableName, sourceValue);
                    continue;
                }

                if (TryExtractJsonPath(sourceValue, extractor.JsonPath, out var extracted))
                {
                    context.SetVariable(extractor.VariableName, extracted);
                }
            }
        }

        private static string? ResolveSourceValue(Component component, ComponentData? componentData, string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return null;
            }

            if (string.Equals(source, "PreviewResponse", StringComparison.OrdinalIgnoreCase))
            {
                return BuildPreviewResponse(componentData);
            }

            if (component.Settings != null && component.Settings.TryGetValue(source, out var settingValue))
            {
                return settingValue;
            }

            if (componentData == null)
            {
                return null;
            }

            if (string.Equals(source, "ComponentData", StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Serialize(componentData);
            }

            if (source.StartsWith("ComponentData.", StringComparison.OrdinalIgnoreCase))
            {
                var path = source.Substring("ComponentData.".Length);
                var jsonPath = path.StartsWith("$") ? path : "$." + path;
                var json = JsonSerializer.Serialize(componentData);
                if (TryExtractJsonPath(json, jsonPath, out var extracted))
                {
                    return extracted;
                }
            }

            return null;
        }

        private static string? BuildPreviewResponse(ComponentData? componentData)
        {
            if (componentData == null)
            {
                return null;
            }

            if (componentData is HttpData httpData)
            {
                return JsonSerializer.Serialize(new
                {
                    status = httpData.ResponseStatus,
                    body = TryParseJson(httpData.ResponseBody),
                    headers = httpData.Headers
                });
            }

            if (componentData is GraphQlData graphQlData)
            {
                return JsonSerializer.Serialize(new
                {
                    status = graphQlData.ResponseStatus,
                    body = TryParseJson(graphQlData.ResponseBody),
                    headers = graphQlData.Headers
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

        private static bool TryExtractJsonPath(string json, string path, out string extracted)
        {
            extracted = string.Empty;
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var element = doc.RootElement;
                var normalized = path.Trim();
                if (normalized.StartsWith("$"))
                {
                    normalized = normalized.TrimStart('$');
                    if (normalized.StartsWith("."))
                    {
                        normalized = normalized.Substring(1);
                    }
                }

                var segments = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
                foreach (var segment in segments)
                {
                    if (!TryResolveSegment(ref element, segment))
                    {
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
            catch
            {
                return false;
            }
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

            if (!element.TryGetProperty(token, out var next))
            {
                return false;
            }

            element = next;
            return true;
        }

        public async Task<ExecutionResult> ExecuteComponentTree(Component component, Test_Automation.Models.ExecutionContext context)
        {
            if (component is Threads threadsComponent)
            {
                var threadCount = 1;
                if (threadsComponent.Settings.TryGetValue("ThreadCount", out var countValue)
                    && int.TryParse(countValue, out var parsed)
                    && parsed > 0)
                {
                    threadCount = parsed;
                }

                return await ExecuteThreadGroup(threadsComponent, context, threadCount);
            }

            if (component is Loop loopComponent)
            {
                return await ExecuteLoop(loopComponent, context);
            }

            if (component is If ifComponent)
            {
                return await ExecuteIf(ifComponent, context);
            }

            if (component is Foreach foreachComponent)
            {
                return await ExecuteForeach(foreachComponent, context);
            }

            var result = await ExecuteComponent(component, context);

            // If component has children, execute them sequentially
            if (component.Children.Count > 0)
            {
                var childResults = new List<ExecutionResult>();
                foreach (var child in component.Children)
                {
                    var childResult = await ExecuteComponentTree(child, context);
                    childResults.Add(childResult);
                    lock (context.Results)
                    {
                        context.Results.Add(childResult);
                    }
                }
            }

            return result;
        }

        private async Task<ExecutionResult> ExecuteLoop(Loop loopComponent, Test_Automation.Models.ExecutionContext context)
        {
            var result = await ExecuteComponent(loopComponent, context);

            var iterations = 1;
            if (loopComponent.Settings.TryGetValue("Iterations", out var value)
                && int.TryParse(value, out var parsed)
                && parsed > 0)
            {
                iterations = parsed;
            }

            var previousIndex = context.GetVariable("LoopIndex");
            for (var i = 0; i < iterations; i++)
            {
                context.SetVariable("LoopIndex", i);
                foreach (var child in loopComponent.Children)
                {
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
            var result = await ExecuteComponent(ifComponent, context);
            var condition = ifComponent.Settings.TryGetValue("Condition", out var value) ? value : string.Empty;
            var conditionMet = EvaluateCondition(condition, context);

            if (result.Data is IfData ifData)
            {
                ifData.Condition = condition;
                ifData.ConditionMet = conditionMet;
            }

            if (conditionMet)
            {
                foreach (var child in ifComponent.Children)
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

        private async Task<ExecutionResult> ExecuteForeach(Foreach foreachComponent, Test_Automation.Models.ExecutionContext context)
        {
            var result = await ExecuteComponent(foreachComponent, context);
            var sourceVariable = foreachComponent.Settings.TryGetValue("SourceVariable", out var value)
                ? value
                : string.Empty;

            var previousItem = context.GetVariable("CurrentItem");
            var previousIndex = context.GetVariable("CurrentIndex");

            var collection = ResolveCollection(context, sourceVariable);
            var index = 0;
            foreach (var item in collection)
            {
                context.SetVariable("CurrentItem", item);
                context.SetVariable("CurrentIndex", index);
                foreach (var child in foreachComponent.Children)
                {
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
            return System.Text.RegularExpressions.Regex.Replace(condition, "\\$\\{([^}]+)\\}", match =>
            {
                var key = match.Groups[1].Value;
                var value = context.GetVariable(key);
                return value?.ToString() ?? string.Empty;
            });
        }

        public async Task<ExecutionResult> ExecuteThreadGroup(Threads threadComponent, Test_Automation.Models.ExecutionContext context, int threadCount = 1)
        {
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
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.MarkAsCompleted(false);
            }

            return result;
        }

        private async Task ExecuteThreadChildren(Threads threadComponent, Test_Automation.Models.ExecutionContext context, int threadIndex)
        {
            CurrentThreadIndex.Value = threadIndex;
            CurrentThreadGroupId.Value = threadComponent.Id;

            foreach (var child in threadComponent.Children)
            {
                var childResult = await ExecuteComponentTree(child, context);
                lock (context.Results)
                {
                    context.Results.Add(childResult);
                }
            }
        }
    }
}
