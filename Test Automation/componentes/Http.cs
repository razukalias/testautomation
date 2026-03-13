using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class Http : Component
    {
        public Http()
        {
            Name = "Http";
        }

        public override async Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            var url = Settings.TryGetValue("Url", out var urlValue) ? urlValue : string.Empty;
            var headers = new Dictionary<string, string>();
            ApplyAuthSettings(Settings, headers, ref url);

            var data = new HttpData
            {
                Id = this.Id,
                ComponentName = this.Name,
                Method = Settings.TryGetValue("Method", out var method) ? method : "GET",
                Url = url,
                Body = Settings.TryGetValue("Body", out var body) ? body : string.Empty
            };

            data.Properties["authType"] = Settings.TryGetValue("AuthType", out var authTypeValue)
                ? authTypeValue
                : "WindowsIntegrated";

            if (Settings.TryGetValue("Headers", out var headersJson) && !string.IsNullOrWhiteSpace(headersJson))
            {
                try
                {
                    var parsedHeaders = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
                    if (parsedHeaders != null)
                    {
                        foreach (var header in parsedHeaders)
                        {
                            if (!headers.ContainsKey(header.Key))
                            {
                                headers[header.Key] = header.Value;
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            data.Headers = headers;

            if (!string.IsNullOrWhiteSpace(url))
            {
                using var client = new HttpClient();
                using var request = new HttpRequestMessage(new HttpMethod(data.Method), url);

                foreach (var header in data.Headers)
                {
                    if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                    {
                        request.Content ??= new StringContent(string.Empty);
                        request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                if (HasBody(data.Method) && !string.IsNullOrEmpty(data.Body))
                {
                    request.Content = new StringContent(data.Body, Encoding.UTF8, "application/json");
                }

                using var response = await client.SendAsync(request, context.StopToken);
                data.ResponseStatus = (int)response.StatusCode;
                foreach (var header in response.Headers)
                {
                    data.ResponseHeaders[header.Key] = string.Join(", ", header.Value);
                }

                foreach (var header in response.Content.Headers)
                {
                    data.ResponseHeaders[header.Key] = string.Join(", ", header.Value);
                }

                data.ResponseBody = await response.Content.ReadAsStringAsync(context.StopToken);
            }

            return data;
        }

        private static bool HasBody(string method)
        {
            return method.Equals("POST", StringComparison.OrdinalIgnoreCase)
                || method.Equals("PUT", StringComparison.OrdinalIgnoreCase)
                || method.Equals("PATCH", StringComparison.OrdinalIgnoreCase)
                || method.Equals("DELETE", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyAuthSettings(Dictionary<string, string> settings, Dictionary<string, string> headers, ref string url)
        {
            if (!settings.TryGetValue("AuthType", out var authType) || string.IsNullOrWhiteSpace(authType))
            {
                return;
            }

            switch (authType)
            {
                case "Basic":
                    AddBasicAuth(settings, headers);
                    break;
                case "Bearer":
                    AddBearerAuth(settings, headers);
                    break;
                case "ApiKey":
                    AddApiKey(settings, headers, ref url);
                    break;
                case "OAuth2":
                    AddBearerAuth(settings, headers);
                    break;
                default:
                    break;
            }
        }

        private static void AddBasicAuth(Dictionary<string, string> settings, Dictionary<string, string> headers)
        {
            settings.TryGetValue("AuthUsername", out var username);
            settings.TryGetValue("AuthPassword", out var password);
            if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
            {
                return;
            }

            var raw = $"{username}:{password}";
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
            headers["Authorization"] = $"Basic {encoded}";
        }

        private static void AddBearerAuth(Dictionary<string, string> settings, Dictionary<string, string> headers)
        {
            if (!settings.TryGetValue("AuthToken", out var token) || string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            headers["Authorization"] = $"Bearer {token}";
        }

        private static void AddApiKey(Dictionary<string, string> settings, Dictionary<string, string> headers, ref string url)
        {
            settings.TryGetValue("ApiKeyName", out var name);
            settings.TryGetValue("ApiKeyValue", out var value);
            settings.TryGetValue("ApiKeyLocation", out var location);

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (string.Equals(location, "Query", StringComparison.OrdinalIgnoreCase))
            {
                var separator = url.Contains("?") ? "&" : "?";
                var encodedName = Uri.EscapeDataString(name);
                var encodedValue = Uri.EscapeDataString(value);
                url = string.Concat(url, separator, encodedName, "=", encodedValue);
                return;
            }

            headers[name] = value;
        }
    }
}
