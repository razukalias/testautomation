using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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

        public override async Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            var endpoint = Settings.TryGetValue("Endpoint", out var endpointValue) ? endpointValue : string.Empty;
            var headers = new Dictionary<string, string>();
            ApplyAuthSettings(Settings, headers, ref endpoint);

            var data = new GraphQlData
            {
                Id = this.Id,
                ComponentName = this.Name,
                Endpoint = endpoint,
                Query = Settings.TryGetValue("Query", out var queryValue) ? queryValue : string.Empty,
                Variables = Settings.TryGetValue("Variables", out var variablesValue) ? variablesValue : "{}",
                Headers = headers
            };

            data.Properties["authType"] = Settings.TryGetValue("AuthType", out var authTypeValue)
                ? authTypeValue
                : "WindowsIntegrated";

            if (Settings.TryGetValue("Headers", out var headersJson) && !string.IsNullOrWhiteSpace(headersJson))
            {
                try
                {
                    var parsedHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
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

            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                var requestBody = BuildGraphQlPayload(data.Query, data.Variables);

                using var client = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
                };

                foreach (var header in data.Headers)
                {
                    if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                    {
                        request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                using var response = await client.SendAsync(request);
                data.ResponseStatus = (int)response.StatusCode;
                data.ResponseBody = await response.Content.ReadAsStringAsync();
            }

            return data;
        }

        private static string BuildGraphQlPayload(string query, string variables)
        {
            object? variablesPayload = null;

            if (!string.IsNullOrWhiteSpace(variables))
            {
                try
                {
                    using var doc = JsonDocument.Parse(variables);
                    variablesPayload = doc.RootElement.Clone();
                }
                catch
                {
                    variablesPayload = variables;
                }
            }

            var payload = new Dictionary<string, object?>
            {
                ["query"] = query,
                ["variables"] = variablesPayload ?? new Dictionary<string, object>()
            };

            return JsonSerializer.Serialize(payload);
        }

        private static void ApplyAuthSettings(Dictionary<string, string> settings, Dictionary<string, string> headers, ref string endpoint)
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
                    AddApiKey(settings, headers, ref endpoint);
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

        private static void AddApiKey(Dictionary<string, string> settings, Dictionary<string, string> headers, ref string endpoint)
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
                var separator = endpoint.Contains("?") ? "&" : "?";
                var encodedName = Uri.EscapeDataString(name);
                var encodedValue = Uri.EscapeDataString(value);
                endpoint = string.Concat(endpoint, separator, encodedName, "=", encodedValue);
                return;
            }

            headers[name] = value;
        }
    }
}
