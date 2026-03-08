using System.Threading.Tasks;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class GraphQl : Component
    {
        public GraphQl()
        {
            Name = "GraphQl";
        }

        public override Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            var data = new GraphQlData
            {
                Id = this.Id,
                ComponentName = this.Name
            };

            return Task.FromResult<ComponentData>(data);
        }
    }
}
