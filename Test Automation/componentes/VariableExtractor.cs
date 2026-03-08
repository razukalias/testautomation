using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class VariableExtractor : Component
    {
        public VariableExtractor()
        {
            Name = "VariableExtractor";
        }

        public override Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            // VariableExtractor component logic
            var data = new VariableExtractorData { Id = this.Id, ComponentName = this.Name };
            return Task.FromResult<ComponentData>(data);
        }
    }
}
