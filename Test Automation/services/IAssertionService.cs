using System.Collections.Generic;
using Test_Automation.Models;
using ExecutionContext = Test_Automation.Models.ExecutionContext;

namespace Test_Automation.Services
{
    public interface IAssertionService
    {
        List<Models.Editor.AssertionRule> ResolveAssertions(List<Models.Editor.AssertionRule> assertions, ExecutionContext context);
        List<AssertionEvaluationResult> EvaluateAssertions(Componentes.Component component, ComponentData? componentData, ExecutionContext context, System.Action<string> trace);
    }
}
