using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class Foreach : Component
    {
        public Foreach()
        {
            Name = "Foreach";
        }

        public override Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            var outputVariable = Settings != null && Settings.TryGetValue("OutputVariable", out var configuredOutput)
                ? configuredOutput ?? string.Empty
                : string.Empty;

            var data = new ForeachData
            {
                Id = this.Id,
                ComponentName = this.Name,
                Collection = new List<object>(),
                CurrentItem = null,
                OutputVariable = outputVariable?.Trim() ?? string.Empty,
                ChildComponents = this.Children
                    .Select(child => child.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList()
            };
            return Task.FromResult<ComponentData>(data);
        }
    }
}
