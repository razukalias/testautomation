using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class Dataset : Component
    {
        public Dataset()
        {
            Name = "Dataset";
        }

        public override Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            // Dataset component logic
            var data = new DatasetData { Id = this.Id, ComponentName = this.Name };
            return Task.FromResult<ComponentData>(data);
        }
    }
}
