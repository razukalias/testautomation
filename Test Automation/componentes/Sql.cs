using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Test_Automation.Models;

namespace Test_Automation.Componentes
{
    public class Sql : Component
    {
        public Sql()
        {
            Name = "Sql";
        }

        public override async Task<ComponentData> Execute(Test_Automation.Models.ExecutionContext context)
        {
            var connectionString = Settings.TryGetValue("Connection", out var connectionValue)
                ? connectionValue
                : string.Empty;
            connectionString = ApplySqlAuth(Settings, connectionString);

            var data = new SqlData
            {
                Id = this.Id,
                ComponentName = this.Name,
                ConnectionString = connectionString,
                Query = Settings.TryGetValue("Query", out var queryValue) ? queryValue : string.Empty
            };

            data.Properties["authType"] = Settings.TryGetValue("AuthType", out var authTypeValue)
                ? authTypeValue
                : "WindowsIntegrated";

            if (!string.IsNullOrWhiteSpace(data.ConnectionString) && !string.IsNullOrWhiteSpace(data.Query))
            {
                using var connection = new SqlConnection(data.ConnectionString);
                await connection.OpenAsync(context.StopToken);
                using var command = new SqlCommand(data.Query, connection);

                using var reader = await command.ExecuteReaderAsync(context.StopToken);
                if (reader.FieldCount > 0)
                {
                    while (await reader.ReadAsync(context.StopToken))
                    {
                        var row = new Dictionary<string, object>();
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            var name = reader.GetName(i);
                            var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            row[name] = value ?? string.Empty;
                        }

                        data.QueryResult.Add(row);
                    }
                }
                else
                {
                    var affected = reader.RecordsAffected;
                    data.Properties["rowsAffected"] = affected;
                }
            }

            return data;
        }

        private static string ApplySqlAuth(Dictionary<string, string> settings, string connectionString)
        {
            settings.TryGetValue("AuthType", out var authType);

            if (string.Equals(authType, "WindowsIntegrated", StringComparison.OrdinalIgnoreCase))
            {
                if (!ContainsConnectionKey(connectionString, "Integrated Security")
                    && !ContainsConnectionKey(connectionString, "Trusted_Connection"))
                {
                    connectionString = AppendConnectionPart(connectionString, "Integrated Security=true");
                }

                return connectionString;
            }

            if (string.Equals(authType, "Basic", StringComparison.OrdinalIgnoreCase))
            {
                settings.TryGetValue("AuthUsername", out var username);
                settings.TryGetValue("AuthPassword", out var password);

                if (!string.IsNullOrWhiteSpace(username) && !ContainsConnectionKey(connectionString, "User Id"))
                {
                    connectionString = AppendConnectionPart(connectionString, $"User Id={username}");
                }

                if (!string.IsNullOrWhiteSpace(password) && !ContainsConnectionKey(connectionString, "Password"))
                {
                    connectionString = AppendConnectionPart(connectionString, $"Password={password}");
                }
            }

            return connectionString;
        }

        private static bool ContainsConnectionKey(string connectionString, string key)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return false;
            }

            return connectionString.IndexOf(key + "=", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string AppendConnectionPart(string connectionString, string part)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return part;
            }

            if (!connectionString.EndsWith(";", StringComparison.Ordinal))
            {
                connectionString += ";";
            }

            return connectionString + part;
        }
    }
}
