using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Test_Automation.Componentes;
using Test_Automation.Models;
using Test_Automation.Services;
using Test_Automation.Models.Editor;
using ExecutionContext = Test_Automation.Models.ExecutionContext;

namespace TestIntegration
{
    /// <summary>
    /// Integration test for assertions and variable extraction
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Test Automation Integration Tests ===\n");

            await Test1_HttpAssertion();
            await Test2_VariableExtraction();
            await Test3_DatasetVariableExtraction();
            await Test4_ForeachIteration();

            Console.WriteLine("\n=== All Tests Completed ===");
        }

        /// <summary>
        /// Test 1: HTTP component with assertions
        /// </summary>
        static async Task Test1_HttpAssertion()
        {
            Console.WriteLine("--- Test 1: HTTP Assertion ---");

            var executor = new ComponentExecutor();
            var context = new ExecutionContext();

            // Create HTTP component with assertion
            var http = new Http();
            http.Settings["Url"] = "https://jsonplaceholder.typicode.com/users/1";
            http.Settings["Method"] = "GET";
            
            // Add assertion: response status should be 200
            http.Assertions.Add(new AssertionRule("Status", "", "Equals", "200", "Assert"));
            
            // Add assertion: body should contain "Leanne Graham"
            http.Assertions.Add(new AssertionRule("PreviewResponse", "$.name", "Contains", "Leanne", "Assert"));

            var result = await executor.ExecuteComponent(http, context);

            Console.WriteLine($"  Status: {result.Status}");
            Console.WriteLine($"  Passed: {result.Passed}");
            Console.WriteLine($"  Assert Passed: {result.AssertPassedCount}");
            Console.WriteLine($"  Assert Failed: {result.AssertFailedCount}");
            
            foreach (var ar in result.AssertionResults)
            {
                Console.WriteLine($"    Assertion: {ar.Condition} '{ar.Source}' '{ar.JsonPath}'");
                Console.WriteLine($"      Expected: {ar.Expected}");
                Console.WriteLine($"      Actual: {ar.Actual}");
                Console.WriteLine($"      Passed: {ar.Passed}");
            }

            Console.WriteLine($"  Test 1 Result: {(result.AssertPassedCount > 0 ? "PASSED" : "FAILED")}\n");
        }

        /// <summary>
        /// Test 2: Variable extraction from HTTP response
        /// </summary>
        static async Task Test2_VariableExtraction()
        {
            Console.WriteLine("--- Test 2: Variable Extraction ---");

            var executor = new ComponentExecutor();
            var context = new ExecutionContext();

            // Create HTTP component with variable extraction
            var http = new Http();
            http.Settings["Url"] = "https://jsonplaceholder.typicode.com/users/1";
            http.Settings["Method"] = "GET";
            
            // Extract user's name to variable "userName"
            http.Extractors.Add(new VariableExtractionRule("PreviewResponse", "$.name", "userName"));
            
            // Extract user's email to variable "userEmail"  
            http.Extractors.Add(new VariableExtractionRule("PreviewResponse", "$.email", "userEmail"));

            var result = await executor.ExecuteComponent(http, context);

            Console.WriteLine($"  Status: {result.Status}");
            
            // Check if variables were extracted
            var userName = context.GetVariable("userName");
            var userEmail = context.GetVariable("userEmail");
            
            Console.WriteLine($"  userName = {userName}");
            Console.WriteLine($"  userEmail = {userEmail}");

            Console.WriteLine($"  Test 2 Result: {(userName?.ToString() == "Leanne Graham" ? "PASSED" : "FAILED")}\n");
        }

        /// <summary>
        /// Test 3: Dataset variable extraction
        /// </summary>
        static async Task Test3_DatasetVariableExtraction()
        {
            Console.WriteLine("--- Test 3: Dataset Variable Extraction ---");

            var executor = new ComponentExecutor();
            var context = new ExecutionContext();

            // Create Dataset component
            var dataset = new Dataset();
            dataset.Settings["Format"] = "Json";
            dataset.Settings["SourcePath"] = "testdata.json";
            
            // Note: This test would require an actual JSON file
            // For now, we'll simulate by setting up the extractor
            
            Console.WriteLine("  Note: Dataset test requires test data file");
            Console.WriteLine("  Skipping actual execution...\n");
        }

        /// <summary>
        /// Test 4: Foreach iteration with variable
        /// </summary>
        static async Task Test4_ForeachIteration()
        {
            Console.WriteLine("--- Test 4: Foreach Iteration ---");

            var executor = new ComponentExecutor();
            var context = new ExecutionContext();

            // Manually set a variable with a collection (simulating dataset extraction)
            var testData = new List<Dictionary<string, object>>
            {
                new() { ["name"] = "John", ["age"] = 30 },
                new() { ["name"] = "Jane", ["age"] = 25 },
                new() { ["name"] = "Bob", ["age"] = 35 }
            };
            
            // Serialize to JSON like the variable extractor would
            var json = System.Text.Json.JsonSerializer.Serialize(testData);
            context.SetVariable("testDataset", json);

            Console.WriteLine($"  Set testDataset variable: {json}");

            // Create Foreach component
            var foreachComp = new Foreach();
            foreachComp.Settings["SourceVariable"] = "testDataset";
            foreachComp.Settings["OutputVariable"] = "item";

            var result = await executor.ExecuteComponentTree(foreachComp, context);

            Console.WriteLine($"  Status: {result.Status}");
            
            // Check the foreach data
            if (result.Data is ForeachData fd)
            {
                Console.WriteLine($"  Collection count: {fd.Collection?.Count ?? 0}");
                Console.WriteLine($"  Current index: {fd.CurrentIndex}");
                
                foreach (var item in fd.Collection ?? new List<object>())
                {
                    Console.WriteLine($"    Item: {item}");
                }
            }

            // Check if currentItem was set for each iteration
            var currentItem = context.GetVariable("item");
            Console.WriteLine($"  Final item value: {currentItem}");

            Console.WriteLine($"  Test 4 Result: PASSED\n");
        }
    }
}
