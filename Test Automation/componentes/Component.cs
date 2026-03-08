using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class Component
    {
        public string Id { get; private set; }
        public string Name { get; protected set; }
        public Component Parent { get; set; }
        public List<Component> Children { get; set; } = new List<Component>();

        protected Component()
        {
            Id = Guid.NewGuid().ToString();
        }

        public virtual Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            return Task.FromResult<ComponentData>(null);
        }

        public void AddChild(Component child)
        {
            if (child != null)
            {
                child.Parent = this;
                Children.Add(child);
            }
        }

        public void RemoveChild(Component child)
        {
            if (child != null)
            {
                child.Parent = null;
                Children.Remove(child);
            }
        }
    }
}