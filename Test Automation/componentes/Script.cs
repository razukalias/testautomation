using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class Script : Component
    {
        public Script()
        {
            Name = "Script";
        }

        public override Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            // Script component logic
            var data = new ScriptData { Id = this.Id, ComponentName = this.Name };
            return Task.FromResult<ComponentData>(data);
        }
    }
}
