using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class Loop : Component
    {
        public Loop()
        {
            Name = "Loop";
        }

        public override Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            // Loop component logic
            var data = new LoopData { Id = this.Id, ComponentName = this.Name };
            return Task.FromResult<ComponentData>(data);
        }
    }
}
