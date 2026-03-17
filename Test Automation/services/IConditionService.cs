using Test_Automation.Models;
using ExecutionContext = Test_Automation.Models.ExecutionContext;

namespace Test_Automation.Services
{
    public interface IConditionService
    {
        bool Evaluate(string? condition, ExecutionContext context);
    }
}
