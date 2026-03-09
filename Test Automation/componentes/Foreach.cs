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
            var data = new ForeachData
            {
                Id = this.Id,
                ComponentName = this.Name,
                Collection = new List<object>()
            };
            return Task.FromResult<ComponentData>(data);
        }
    }
}
