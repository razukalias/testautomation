using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
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
            var provider = NormalizeProvider(GetSetting("Provider", "SqlServer"));
            var authType = GetSetting("AuthType", GetDefaultAuthType(provider));

            var connectionString = GetSetting("Connection", string.Empty);
            connectionString = ApplySqlAuth(provider, authType, Settings, connectionString);

            var data = new SqlData
            {
                Id = this.Id,
                ComponentName = this.Name,
                Provider = provider,
                ConnectionString = connectionString,
                Query = GetSetting("Query", string.Empty)
            };

            data.Properties["provider"] = provider;
            data.Properties["authType"] = authType;

            if (string.IsNullOrWhiteSpace(data.ConnectionString) || string.IsNullOrWhiteSpace(data.Query))
            {
                return data;
            }

            using var connection = CreateConnection(provider, data.ConnectionString);
            await connection.OpenAsync(context.StopToken);

            using var command = connection.CreateCommand();
            command.CommandText = data.Query;

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
                data.Properties["rowsAffected"] = reader.RecordsAffected;
            }

            return data;
        }

        private string GetSetting(string key, string fallback)
        {
            return Settings.TryGetValue(key, out var value) ? value : fallback;
        }

        private static DbConnection CreateConnection(string provider, string connectionString)
        {
            return provider switch
            {
                "SqlServer" => new SqlConnection(connectionString),
                "PostgreSql" => new NpgsqlConnection(connectionString),
                "MySql" => new MySqlConnection(connectionString),
                "Sqlite" => new SqliteConnection(connectionString),
                _ => throw new InvalidOperationException($"Unsupported SQL provider: {provider}")
            };
        }

        private static string NormalizeProvider(string? provider)
        {
            if (string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, "Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                return "PostgreSql";
            }

            if (string.Equals(provider, "MySql", StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, "MySQL", StringComparison.OrdinalIgnoreCase))
            {
                return "MySql";
            }

            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, "SQLite", StringComparison.OrdinalIgnoreCase))
            {
                return "Sqlite";
            }

            return "SqlServer";
        }

        private static string GetDefaultAuthType(string provider)
        {
            return string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase)
                ? "WindowsIntegrated"
                : "None";
        }

        private static string ApplySqlAuth(string provider, string authType, Dictionary<string, string> settings, string connectionString)
        {
            var normalizedProvider = NormalizeProvider(provider);
            var normalizedAuth = string.IsNullOrWhiteSpace(authType) ? GetDefaultAuthType(normalizedProvider) : authType.Trim();

            if (string.Equals(normalizedAuth, "None", StringComparison.OrdinalIgnoreCase))
            {
                return connectionString;
            }

            if (string.Equals(normalizedProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(normalizedAuth, "None", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Sqlite supports only 'None' authentication in SQL component.");
                }

                return connectionString;
            }

            if (string.Equals(normalizedProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(normalizedAuth, "WindowsIntegrated", StringComparison.OrdinalIgnoreCase))
                {
                    if (!ContainsConnectionKey(connectionString, "Integrated Security")
                        && !ContainsConnectionKey(connectionString, "Trusted_Connection"))
                    {
                        connectionString = AppendConnectionPart(connectionString, "Integrated Security=true");
                    }

                    return connectionString;
                }

                if (string.Equals(normalizedAuth, "Basic", StringComparison.OrdinalIgnoreCase))
                {
                    settings.TryGetValue("AuthUsername", out var username);
                    settings.TryGetValue("AuthPassword", out var password);

                    if (!string.IsNullOrWhiteSpace(username)
                        && !ContainsConnectionKey(connectionString, "User Id")
                        && !ContainsConnectionKey(connectionString, "UserID")
                        && !ContainsConnectionKey(connectionString, "UID"))
                    {
                        connectionString = AppendConnectionPart(connectionString, $"User Id={username}");
                    }

                    if (!string.IsNullOrWhiteSpace(password)
                        && !ContainsConnectionKey(connectionString, "Password")
                        && !ContainsConnectionKey(connectionString, "Pwd"))
                    {
                        connectionString = AppendConnectionPart(connectionString, $"Password={password}");
                    }

                    return connectionString;
                }

                throw new InvalidOperationException($"Unsupported SQL Server auth type '{normalizedAuth}'. Use WindowsIntegrated, Basic, or None.");
            }

            if (string.Equals(normalizedAuth, "WindowsIntegrated", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Provider '{normalizedProvider}' does not support WindowsIntegrated auth in SQL component.");
            }

            if (string.Equals(normalizedAuth, "Basic", StringComparison.OrdinalIgnoreCase))
            {
                settings.TryGetValue("AuthUsername", out var username);
                settings.TryGetValue("AuthPassword", out var password);

                if (string.Equals(normalizedProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(username)
                        && !ContainsConnectionKey(connectionString, "Username")
                        && !ContainsConnectionKey(connectionString, "User ID")
                        && !ContainsConnectionKey(connectionString, "UserId"))
                    {
                        connectionString = AppendConnectionPart(connectionString, $"Username={username}");
                    }

                    if (!string.IsNullOrWhiteSpace(password)
                        && !ContainsConnectionKey(connectionString, "Password"))
                    {
                        connectionString = AppendConnectionPart(connectionString, $"Password={password}");
                    }

                    return connectionString;
                }

                if (string.Equals(normalizedProvider, "MySql", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(username)
                        && !ContainsConnectionKey(connectionString, "User Id")
                        && !ContainsConnectionKey(connectionString, "Uid")
                        && !ContainsConnectionKey(connectionString, "UserID"))
                    {
                        connectionString = AppendConnectionPart(connectionString, $"User Id={username}");
                    }

                    if (!string.IsNullOrWhiteSpace(password)
                        && !ContainsConnectionKey(connectionString, "Password")
                        && !ContainsConnectionKey(connectionString, "Pwd"))
                    {
                        connectionString = AppendConnectionPart(connectionString, $"Password={password}");
                    }

                    return connectionString;
                }
            }

            throw new InvalidOperationException($"Unsupported auth '{normalizedAuth}' for provider '{normalizedProvider}'.");
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