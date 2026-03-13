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
            return ExecuteTimerAsync(context);
        }

        private async Task<ComponentData> ExecuteTimerAsync(Test_Automation.Models.ExecutionContext context)
        {
            var data = new TimerData { Id = this.Id, ComponentName = this.Name };

            var delayMs = 0;
            if (Settings.TryGetValue("DelayMs", out var delayValue))
            {
                int.TryParse(delayValue, out delayMs);
            }

            if (delayMs > 0)
            {
                var remaining = delayMs;
                while (remaining > 0)
                {
                    if (!context.IsRunning || context.StopToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException("Timer execution stopped by user.");
                    }

                    var wait = Math.Min(remaining, 200);
                    await Task.Delay(wait, context.StopToken);
                    remaining -= wait;
                }
            }

            data.DelayMs = delayMs;
            data.Executed = true;
            return data;
        }
    }
}
