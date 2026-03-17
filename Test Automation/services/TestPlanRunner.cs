using System;
using System.Linq;
using System.Threading.Tasks;
using Test_Automation.Componentes;
using Test_Automation.Models;

namespace Test_Automation.Services
{
    /// <summary>
    /// Service for running a complete test plan
    /// </summary>
    public class TestPlanRunner
    {
        private readonly ComponentExecutor _executor;

        public TestPlanRunner()
        {
            var variableService = new VariableService();
            var assertionService = new AssertionService();
            var conditionService = new ConditionService();
            _executor = new ComponentExecutor(variableService, assertionService, conditionService);
        }

        public TestPlanRunner(ComponentExecutor executor)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        public async Task<ExecutionSummary> RunTestPlan(TestPlan testPlan)
        {
            if (testPlan == null)
                throw new ArgumentNullException(nameof(testPlan));

            var context = new Test_Automation.Models.ExecutionContext();
            var summary = new ExecutionSummary
            {
                ExecutionId = context.ExecutionId,
                StartTime = context.StartTime
            };

            try
            {
                // Execute the test plan and its children (Threads)
                await _executor.ExecuteComponentTree(testPlan, context);

                // Calculate summary
                summary.TotalComponents = context.Results.Count;
                summary.PassedComponents = context.Results.Count(r => r.Passed);
                summary.FailedComponents = context.Results.Count(r => !r.Passed);
                summary.EndTime = DateTime.UtcNow;
                summary.TotalDurationMs = (long)(summary.EndTime - summary.StartTime).TotalMilliseconds;

                context.Status = summary.Status;
                context.IsRunning = false;
                context.EndTime = summary.EndTime;
            }
            catch (OperationCanceledException)
            {
                context.Status = "stopped";
                context.IsRunning = false;
                context.EndTime = DateTime.UtcNow;
                summary.EndTime = context.EndTime.Value;
                summary.TotalDurationMs = (long)(summary.EndTime - summary.StartTime).TotalMilliseconds;
                summary.TotalComponents = context.Results.Count;
                summary.PassedComponents = context.Results.Count(r => r.Passed);
                summary.FailedComponents = context.Results.Count(r => !r.Passed);
            }
            catch (Exception)
            {
                context.Status = "failed";
                context.IsRunning = false;
                context.EndTime = DateTime.UtcNow;
                summary.EndTime = context.EndTime.Value;
                summary.TotalDurationMs = (long)(summary.EndTime - summary.StartTime).TotalMilliseconds;
            }

            return summary;
        }

        public async Task<ExecutionSummary> RunTestPlanWithContext(TestPlan testPlan, Test_Automation.Models.ExecutionContext context)
        {
            if (testPlan == null)
                throw new ArgumentNullException(nameof(testPlan));

            var summary = new ExecutionSummary
            {
                ExecutionId = context.ExecutionId,
                StartTime = context.StartTime
            };

            try
            {
                await _executor.ExecuteComponentTree(testPlan, context);

                summary.TotalComponents = context.Results.Count;
                summary.PassedComponents = context.Results.Count(r => r.Passed);
                summary.FailedComponents = context.Results.Count(r => !r.Passed);
                summary.EndTime = DateTime.UtcNow;
                summary.TotalDurationMs = (long)(summary.EndTime - summary.StartTime).TotalMilliseconds;

                context.Status = summary.Status;
                context.IsRunning = false;
                context.EndTime = summary.EndTime;
            }
            catch (OperationCanceledException)
            {
                context.Status = "stopped";
                context.IsRunning = false;
                context.EndTime = DateTime.UtcNow;
                summary.EndTime = context.EndTime.Value;
                summary.TotalDurationMs = (long)(summary.EndTime - summary.StartTime).TotalMilliseconds;
                summary.TotalComponents = context.Results.Count;
                summary.PassedComponents = context.Results.Count(r => r.Passed);
                summary.FailedComponents = context.Results.Count(r => !r.Passed);
            }
            catch (Exception)
            {
                context.Status = "failed";
                context.IsRunning = false;
                context.EndTime = DateTime.UtcNow;
                summary.EndTime = context.EndTime.Value;
                summary.TotalDurationMs = (long)(summary.EndTime - summary.StartTime).TotalMilliseconds;
            }

            return summary;
        }
    }
}
