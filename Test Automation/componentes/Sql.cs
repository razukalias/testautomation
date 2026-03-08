using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class Sql : Component
    {
        public Sql()
        {
            Name = "Sql";
        }

        public override Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            // SQL component logic
            var data = new SqlData { Id = this.Id, ComponentName = this.Name };
            return Task.FromResult<ComponentData>(data);
        }
    }
}
