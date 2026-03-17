using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Test_Automation.Componentes;
using Test_Automation.Models;
using Test_Automation.Services;
using ExecutionContext = Test_Automation.Models.ExecutionContext;
using ComponentTimer = Test_Automation.Componentes.Timer;

internal static class Program
{
	private static async Task<int> Main()
	{
		var context = new ExecutionContext();
		context.SetVariable("shouldRun", "true");
		context.SetVariable("items", new List<object> { "alpha", "beta", "gamma" });

		var root = new TestPlan();

		var ifNode = new If();
		ifNode.Settings["Condition"] = "${shouldRun} == true";

		var loopNode = new Loop();
		loopNode.Settings["Iterations"] = "2";

		var loopExtractor = new VariableExtractor();
		loopExtractor.Settings["Pattern"] = "${loop_index}";
		loopExtractor.Settings["VariableName"] = "lastLoopIndex";
		loopNode.AddChild(loopExtractor);

		ifNode.AddChild(loopNode);
		root.AddChild(ifNode);

		var foreachNode = new Foreach();
		foreachNode.Settings["SourceVariable"] = "items";
		foreachNode.Settings["OutputVariable"] = "currentItem";

		var foreachExtractor = new VariableExtractor();
		foreachExtractor.Settings["Pattern"] = "${CurrentItem}";
		foreachExtractor.Settings["VariableName"] = "lastItem";
		foreachNode.AddChild(foreachExtractor);

		root.AddChild(foreachNode);

		var threadsNode = new Threads();
		threadsNode.Settings["ThreadCount"] = "2";

		var timerNode = new ComponentTimer();
		timerNode.Settings["DelayMs"] = "5";
		threadsNode.AddChild(timerNode);

		root.AddChild(threadsNode);

		var executor = new ComponentExecutor(new VariableService(), new AssertionService(), new ConditionService());
		var rootResult = await executor.ExecuteComponentTree(root, context);

		context.Results.Add(rootResult);

		var lastLoopIndex = context.GetVariable("lastLoopIndex")?.ToString() ?? string.Empty;
		var lastItem = context.GetVariable("lastItem")?.ToString() ?? string.Empty;
		var totalResults = context.Results.Count;
		var failedResults = context.Results.Count(item => !item.Passed);

		Console.WriteLine($"Results={totalResults}");
		Console.WriteLine($"Failed={failedResults}");
		Console.WriteLine($"lastLoopIndex={lastLoopIndex}");
		Console.WriteLine($"lastItem={lastItem}");

		var ok = failedResults == 0
			&& (lastLoopIndex == "1")
			&& string.Equals(lastItem, "gamma", StringComparison.Ordinal);

		Console.WriteLine(ok ? "SMOKE_PASS" : "SMOKE_FAIL");
		return ok ? 0 : 2;
	}
}
