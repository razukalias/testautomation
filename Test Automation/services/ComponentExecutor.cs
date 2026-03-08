using System;
using System.Collections.Generic;
using System.Linq;
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
        public async Task<ExecutionResult> ExecuteComponent(Component component, Test_Automation.Models.ExecutionContext context)
        {
            if (component == null || context == null)
                throw new ArgumentNullException(nameof(component));

            var result = new ExecutionResult
            {
                ComponentId = component.Id,
                ComponentName = component.Name,
                Status = "running"
            };

            try
            {
                // Execute the component
                var componentData = await component.Execute(context);
                result.Data = componentData;
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

        public async Task<ExecutionResult> ExecuteComponentTree(Component component, Test_Automation.Models.ExecutionContext context)
        {
            var result = await ExecuteComponent(component, context);

            // If component has children, execute them sequentially
            if (component.Children.Count > 0)
            {
                var childResults = new List<ExecutionResult>();
                foreach (var child in component.Children)
                {
                    var childResult = await ExecuteComponentTree(child, context);
                    childResults.Add(childResult);
                    context.Results.Add(childResult);
                }
            }

            return result;
        }

        public async Task<ExecutionResult> ExecuteThreadGroup(Threads threadComponent, Test_Automation.Models.ExecutionContext context, int threadCount = 1)
        {
            var result = new ExecutionResult
            {
                ComponentId = threadComponent.Id,
                ComponentName = threadComponent.Name,
                Status = "running"
            };

            try
            {
                if (threadCount <= 1)
                {
                    // Execute sequentially
                    foreach (var child in threadComponent.Children)
                    {
                        var childResult = await ExecuteComponentTree(child, context);
                        context.Results.Add(childResult);
                    }
                }
                else
                {
                    // Execute in parallel
                    var tasks = threadComponent.Children.Select(child => ExecuteComponentTree(child, context)).ToList();
                    var results = await Task.WhenAll(tasks);
                    foreach (var r in results)
                    {
                        context.Results.Add(r);
                    }
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
    }
}
