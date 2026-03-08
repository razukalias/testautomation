using System;
using Test_Automation.Componentes;

namespace Test_Automation.Factories
{
    /// <summary>
    /// Factory for creating component instances based on type name
    /// </summary>
    public class ComponentFactory
    {
        public static Component CreateComponent(string componentType)
        {
            return componentType?.ToLower() switch
            {
                "http" => new Http(),
                "graphql" => new GraphQl(),
                "timer" => new Test_Automation.Componentes.Timer(),
                "sql" => new Sql(),
                "dataset" => new Dataset(),
                "if" => new If(),
                "loop" => new Loop(),
                "foreach" => new Foreach(),
                "threads" => new Threads(),
                "config" => new Config(),
                "testplan" => new TestPlan(),
                "assert" => new Assert(),
                "variableextractor" => new VariableExtractor(),
                "script" => new Script(),
                _ => throw new ArgumentException($"Unknown component type: {componentType}")
            };
        }
    }
}
