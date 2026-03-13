using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test_Automation.Models;
using Test_Automation.Services;

namespace Test_Automation.Componentes
{
    public class Script : Component
    {
        public Script()
        {
            Name = "Script";
        }

        public override async Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            var language = Settings.TryGetValue("Language", out var configuredLanguage)
                ? configuredLanguage
                : "CSharp";
            var code = Settings.TryGetValue("Code", out var configuredCode)
                ? configuredCode
                : string.Empty;

            var data = new ScriptData
            {
                Id = Id,
                ComponentName = Name,
                ScriptLanguage = language,
                ScriptCode = code
            };

            var outcome = await ScriptEngine.ExecuteAsync(language, code, context);
            if (!outcome.Success)
            {
                throw new InvalidOperationException($"Script execution failed: {outcome.Error}");
            }

            var resultText = outcome.Result?.ToString() ?? string.Empty;
            data.ExecutionResult = resultText;
            data.Properties["result"] = outcome.Result ?? string.Empty;
            context.SetVariable("lastScriptResult", resultText);
            return data;
        }
    }
}
