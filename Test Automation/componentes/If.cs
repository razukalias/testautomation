using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class If : Component
    {
        public If()
        {
            Name = "If";
        }

        public override Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            var data = new IfData
            {
                Id = this.Id,
                ComponentName = this.Name,
                Condition = Settings.TryGetValue("Condition", out var value) ? value : string.Empty
            };
            return Task.FromResult<ComponentData>(data);
        }
    }
}
