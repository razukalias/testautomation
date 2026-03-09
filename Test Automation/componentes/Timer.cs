using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class Timer : Component
    {
        public Timer()
        {
            Name = "Timer";
        }

        public override Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            return ExecuteTimerAsync();
        }

        private async Task<ComponentData> ExecuteTimerAsync()
        {
            var data = new TimerData { Id = this.Id, ComponentName = this.Name };

            var delayMs = 0;
            if (Settings.TryGetValue("DelayMs", out var delayValue))
            {
                int.TryParse(delayValue, out delayMs);
            }

            if (delayMs > 0)
            {
                await Task.Delay(delayMs);
            }

            data.DelayMs = delayMs;
            data.Executed = true;
            return data;
        }
    }
}
