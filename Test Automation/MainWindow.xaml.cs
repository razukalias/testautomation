using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Data;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Test_Automation.Factories;
using Test_Automation.Models;
using Test_Automation.Models.Editor;
using Test_Automation.Models.ProjectFiles;
using Test_Automation.Services;

namespace Test_Automation
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<PlanNode> RootNodes { get; } = new ObservableCollection<PlanNode>();
        public ObservableCollection<string> ExtractorSourceOptions { get; } = new ObservableCollection<string>();
        private static readonly string[] BaseExtractorSources =
        {
            "PreviewRequest",
            "PreviewResponse",
            "PreviewLogs"
        };
        public ObservableCollection<string> AuthTypeOptions { get; } = new ObservableCollection<string>
        {
            "WindowsIntegrated",
            "None",
            "Basic",
            "Bearer",
            "ApiKey",
            "OAuth2"
        };

        public ObservableCollection<string> AssertionConditionOptions { get; } = new ObservableCollection<string>
        {
            "Equals",
            "NotEquals",
            "Contains",
            "NotContains",
            "StartsWith",
            "EndsWith",
            "GreaterThan",
            "GreaterOrEqual",
            "LessThan",
            "LessOrEqual",
            "IsEmpty",
            "IsNotEmpty",
            "Regex",
            "Script"
        };

        public ObservableCollection<string> AssertionModeOptions { get; } = new ObservableCollection<string>
        {
            "Assert",
            "Expect",
            "Assert and Stop"
        };

        public ObservableCollection<string> EnvironmentOptions { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> TraceLevelOptions { get; } = new ObservableCollection<string>
        {
            "Off",
            "Errors",
            "Verbose"
        };

        public ObservableCollection<string> HttpMethodOptions { get; } = new ObservableCollection<string>
        {
            "GET",
            "POST",
            "PUT",
            "PATCH",
            "DELETE",
            "HEAD",
            "OPTIONS"
        };

        public ObservableCollection<string> SqlProviderOptions { get; } = new ObservableCollection<string>
        {
            "SqlServer",
            "PostgreSql",
            "MySql",
            "Sqlite"
        };

        public ObservableCollection<string> DatasetFormatOptions { get; } = new ObservableCollection<string>
        {
            "Auto",
            "Excel",
            "Csv",
            "Json",
            "Xml"
        };

        public ObservableCollection<string> ProjectRunModeOptions { get; } = new ObservableCollection<string>
        {
            "Sequence",
            "Parallel"
        };

        public ObservableCollection<string> PreviewDataModeOptions { get; } = new ObservableCollection<string>
        {
            "Last Run",
            "Full History"
        };

        private PlanNode? _selectedNode;
        private string _selectedEnvironment = string.Empty;
        private string _selectedTraceLevel = "Verbose";
        private bool _isSyncingEnvironment;
        private bool _isRefreshingEnvironmentOptions;
        private bool _isNormalizingVariables;
        private int _variableUsageVersion;
        private Dictionary<string, List<string>> _variableUsageMap = new(StringComparer.OrdinalIgnoreCase);
        private string? _currentProjectFilePath;
        private string _jsonPreview = "{}";
        private string _previewRequest = "Select a component to see request preview.";
        private string _previewResponse = "Select a component to see response preview.";
        private string _httpRequestHeadersPreview = "Select an HTTP component to see request headers.";
        private string _httpRequestCookiesPreview = "Select an HTTP component to see request cookies.";
        private string _httpRequestMetadataPreview = "Select an HTTP component to see request metadata.";
        private string _httpResponseHeadersPreview = "Select an HTTP component to see response headers.";
        private string _httpResponseCookiesPreview = "Select an HTTP component to see response cookies.";
        private string _httpResponseMetadataPreview = "Select an HTTP component to see response metadata.";
        private string _previewLogs = "Logs will appear here.";
        private string _variablesPreview = "{}";
        private string _assertionPreview = "Select a component to see assertion preview.";
        private string _runtimeTraceLogBuffer = string.Empty;
        private Test_Automation.Models.ExecutionContext? _lastExecutionContext;
        private Test_Automation.Models.ExecutionContext? _activeExecutionContext;
        private readonly List<Test_Automation.Models.ExecutionContext> _activeProjectExecutionContexts = new();
        private bool _isRunInProgress;
        private string _selectedProjectRunMode = "Sequence";
        private string _selectedPreviewDataMode = "Last Run";
        private bool _isApplyingCatalogSelection;
        private bool _isSynchronizingCatalogEditor;
        private readonly ObservableCollection<ApiCatalogBaseUrlEntry> _projectUrlCatalogEntries = new();
        private readonly ObservableCollection<string> _catalogBaseUrlOptions = new();
        private readonly ObservableCollection<string> _catalogEndpointOptions = new();
        private readonly ObservableCollection<string> _sqlAuthTypeOptions = new();
        private DataTable _datasetPreviewTable = new("DatasetPreview");
        private string _datasetPreviewStatus = "Choose a dataset source and click Refresh Preview.";
        private readonly List<ApiCatalogBaseUrlEntry> _parsedApiCatalog = new();
        private bool _isApplyingMainLayoutBounds;
        private static readonly string MainLayoutStatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TestAutomation",
            "layout.main.json");
        private static readonly string AppStatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TestAutomation",
            "appstate.json");

        private sealed class MainLayoutState
        {
            public double LeftColumnWidth { get; set; }
            public double LeftTopRowHeight { get; set; }
            public double EditorTopRowHeight { get; set; }
            public double EditorBottomRowHeight { get; set; }
            public double AssertionRowsHeight { get; set; }
            public double AssertionTreeHeight { get; set; }
            public double AssertionTreeLeftWidth { get; set; }
            public double AssertionTreeRightWidth { get; set; }
        }

        private sealed class AppState
        {
            public string? LastProjectFilePath { get; set; }
        }

        public sealed class ApiCatalogBaseUrlEntry
            : INotifyPropertyChanged
        {
            private string _name = string.Empty;
            private string _baseUrl = string.Empty;

            public string Name
            {
                get => _name;
                set
                {
                    if (_name == value) return;
                    _name = value;
                    OnPropertyChanged();
                }
            }

            public string BaseUrl
            {
                get => _baseUrl;
                set
                {
                    if (_baseUrl == value) return;
                    _baseUrl = value;
                    OnPropertyChanged();
                }
            }

            public ObservableCollection<ApiCatalogEndpointEntry> Endpoints { get; set; } = new();

            public event PropertyChangedEventHandler? PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public sealed class ApiCatalogEndpointEntry
            : INotifyPropertyChanged
        {
            private string _name = string.Empty;
            private string _path = string.Empty;
            private string _method = string.Empty;
            private string _body = string.Empty;
            private string _headers = string.Empty;
            private string _query = string.Empty;
            private string _variables = string.Empty;

            public string Name
            {
                get => _name;
                set
                {
                    if (_name == value) return;
                    _name = value;
                    OnPropertyChanged();
                }
            }

            public string Path
            {
                get => _path;
                set
                {
                    if (_path == value) return;
                    _path = value;
                    OnPropertyChanged();
                }
            }

            public string Method
            {
                get => _method;
                set
                {
                    if (_method == value) return;
                    _method = value;
                    OnPropertyChanged();
                }
            }

            public string Body
            {
                get => _body;
                set
                {
                    if (_body == value) return;
                    _body = value;
                    OnPropertyChanged();
                }
            }

            public ObservableCollection<ApiCatalogParameterEntry> Parameters { get; set; } = new();

            public string Headers
            {
                get => _headers;
                set
                {
                    if (_headers == value) return;
                    _headers = value;
                    OnPropertyChanged();
                }
            }

            public string Query
            {
                get => _query;
                set
                {
                    if (_query == value) return;
                    _query = value;
                    OnPropertyChanged();
                }
            }

            public string Variables
            {
                get => _variables;
                set
                {
                    if (_variables == value) return;
                    _variables = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public sealed class ApiCatalogParameterEntry : INotifyPropertyChanged
        {
            private string _key = string.Empty;
            private string _value = string.Empty;

            public string Key
            {
                get => _key;
                set
                {
                    if (_key == value) return;
                    _key = value;
                    OnPropertyChanged();
                }
            }

            public string Value
            {
                get => _value;
                set
                {
                    if (_value == value) return;
                    _value = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        // Deserializes "parameters" from either legacy string form or new [{key,value}] array form.
        private sealed class CatalogParameterCollectionConverter
            : System.Text.Json.Serialization.JsonConverter<ObservableCollection<ApiCatalogParameterEntry>>
        {
            public override ObservableCollection<ApiCatalogParameterEntry> Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options)
            {
                var result = new ObservableCollection<ApiCatalogParameterEntry>();
                if (reader.TokenType == JsonTokenType.String)
                {
                    var raw = (reader.GetString() ?? string.Empty).TrimStart('?');
                    foreach (var part in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var eqIdx = part.IndexOf('=');
                        result.Add(eqIdx >= 0
                            ? new ApiCatalogParameterEntry { Key = Uri.UnescapeDataString(part[..eqIdx]), Value = Uri.UnescapeDataString(part[(eqIdx + 1)..]) }
                            : new ApiCatalogParameterEntry { Key = Uri.UnescapeDataString(part), Value = string.Empty });
                    }
                    return result;
                }
                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    using var doc = JsonDocument.ParseValue(ref reader);
                    foreach (var element in doc.RootElement.EnumerateArray())
                    {
                        result.Add(new ApiCatalogParameterEntry
                        {
                            Key   = element.TryGetProperty("key",   out var k) ? k.GetString() ?? string.Empty : string.Empty,
                            Value = element.TryGetProperty("value", out var v) ? v.GetString() ?? string.Empty : string.Empty,
                        });
                    }
                    return result;
                }
                reader.Skip();
                return result;
            }

            public override void Write(
                Utf8JsonWriter writer,
                ObservableCollection<ApiCatalogParameterEntry> value,
                JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                foreach (var p in value)
                {
                    writer.WriteStartObject();
                    writer.WriteString("key",   p.Key   ?? string.Empty);
                    writer.WriteString("value", p.Value ?? string.Empty);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
        }

        public PlanNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode == value) return;
                _selectedNode = value;
                OnPropertyChanged();
                SyncCatalogSelectionFromCurrentNode();
                NotifySelectedNodeEditorProperties();
                RebuildExtractorSourceOptions();
                RefreshComponentPreview();
                RefreshDatasetPreview();
                RefreshAssertionJsonTreePanel();
            }
        }

        public string JsonPreview
        {
            get => _jsonPreview;
            set
            {
                if (_jsonPreview == value) return;
                _jsonPreview = value;
                OnPropertyChanged();
            }
        }

        public string PreviewRequest
        {
            get => _previewRequest;
            set
            {
                if (_previewRequest == value) return;
                _previewRequest = value;
                OnPropertyChanged();
            }
        }

        public string PreviewResponse
        {
            get => _previewResponse;
            set
            {
                if (_previewResponse == value) return;
                _previewResponse = value;
                OnPropertyChanged();
            }
        }

        public string PreviewLogs
        {
            get => _previewLogs;
            set
            {
                if (_previewLogs == value) return;
                _previewLogs = value;
                OnPropertyChanged();
            }
        }

        public string HttpRequestHeadersPreview
        {
            get => _httpRequestHeadersPreview;
            set
            {
                if (_httpRequestHeadersPreview == value) return;
                _httpRequestHeadersPreview = value;
                OnPropertyChanged();
            }
        }

        public string HttpRequestCookiesPreview
        {
            get => _httpRequestCookiesPreview;
            set
            {
                if (_httpRequestCookiesPreview == value) return;
                _httpRequestCookiesPreview = value;
                OnPropertyChanged();
            }
        }

        public string HttpRequestMetadataPreview
        {
            get => _httpRequestMetadataPreview;
            set
            {
                if (_httpRequestMetadataPreview == value) return;
                _httpRequestMetadataPreview = value;
                OnPropertyChanged();
            }
        }

        public string HttpResponseHeadersPreview
        {
            get => _httpResponseHeadersPreview;
            set
            {
                if (_httpResponseHeadersPreview == value) return;
                _httpResponseHeadersPreview = value;
                OnPropertyChanged();
            }
        }

        public string HttpResponseCookiesPreview
        {
            get => _httpResponseCookiesPreview;
            set
            {
                if (_httpResponseCookiesPreview == value) return;
                _httpResponseCookiesPreview = value;
                OnPropertyChanged();
            }
        }

        public string HttpResponseMetadataPreview
        {
            get => _httpResponseMetadataPreview;
            set
            {
                if (_httpResponseMetadataPreview == value) return;
                _httpResponseMetadataPreview = value;
                OnPropertyChanged();
            }
        }

        public string VariablesPreview
        {
            get => _variablesPreview;
            set
            {
                if (_variablesPreview == value) return;
                _variablesPreview = value;
                OnPropertyChanged();
            }
        }

        public string AssertionPreview
        {
            get => _assertionPreview;
            set
            {
                if (_assertionPreview == value) return;
                _assertionPreview = value;
                OnPropertyChanged();
            }
        }

        public bool IsProjectSelected => SelectedNode?.Type == "Project";
        public bool IsComponentSelected => SelectedNode != null && SelectedNode.Type != "Project";
        public bool HasSelectedNodeChildren => SelectedNode != null && SelectedNode.Children.Count > 0;
        public bool IsHttpSelected => SelectedNode?.Type == "Http";
        public bool IsGraphQlSelected => SelectedNode?.Type == "GraphQl";
        public bool IsSqlSelected => SelectedNode?.Type == "Sql";
        public bool IsDatasetSelected => SelectedNode?.Type == "Dataset";
        public bool IsTimerSelected => SelectedNode?.Type == "Timer";
        public bool IsLoopSelected => SelectedNode?.Type == "Loop";
        public bool IsIfSelected => SelectedNode?.Type == "If";
        public bool IsThreadsSelected => SelectedNode?.Type == "Threads";
        public bool IsAssertSelected => SelectedNode?.Type == "Assert";
        public bool IsVariableExtractorSelected => SelectedNode?.Type == "VariableExtractor";
        public bool IsScriptSelected => SelectedNode?.Type == "Script";

        public IEnumerable<NodeSetting> ProjectVariablesForEditor =>
            SelectedNode?.Type == "Project"
                ? SelectedNode.Variables.Where(variable => !string.Equals(variable.Key, "env", StringComparison.OrdinalIgnoreCase))
                : Enumerable.Empty<NodeSetting>();

        public int VariableUsageVersion
        {
            get => _variableUsageVersion;
            private set
            {
                if (_variableUsageVersion == value) return;
                _variableUsageVersion = value;
                OnPropertyChanged();
            }
        }

        public string ProjectDescription
        {
            get => GetSettingValue("Description", string.Empty);
            set => SetSettingValue("Description", value);
        }

        public string ProjectEnvironment
        {
            get => GetSettingValue("Environment", "dev");
            set
            {
                SetSettingValue("Environment", value);
                RefreshEnvironmentOptions();
            }
        }

        public string ProjectUrlCatalogJson
        {
            get => GetProjectSettingValue("UrlCatalog", "[]");
            set
            {
                SetProjectSettingValue("UrlCatalog", value ?? "[]");
                if (!_isSynchronizingCatalogEditor)
                {
                    RefreshApiCatalogState();
                }
            }
        }

        public ObservableCollection<ApiCatalogBaseUrlEntry> ProjectUrlCatalogEntries => _projectUrlCatalogEntries;

        public ObservableCollection<string> CatalogBaseUrlOptions => _catalogBaseUrlOptions;
        public ObservableCollection<string> CatalogEndpointOptions => _catalogEndpointOptions;

        public string SelectedHttpCatalogBase
        {
            get => string.Equals(SelectedNode?.Type, "Http", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("CatalogBase", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Http", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var next = value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(next))
                {
                    return;
                }

                SetSettingValue("CatalogBase", next);
                if (_isApplyingCatalogSelection)
                {
                    return;
                }

                RefreshCatalogEndpointOptions();
                OnPropertyChanged();
            }
        }

        public string SelectedHttpCatalogEndpoint
        {
            get => string.Equals(SelectedNode?.Type, "Http", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("CatalogEndpoint", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Http", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var next = value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(next))
                {
                    return;
                }

                SetSettingValue("CatalogEndpoint", next);
                OnPropertyChanged();
            }
        }

        public string SelectedGraphQlCatalogBase
        {
            get => string.Equals(SelectedNode?.Type, "GraphQl", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("CatalogBase", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "GraphQl", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var next = value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(next))
                {
                    return;
                }

                SetSettingValue("CatalogBase", next);
                if (_isApplyingCatalogSelection)
                {
                    return;
                }

                RefreshCatalogEndpointOptions();
                OnPropertyChanged();
            }
        }

        public string SelectedGraphQlCatalogEndpoint
        {
            get => string.Equals(SelectedNode?.Type, "GraphQl", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("CatalogEndpoint", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "GraphQl", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var next = value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(next))
                {
                    return;
                }

                SetSettingValue("CatalogEndpoint", next);
                OnPropertyChanged();
            }
        }

        public string SelectedEnvironment
        {
            get => _selectedEnvironment;
            set
            {
                var next = value ?? string.Empty;
                if (string.Equals(_selectedEnvironment, next, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedEnvironment = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HttpUrlResolved));
                OnPropertyChanged(nameof(SqlConnectionResolved));
                OnPropertyChanged(nameof(SqlQueryResolved));
                OnPropertyChanged(nameof(DatasetResolvedSourcePath));

                if (string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    RefreshDatasetPreview();
                }

                if (_isSyncingEnvironment)
                {
                    return;
                }

                if (SetProjectVariable("env", _selectedEnvironment))
                {
                    UpdateProjectVariablesPreview();
                    RefreshJsonPreview();
                }
            }
        }

        public string SelectedTraceLevel
        {
            get => _selectedTraceLevel;
            set
            {
                var normalized = TraceLevelOptions.Contains(value) ? value : "Verbose";
                if (string.Equals(_selectedTraceLevel, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedTraceLevel = normalized;
                OnPropertyChanged();
            }
        }

        public string SelectedProjectRunMode
        {
            get => _selectedProjectRunMode;
            set
            {
                var normalized = ProjectRunModeOptions.Contains(value) ? value : "Sequence";
                if (string.Equals(_selectedProjectRunMode, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedProjectRunMode = normalized;
                OnPropertyChanged();
            }
        }

        public string SelectedPreviewDataMode
        {
            get => _selectedPreviewDataMode;
            set
            {
                var normalized = PreviewDataModeOptions.Contains(value) ? value : "Last Run";
                if (string.Equals(_selectedPreviewDataMode, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedPreviewDataMode = normalized;
                OnPropertyChanged();
                RefreshComponentPreview();
            }
        }

        private bool IsFullPreviewHistoryMode => string.Equals(SelectedPreviewDataMode, "Full History", StringComparison.OrdinalIgnoreCase);

        private List<ExecutionResult> FilterPreviewResults(IEnumerable<ExecutionResult> source, bool lastPerComponent)
        {
            var ordered = source
                .OrderBy(result => result.EndTime ?? result.StartTime)
                .ToList();

            if (IsFullPreviewHistoryMode || ordered.Count == 0)
            {
                return ordered;
            }

            if (!lastPerComponent)
            {
                return new List<ExecutionResult> { ordered[^1] };
            }

            return ordered
                .GroupBy(result => result.ComponentId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(result => result.EndTime ?? result.StartTime)
                    .First())
                .OrderBy(result => result.EndTime ?? result.StartTime)
                .ToList();
        }

        public string HttpMethod
        {
            get => GetSettingValue("Method", "GET");
            set => SetSettingValue("Method", value);
        }

        public string HttpUrl
        {
            get => GetSettingValue("Url", string.Empty);
            set => SetSettingValue("Url", value);
        }

        public string HttpUrlResolved => ResolveWithProjectVariables(HttpUrl);

        public string HttpBody
        {
            get => GetSettingValue("Body", string.Empty);
            set => SetSettingValue("Body", value);
        }

        public string HttpHeaders
        {
            get => GetSettingValue("Headers", "{}");
            set => SetSettingValue("Headers", value);
        }

        public string HttpAuthType
        {
            get => GetSettingValue("AuthType", "WindowsIntegrated");
            set
            {
                SetSettingValue("AuthType", value);
                RaiseHttpAuthVisibilityChanged();
            }
        }

        public bool HttpShowBasicFields => string.Equals(HttpAuthType, "Basic", StringComparison.OrdinalIgnoreCase);
        public bool HttpShowBearerFields => string.Equals(HttpAuthType, "Bearer", StringComparison.OrdinalIgnoreCase);
        public bool HttpShowApiKeyFields => string.Equals(HttpAuthType, "ApiKey", StringComparison.OrdinalIgnoreCase);
        public bool HttpShowOAuthFields => string.Equals(HttpAuthType, "OAuth2", StringComparison.OrdinalIgnoreCase);

        public string HttpAuthUsername
        {
            get => GetSettingValue("AuthUsername", string.Empty);
            set => SetSettingValue("AuthUsername", value);
        }

        public string HttpAuthPassword
        {
            get => GetSettingValue("AuthPassword", string.Empty);
            set => SetSettingValue("AuthPassword", value);
        }

        public string HttpAuthToken
        {
            get => GetSettingValue("AuthToken", string.Empty);
            set => SetSettingValue("AuthToken", value);
        }

        public string HttpApiKeyName
        {
            get => GetSettingValue("ApiKeyName", string.Empty);
            set => SetSettingValue("ApiKeyName", value);
        }

        public string HttpApiKeyValue
        {
            get => GetSettingValue("ApiKeyValue", string.Empty);
            set => SetSettingValue("ApiKeyValue", value);
        }

        public string HttpApiKeyLocation
        {
            get => GetSettingValue("ApiKeyLocation", "Header");
            set => SetSettingValue("ApiKeyLocation", value);
        }

        public string HttpOAuthTokenUrl
        {
            get => GetSettingValue("OAuthTokenUrl", string.Empty);
            set => SetSettingValue("OAuthTokenUrl", value);
        }

        public string HttpOAuthClientId
        {
            get => GetSettingValue("OAuthClientId", string.Empty);
            set => SetSettingValue("OAuthClientId", value);
        }

        public string HttpOAuthClientSecret
        {
            get => GetSettingValue("OAuthClientSecret", string.Empty);
            set => SetSettingValue("OAuthClientSecret", value);
        }

        public string HttpOAuthScope
        {
            get => GetSettingValue("OAuthScope", string.Empty);
            set => SetSettingValue("OAuthScope", value);
        }

        public string GraphQlEndpoint
        {
            get => GetSettingValue("Endpoint", "https://api.example.com/graphql");
            set => SetSettingValue("Endpoint", value);
        }

        public string GraphQlQuery
        {
            get => GetSettingValue("Query", "query { health }");
            set => SetSettingValue("Query", value);
        }

        public string GraphQlVariables
        {
            get => GetSettingValue("Variables", "{}");
            set => SetSettingValue("Variables", value);
        }

        public string GraphQlHeaders
        {
            get => GetSettingValue("Headers", "{}");
            set => SetSettingValue("Headers", value);
        }

        public string GraphQlAuthType
        {
            get => GetSettingValue("AuthType", "WindowsIntegrated");
            set
            {
                SetSettingValue("AuthType", value);
                RaiseGraphQlAuthVisibilityChanged();
            }
        }

        public bool GraphQlShowBasicFields => string.Equals(GraphQlAuthType, "Basic", StringComparison.OrdinalIgnoreCase);
        public bool GraphQlShowBearerFields => string.Equals(GraphQlAuthType, "Bearer", StringComparison.OrdinalIgnoreCase);
        public bool GraphQlShowApiKeyFields => string.Equals(GraphQlAuthType, "ApiKey", StringComparison.OrdinalIgnoreCase);
        public bool GraphQlShowOAuthFields => string.Equals(GraphQlAuthType, "OAuth2", StringComparison.OrdinalIgnoreCase);

        public string GraphQlAuthUsername
        {
            get => GetSettingValue("AuthUsername", string.Empty);
            set => SetSettingValue("AuthUsername", value);
        }

        public string GraphQlAuthPassword
        {
            get => GetSettingValue("AuthPassword", string.Empty);
            set => SetSettingValue("AuthPassword", value);
        }

        public string GraphQlAuthToken
        {
            get => GetSettingValue("AuthToken", string.Empty);
            set => SetSettingValue("AuthToken", value);
        }

        public string GraphQlApiKeyName
        {
            get => GetSettingValue("ApiKeyName", string.Empty);
            set => SetSettingValue("ApiKeyName", value);
        }

        public string GraphQlApiKeyValue
        {
            get => GetSettingValue("ApiKeyValue", string.Empty);
            set => SetSettingValue("ApiKeyValue", value);
        }

        public string GraphQlApiKeyLocation
        {
            get => GetSettingValue("ApiKeyLocation", "Header");
            set => SetSettingValue("ApiKeyLocation", value);
        }

        public string GraphQlOAuthTokenUrl
        {
            get => GetSettingValue("OAuthTokenUrl", string.Empty);
            set => SetSettingValue("OAuthTokenUrl", value);
        }

        public string GraphQlOAuthClientId
        {
            get => GetSettingValue("OAuthClientId", string.Empty);
            set => SetSettingValue("OAuthClientId", value);
        }

        public string GraphQlOAuthClientSecret
        {
            get => GetSettingValue("OAuthClientSecret", string.Empty);
            set => SetSettingValue("OAuthClientSecret", value);
        }

        public string GraphQlOAuthScope
        {
            get => GetSettingValue("OAuthScope", string.Empty);
            set => SetSettingValue("OAuthScope", value);
        }

        public string SqlConnection
        {
            get => string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("Connection", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("Connection", value);
            }
        }

        public string SqlConnectionResolved => ResolveWithProjectVariables(SqlConnection);

        public string SqlProvider
        {
            get => string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase)
                ? NormalizeSqlProvider(GetSettingValue("Provider", "SqlServer"))
                : "SqlServer";
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var normalized = NormalizeSqlProvider(value);
                SetSettingValue("Provider", normalized);
                RefreshSqlAuthTypeOptions();

                if (!_sqlAuthTypeOptions.Contains(SqlAuthType))
                {
                    SqlAuthType = GetDefaultSqlAuthType(normalized);
                }

                RaiseSqlAuthVisibilityChanged();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SqlAuthTypeOptions));
            }
        }

        public string SqlQuery
        {
            get => string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("Query", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("Query", value);
            }
        }

        public string SqlQueryResolved => ResolveWithProjectVariables(SqlQuery);

        public string SqlAuthType
        {
            get => string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("AuthType", GetDefaultSqlAuthType(SqlProvider))
                : GetDefaultSqlAuthType("SqlServer");
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var normalized = value ?? string.Empty;
                if (!_sqlAuthTypeOptions.Contains(normalized))
                {
                    normalized = GetDefaultSqlAuthType(SqlProvider);
                }

                SetSettingValue("AuthType", normalized);
                RaiseSqlAuthVisibilityChanged();
            }
        }

        public ObservableCollection<string> SqlAuthTypeOptions => _sqlAuthTypeOptions;

        public bool SqlShowBasicFields => string.Equals(SqlAuthType, "Basic", StringComparison.OrdinalIgnoreCase);

        public string SqlAuthUsername
        {
            get => string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("AuthUsername", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("AuthUsername", value);
            }
        }

        public string SqlAuthPassword
        {
            get => string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("AuthPassword", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("AuthPassword", value);
            }
        }

        public string DatasetFormat
        {
            get => string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("Format", "Auto")
                : "Auto";
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("Format", value);
                RefreshDatasetPreview();
            }
        }

        public string DatasetSourcePath
        {
            get => string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("SourcePath", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("SourcePath", value);
                OnPropertyChanged(nameof(DatasetResolvedSourcePath));
                RefreshDatasetPreview();
            }
        }

        public string DatasetResolvedSourcePath => ResolveWithProjectVariables(DatasetSourcePath);

        public string DatasetSheetName
        {
            get => string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("SheetName", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("SheetName", value);
                RefreshDatasetPreview();
            }
        }

        public string DatasetCsvDelimiter
        {
            get => string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("CsvDelimiter", ",")
                : ",";
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("CsvDelimiter", value);
                RefreshDatasetPreview();
            }
        }

        public bool DatasetCsvHasHeader
        {
            get
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var raw = GetSettingValue("CsvHasHeader", "true");
                return bool.TryParse(raw, out var parsed) ? parsed : true;
            }
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("CsvHasHeader", value ? "true" : "false");
                RefreshDatasetPreview();
            }
        }

        public string DatasetJsonArrayPath
        {
            get => string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("JsonArrayPath", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("JsonArrayPath", value);
                RefreshDatasetPreview();
            }
        }

        public string DatasetXmlRowPath
        {
            get => string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("XmlRowPath", string.Empty)
                : string.Empty;
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("XmlRowPath", value);
                RefreshDatasetPreview();
            }
        }

        public string DatasetMaxRows
        {
            get => string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase)
                ? GetSettingValue("MaxRows", "0")
                : "0";
            set
            {
                if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                SetSettingValue("MaxRows", value);
                RefreshDatasetPreview();
            }
        }

        public DataView DatasetPreviewRows => _datasetPreviewTable.DefaultView;

        public string DatasetPreviewStatus
        {
            get => _datasetPreviewStatus;
            set
            {
                if (_datasetPreviewStatus == value)
                {
                    return;
                }

                _datasetPreviewStatus = value;
                OnPropertyChanged();
            }
        }

        private static string NormalizeSqlProvider(string? provider)
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

        private static string GetDefaultSqlAuthType(string provider)
        {
            return string.Equals(NormalizeSqlProvider(provider), "SqlServer", StringComparison.OrdinalIgnoreCase)
                ? "WindowsIntegrated"
                : "None";
        }

        private void RefreshSqlAuthTypeOptions()
        {
            _sqlAuthTypeOptions.Clear();

            var provider = string.Equals(SelectedNode?.Type, "Sql", StringComparison.OrdinalIgnoreCase)
                ? SqlProvider
                : "SqlServer";

            if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                _sqlAuthTypeOptions.Add("WindowsIntegrated");
                _sqlAuthTypeOptions.Add("None");
                _sqlAuthTypeOptions.Add("Basic");
            }
            else if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                _sqlAuthTypeOptions.Add("None");
            }
            else
            {
                _sqlAuthTypeOptions.Add("None");
                _sqlAuthTypeOptions.Add("Basic");
            }

            OnPropertyChanged(nameof(SqlAuthTypeOptions));
        }

        public string TimerDelayMs
        {
            get => GetSettingValue("DelayMs", "1000");
            set => SetSettingValue("DelayMs", value);
        }

        public string LoopIterations
        {
            get => GetSettingValue("Iterations", "1");
            set => SetSettingValue("Iterations", value);
        }

        public string IfCondition
        {
            get => GetSettingValue("Condition", string.Empty);
            set => SetSettingValue("Condition", value);
        }

        public string ThreadCount
        {
            get => GetSettingValue("ThreadCount", "1");
            set => SetSettingValue("ThreadCount", value);
        }

        public string RampUpSeconds
        {
            get => GetSettingValue("RampUpSeconds", "1");
            set => SetSettingValue("RampUpSeconds", value);
        }

        public string AssertExpected
        {
            get => GetSettingValue("Expected", string.Empty);
            set => SetSettingValue("Expected", value);
        }

        public string AssertActual
        {
            get => GetSettingValue("Actual", string.Empty);
            set => SetSettingValue("Actual", value);
        }

        public string ExtractorPattern
        {
            get => GetSettingValue("Pattern", string.Empty);
            set => SetSettingValue("Pattern", value);
        }

        public string ExtractorVariableName
        {
            get => GetSettingValue("VariableName", string.Empty);
            set => SetSettingValue("VariableName", value);
        }

        public string ScriptLanguage
        {
            get => GetSettingValue("Language", "CSharp");
            set => SetSettingValue("Language", value);
        }

        public string ScriptCode
        {
            get => GetSettingValue("Code", string.Empty);
            set => SetSettingValue("Code", value);
        }

        private Point _dragStartPoint;
        private PlanNode? _draggedNode;
        private bool _isSyncingAssertionTreeSource;
        private string _assertionTreeSource = "PreviewResponse";

        private sealed class AssertionTreeNodeTag
        {
            public string Source { get; set; } = string.Empty;
            public string Path { get; set; } = string.Empty;
            public string Expected { get; set; } = string.Empty;
            public bool IsLeaf { get; set; }
        }

        private static readonly string[] StepTypes =
        {
            "Http", "GraphQl", "Sql", "Dataset", "Assert", "VariableExtractor", "Script", "Timer"
        };

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            SizeChanged += MainWindow_SizeChanged;
            RootNodes.CollectionChanged += RootNodes_CollectionChanged;
            UpdateWindowTitle();
            RefreshSqlAuthTypeOptions();
            RefreshEnvironmentOptions();
            RebuildVariableUsageMap();
            RefreshJsonPreview();
            RefreshApiCatalogState();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadMainLayoutState();
            RestoreLastProjectIfAvailable();
            Dispatcher.BeginInvoke(new Action(EnforceMainLayoutMinimums), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            SaveMainLayoutState();
            SaveAppState();
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            EnforceMainLayoutMinimums();
        }

        private void EnforceMainLayoutMinimums()
        {
            if (_isApplyingMainLayoutBounds || MainContentGrid == null)
            {
                return;
            }

            try
            {
                _isApplyingMainLayoutBounds = true;

                const double topMin = 240;
                const double bottomMin = 80;

                var splitterHeight = 6d;
                var available = Math.Max(MainContentGrid.ActualHeight - splitterHeight, 0);
                if (available <= 0)
                {
                    return;
                }

                var desiredTop = EditorTopRow.ActualHeight;
                var desiredBottom = EditorBottomRow.ActualHeight;
                if (double.IsNaN(desiredTop) || desiredTop <= 0)
                {
                    desiredTop = topMin;
                }

                if (double.IsNaN(desiredBottom) || desiredBottom <= 0)
                {
                    desiredBottom = bottomMin;
                }

                if (available >= topMin + bottomMin)
                {
                    desiredTop = Math.Max(desiredTop, topMin);
                    desiredBottom = Math.Max(desiredBottom, bottomMin);

                    var total = desiredTop + desiredBottom;
                    if (total > available)
                    {
                        desiredTop = Math.Max(topMin, available - desiredBottom);
                        desiredBottom = Math.Max(bottomMin, available - desiredTop);
                    }
                }
                else
                {
                    // Window too small for ideal mins: keep preview visible with a protected floor.
                    var protectedBottom = Math.Max(80, available * 0.2);
                    desiredBottom = Math.Min(available, protectedBottom);
                    desiredTop = Math.Max(0, available - desiredBottom);
                }

                EditorTopRow.Height = new GridLength(Math.Max(desiredTop, 0), GridUnitType.Pixel);
                EditorBottomRow.Height = new GridLength(Math.Max(desiredBottom, 0), GridUnitType.Pixel);
            }
            finally
            {
                _isApplyingMainLayoutBounds = false;
            }
        }

        private void LoadMainLayoutState()
        {
            try
            {
                if (!File.Exists(MainLayoutStatePath))
                {
                    return;
                }

                var json = File.ReadAllText(MainLayoutStatePath);
                var state = JsonSerializer.Deserialize<MainLayoutState>(json);
                if (state == null)
                {
                    return;
                }

                ApplyPixelLength(MainLeftColumn, state.LeftColumnWidth, min: 220);
                ApplyPixelLength(LeftTopControlsRow, state.LeftTopRowHeight, min: 140);
                ApplyPixelLength(EditorTopRow, state.EditorTopRowHeight, min: 280);
                ApplyPixelLength(EditorBottomRow, state.EditorBottomRowHeight, min: 80);
                ApplyPixelLength(AssertionRowsAreaRow, state.AssertionRowsHeight, min: 100);
                ApplyPixelLength(AssertionTreeAreaRow, state.AssertionTreeHeight, min: 220);
                ApplyPixelLength(AssertionTreeLeftColumn, state.AssertionTreeLeftWidth, min: 160);
                ApplyPixelLength(AssertionTreeRightColumn, state.AssertionTreeRightWidth, min: 220);
            }
            catch
            {
                // Ignore corrupt or inaccessible layout files and continue with defaults.
            }
        }

        private void SaveMainLayoutState()
        {
            try
            {
                var state = new MainLayoutState
                {
                    LeftColumnWidth = MainLeftColumn.ActualWidth,
                    LeftTopRowHeight = LeftTopControlsRow.ActualHeight,
                    EditorTopRowHeight = EditorTopRow.ActualHeight,
                    EditorBottomRowHeight = EditorBottomRow.ActualHeight,
                    AssertionRowsHeight = AssertionRowsAreaRow.ActualHeight,
                    AssertionTreeHeight = AssertionTreeAreaRow.ActualHeight,
                    AssertionTreeLeftWidth = AssertionTreeLeftColumn.ActualWidth,
                    AssertionTreeRightWidth = AssertionTreeRightColumn.ActualWidth
                };

                var directory = Path.GetDirectoryName(MainLayoutStatePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(MainLayoutStatePath, json);
            }
            catch
            {
                // Ignore save issues to avoid interrupting app shutdown.
            }
        }

        private void RestoreLastProjectIfAvailable()
        {
            try
            {
                if (!File.Exists(AppStatePath))
                {
                    return;
                }

                var json = File.ReadAllText(AppStatePath);
                var state = JsonSerializer.Deserialize<AppState>(json);
                var lastPath = state?.LastProjectFilePath;
                if (string.IsNullOrWhiteSpace(lastPath))
                {
                    return;
                }

                if (!File.Exists(lastPath))
                {
                    MessageBox.Show(
                        $"Last project file was not found:\n{lastPath}",
                        "Last Project",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    _currentProjectFilePath = null;
                    UpdateWindowTitle();
                    SaveAppState();
                    return;
                }

                TryLoadProjectFromFile(lastPath, "Last Project");
            }
            catch
            {
                // Ignore corrupt or inaccessible app state and continue with defaults.
            }
        }

        private void SaveAppState()
        {
            try
            {
                var state = new AppState
                {
                    LastProjectFilePath = _currentProjectFilePath
                };

                var directory = Path.GetDirectoryName(AppStatePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(AppStatePath, json);
            }
            catch
            {
                // Ignore save issues to avoid interrupting app behavior.
            }
        }

        private bool TryLoadProjectFromFile(string filePath, string dialogTitle)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var model = JsonSerializer.Deserialize<ProjectFileModel>(json);

                if (model?.Project == null)
                {
                    MessageBox.Show("Invalid project file format.", dialogTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                var root = FromFileModel(model.Project, null);
                if (root.Type != "Project")
                {
                    MessageBox.Show("Root node must be of type Project.", dialogTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                RootNodes.Clear();
                RootNodes.Add(root);
                SelectedNode = root;
                _currentProjectFilePath = filePath;
                UpdateWindowTitle();
                RefreshEnvironmentOptions();
                RefreshJsonPreview();
                UpdateProjectVariablesPreview();
                SaveAppState();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load project: {ex.Message}", dialogTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static void ApplyPixelLength(ColumnDefinition definition, double value, double min)
        {
            if (definition == null || double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                return;
            }

            definition.Width = new GridLength(Math.Max(value, min), GridUnitType.Pixel);
        }

        private static void ApplyPixelLength(RowDefinition definition, double value, double min)
        {
            if (definition == null || double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                return;
            }

            definition.Height = new GridLength(Math.Max(value, min), GridUnitType.Pixel);
        }

        private void StopRunButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRunInProgress)
            {
                return;
            }

            var requested = false;
            if (_activeExecutionContext != null)
            {
                _activeExecutionContext.Status = "stopping";
                _activeExecutionContext.RequestStop();
                requested = true;
            }

            foreach (var context in _activeProjectExecutionContexts)
            {
                context.Status = "stopping";
                context.RequestStop();
                requested = true;
            }

            if (!requested)
            {
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            PreviewLogs = string.Join("\n", new[]
            {
                PreviewLogs,
                $"[{timestamp}] Stop requested by user. Finishing current step..."
            });
        }

        private void SetRunState(
            bool isRunning,
            Test_Automation.Models.ExecutionContext? context = null,
            IEnumerable<Test_Automation.Models.ExecutionContext>? contexts = null)
        {
            _isRunInProgress = isRunning;
            _activeExecutionContext = isRunning ? context : null;
            _activeProjectExecutionContexts.Clear();
            if (isRunning && contexts != null)
            {
                foreach (var runContext in contexts)
                {
                    _activeProjectExecutionContexts.Add(runContext);
                }
            }

            if (RunTestPlanButton != null)
            {
                RunTestPlanButton.IsEnabled = !isRunning;
            }

            if (RunProjectTestPlansButton != null)
            {
                RunProjectTestPlansButton.IsEnabled = !isRunning;
            }

            if (ProjectRunModeComboBox != null)
            {
                ProjectRunModeComboBox.IsEnabled = !isRunning;
            }

            if (StopRunButton != null)
            {
                StopRunButton.IsEnabled = isRunning;
            }
        }

        private void AddProjectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (RootNodes.Any(node => node.Type == "Project"))
                {
                    MessageBox.Show("Only one Project root is allowed.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var root = new PlanNode("Project", "Project");
                RootNodes.Add(root);
                SelectedNode = root;
                _currentProjectFilePath = null;
                UpdateWindowTitle();
                SaveAppState();
                RefreshEnvironmentOptions();
                RefreshJsonPreview();
                UpdateProjectVariablesPreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add project: {ex.Message}", "Add Project", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetProjectNode(out var projectNode))
            {
                MessageBox.Show("Create or load a Project first.", "Save Project", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentProjectFilePath))
            {
                SaveProjectAsButton_Click(sender, e);
                return;
            }

            try
            {
                SaveProjectToFile(_currentProjectFilePath, projectNode);
                MessageBox.Show("Project saved successfully.", "Save Project", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save project: {ex.Message}", "Save Project", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveProjectAsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetProjectNode(out var projectNode))
            {
                MessageBox.Show("Create or load a Project first.", "Save Project", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                FileName = !string.IsNullOrWhiteSpace(_currentProjectFilePath)
                    ? Path.GetFileName(_currentProjectFilePath)
                    : $"{projectNode.Name}.json"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                SaveProjectToFile(dialog.FileName, projectNode);
                _currentProjectFilePath = dialog.FileName;
                UpdateWindowTitle();
                SaveAppState();
                MessageBox.Show("Project saved successfully.", "Save Project", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save project: {ex.Message}", "Save Project", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void SaveProjectToFile(string filePath, PlanNode projectNode)
        {
            var model = new ProjectFileModel
            {
                Version = 1,
                Project = ToFileModel(projectNode)
            };

            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(filePath, json);
        }

        private bool TryGetProjectNode(out PlanNode projectNode)
        {
            var found = RootNodes.FirstOrDefault(node => node.Type == "Project");
            if (found == null)
            {
                projectNode = null!;
                return false;
            }

            projectNode = found;
            return true;
        }

        private void UpdateWindowTitle()
        {
            var suffix = string.IsNullOrWhiteSpace(_currentProjectFilePath)
                ? "Unsaved"
                : Path.GetFileName(_currentProjectFilePath);
            Title = $"Test Automation - {suffix}";
        }

        private void LoadProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            TryLoadProjectFromFile(dialog.FileName, "Load Project");
        }

        private async void RunTestPlanButton_Click(object sender, RoutedEventArgs e)
        {
            var testPlanNode = ResolveTestPlanNode();
            if (testPlanNode == null)
            {
                MessageBox.Show("Select a TestPlan node to run.", "Run TestPlan", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var testPlanComponent = BuildComponentTree(testPlanNode) as Test_Automation.Componentes.TestPlan;
            if (testPlanComponent == null)
            {
                MessageBox.Show("Unable to build the TestPlan component.", "Run TestPlan", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResetAssertionStateForSubtree(testPlanNode);

            var executor = CreateExecutorWithHighlight();
            var runner = new TestPlanRunner(executor);
            var startTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _runtimeTraceLogBuffer = string.Empty;
            PreviewLogs = $"[{startTimestamp}] Running TestPlan: {testPlanNode.Name}";
            VariablesPreview = "{}";

            try
            {
                var context = new Test_Automation.Models.ExecutionContext();
                context.ResetStopRequest();
                context.Status = "running";
                SetRunState(true, context);
                ApplyProjectVariables(context);
                var summary = await runner.RunTestPlanWithContext(testPlanComponent, context);
                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Status: {summary.Status}",
                    $"[{endTimestamp}] Total: {summary.TotalComponents}, Passed: {summary.PassedComponents}, Failed: {summary.FailedComponents}"
                });

                _lastExecutionContext = context;
                VariablesPreview = JsonSerializer.Serialize(context.Variables, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                RefreshComponentPreview();
                AppendRuntimeTraceBufferToPreviewLogs();
            }
            catch (OperationCanceledException)
            {
                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Run stopped by user."
                });
                AppendRuntimeTraceBufferToPreviewLogs();
            }
            catch (Exception ex)
            {
                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Run failed: {ex.Message}"
                });
                AppendRuntimeTraceBufferToPreviewLogs();
                VariablesPreview = "{}";
            }
            finally
            {
                SetRunState(false);
            }
        }

        private async void RunProjectTestPlansButton_Click(object sender, RoutedEventArgs e)
        {
            var testPlanNodes = GetRunnableProjectTestPlans();
            if (testPlanNodes.Count == 0)
            {
                MessageBox.Show("Add at least one enabled TestPlan under the Project.", "Run Project", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var runnablePlans = testPlanNodes
                .Select(node => (Node: node, Component: BuildComponentTree(node) as Test_Automation.Componentes.TestPlan))
                .Where(entry => entry.Component != null)
                .Select(entry => (Node: entry.Node, Component: entry.Component!))
                .ToList();

            if (runnablePlans.Count == 0)
            {
                MessageBox.Show("Unable to build enabled TestPlan components.", "Run Project", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var plan in runnablePlans)
            {
                ResetAssertionStateForSubtree(plan.Node);
            }

            var contexts = runnablePlans.Select(_ => new Test_Automation.Models.ExecutionContext()).ToList();
            for (var i = 0; i < contexts.Count; i++)
            {
                var context = contexts[i];
                context.ResetStopRequest();
                context.Status = "running";
                ApplyProjectVariables(context);
            }

            var mode = string.Equals(SelectedProjectRunMode, "Parallel", StringComparison.OrdinalIgnoreCase)
                ? "Parallel"
                : "Sequence";
            var startTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _runtimeTraceLogBuffer = string.Empty;
            PreviewLogs = $"[{startTimestamp}] Running {runnablePlans.Count} TestPlan(s) in {mode} mode.";
            VariablesPreview = "{}";

            try
            {
                SetRunState(true, null, contexts);

                var plansWithContext = runnablePlans
                    .Select((plan, index) => (plan.Node, plan.Component, Context: contexts[index]))
                    .ToList();

                var summaries = string.Equals(mode, "Parallel", StringComparison.OrdinalIgnoreCase)
                    ? await RunProjectPlansParallelAsync(plansWithContext)
                    : await RunProjectPlansSequentialAsync(plansWithContext);

                var totalComponents = summaries.Sum(entry => entry.Summary.TotalComponents);
                var passedComponents = summaries.Sum(entry => entry.Summary.PassedComponents);
                var failedComponents = summaries.Sum(entry => entry.Summary.FailedComponents);
                var anyStopped = plansWithContext.Any(entry => string.Equals(entry.Context.Status, "stopped", StringComparison.OrdinalIgnoreCase));
                var status = anyStopped
                    ? "stopped"
                    : (failedComponents > 0 ? "failed" : "passed");
                var executedPlans = summaries.Count();

                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Status: {status}",
                    $"[{endTimestamp}] Total Components: {totalComponents}, Passed: {passedComponents}, Failed: {failedComponents}",
                    $"[{endTimestamp}] TestPlans Run: {executedPlans}"
                }.Concat(summaries.Select(entry =>
                    $"[{endTimestamp}] - {entry.Node.Name}: {entry.Summary.Status} (Total: {entry.Summary.TotalComponents}, Passed: {entry.Summary.PassedComponents}, Failed: {entry.Summary.FailedComponents})")));

                var mergedVariables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var context in contexts)
                {
                    foreach (var variable in context.Variables)
                    {
                        mergedVariables[variable.Key] = variable.Value;
                    }
                }

                var mergedExecutionContext = new Test_Automation.Models.ExecutionContext
                {
                    Status = status,
                    IsRunning = false,
                    Results = contexts
                        .SelectMany(context => context.Results)
                        .OrderBy(result => result.StartTime)
                        .ToList()
                };

                foreach (var variable in mergedVariables)
                {
                    mergedExecutionContext.SetVariable(variable.Key, variable.Value);
                }

                _lastExecutionContext = mergedExecutionContext;
                VariablesPreview = JsonSerializer.Serialize(mergedVariables, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                RefreshComponentPreview();
                AppendRuntimeTraceBufferToPreviewLogs();
            }
            catch (OperationCanceledException)
            {
                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Run stopped by user."
                });
                AppendRuntimeTraceBufferToPreviewLogs();
            }
            catch (Exception ex)
            {
                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Run failed: {ex.Message}"
                });
                AppendRuntimeTraceBufferToPreviewLogs();
                VariablesPreview = "{}";
            }
            finally
            {
                SetRunState(false);
            }
        }

        private List<PlanNode> GetRunnableProjectTestPlans()
        {
            var projectNode = RootNodes.FirstOrDefault(node => node.Type == "Project");
            if (projectNode == null)
            {
                return new List<PlanNode>();
            }

            return projectNode.Children
                .Where(node => string.Equals(node.Type, "TestPlan", StringComparison.OrdinalIgnoreCase) && node.IsEnabled)
                .ToList();
        }

        private async Task<List<(PlanNode Node, ExecutionSummary Summary)>> RunProjectPlansSequentialAsync(
            IReadOnlyList<(PlanNode Node, Test_Automation.Componentes.TestPlan Component, Test_Automation.Models.ExecutionContext Context)> plans)
        {
            var summaries = new List<(PlanNode Node, ExecutionSummary Summary)>();
            for (var index = 0; index < plans.Count; index++)
            {
                var plan = plans[index];
                if (plan.Context.StopToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(plan.Context.StopToken);
                }

                var runner = new TestPlanRunner(CreateExecutorWithHighlight());
                var summary = await runner.RunTestPlanWithContext(plan.Component, plan.Context);
                summaries.Add((plan.Node, summary));

                if (string.Equals(plan.Context.Status, "stopped", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(plan.Context.Status, "stopping", StringComparison.OrdinalIgnoreCase))
                {
                    throw new OperationCanceledException(plan.Context.StopToken);
                }
            }

            return summaries;
        }

        private async Task<List<(PlanNode Node, ExecutionSummary Summary)>> RunProjectPlansParallelAsync(
            IReadOnlyList<(PlanNode Node, Test_Automation.Componentes.TestPlan Component, Test_Automation.Models.ExecutionContext Context)> plans)
        {
            var tasks = plans.Select(async plan =>
            {
                var runner = new TestPlanRunner(CreateExecutorWithHighlight());
                var summary = await runner.RunTestPlanWithContext(plan.Component, plan.Context);
                return (plan.Node, Summary: summary, plan.Context);
            });

            var results = await Task.WhenAll(tasks);
            if (results.Any(result => string.Equals(result.Context.Status, "stopped", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.Context.Status, "stopping", StringComparison.OrdinalIgnoreCase)))
            {
                throw new OperationCanceledException();
            }

            return results
                .Select(result => (result.Node, result.Summary))
                .ToList();
        }

        private void PlanTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = FindParentTreeViewItem(e.OriginalSource as DependencyObject);
            if (item == null)
            {
                return;
            }

            item.IsSelected = true;
            if (item.DataContext is not PlanNode selectedNode)
            {
                return;
            }

            item.ContextMenu = BuildContextMenuForNode(selectedNode);
        }

        private ContextMenu BuildContextMenuForNode(PlanNode selectedNode)
        {
            var menu = new ContextMenu();

            foreach (var childType in GetAllowedChildren(selectedNode.Type))
            {
                var addItem = new MenuItem { Header = $"Add {childType}" };
                addItem.Click += (_, _) => AddChildNode(selectedNode, childType);
                menu.Items.Add(addItem);
            }

            if (menu.Items.Count > 0)
            {
                menu.Items.Add(new Separator());
            }

            if (!string.Equals(selectedNode.Type, "Project", StringComparison.OrdinalIgnoreCase))
            {
                var runItem = new MenuItem { Header = "Run (This + Children)" };
                runItem.Click += async (_, _) => await RunSelectedNodeWithChildrenAsync(selectedNode);
                menu.Items.Add(runItem);
                var clearPreviewItem = new MenuItem { Header = "Clear Preview (This + Children)" };
                clearPreviewItem.Click += (_, _) => ClearSelectedNodePreviewWithChildren(selectedNode);
                menu.Items.Add(clearPreviewItem);
                menu.Items.Add(new Separator());
            }

            var cloneItem = new MenuItem { Header = "Clone" };
            cloneItem.Click += (_, _) => CloneNode(selectedNode);
            menu.Items.Add(cloneItem);

            var removeItem = new MenuItem { Header = "Delete" };
            removeItem.Click += (_, _) => RemoveNode(selectedNode);
            menu.Items.Add(removeItem);

            return menu;
        }

        private PlanNode? ResolveTestPlanNode()
        {
            if (SelectedNode != null)
            {
                var current = SelectedNode;
                while (current != null)
                {
                    if (current.Type == "TestPlan")
                    {
                        return current;
                    }

                    current = current.Parent;
                }
            }

            var project = RootNodes.FirstOrDefault(node => node.Type == "Project");
            return project?.Children.FirstOrDefault(node => node.Type == "TestPlan");
        }

        private async Task RunSelectedNodeWithChildrenAsync(PlanNode selectedNode)
        {
            if (selectedNode == null)
            {
                return;
            }

            if (string.Equals(selectedNode.Type, "Project", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Select a component node to run.", "Run Component", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var component = BuildComponentTree(selectedNode);
            if (component == null)
            {
                MessageBox.Show("Unable to build the selected component.", "Run Component", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResetAssertionStateForSubtree(selectedNode);

            var executor = CreateExecutorWithHighlight();
            var startTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _runtimeTraceLogBuffer = string.Empty;
            PreviewLogs = $"[{startTimestamp}] Running: {selectedNode.Name}";
            VariablesPreview = "{}";

            try
            {
                var context = _lastExecutionContext ?? new Test_Automation.Models.ExecutionContext();
                context.ResetStopRequest();
                context.Status = "running";
                SetRunState(true, context);
                ApplyProjectVariables(context);
                var result = await executor.ExecuteComponentTree(component, context);
                context.Results.Add(result);

                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Status: {(result.Passed ? "passed" : "failed")}",
                    $"[{endTimestamp}] Results: {context.Results.Count}"
                });

                _lastExecutionContext = context;
                VariablesPreview = JsonSerializer.Serialize(context.Variables, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                RefreshComponentPreview();
                AppendRuntimeTraceBufferToPreviewLogs();
            }
            catch (OperationCanceledException)
            {
                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Run stopped by user."
                });
                AppendRuntimeTraceBufferToPreviewLogs();
            }
            catch (Exception ex)
            {
                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Run failed: {ex.Message}"
                });
                AppendRuntimeTraceBufferToPreviewLogs();
                VariablesPreview = "{}";
            }
            finally
            {
                SetRunState(false);
            }
        }

        private void ApplyProjectVariables(Test_Automation.Models.ExecutionContext context)
        {
            var projectNode = RootNodes.FirstOrDefault(node => node.Type == "Project");
            if (projectNode == null)
            {
                return;
            }

            foreach (var variable in projectNode.Variables)
            {
                if (string.IsNullOrWhiteSpace(variable.Key))
                {
                    continue;
                }

                context.SetVariable(variable.Key, variable.Value);
            }
        }

        private void RefreshEnvironmentOptions()
        {
            if (_isRefreshingEnvironmentOptions)
            {
                return;
            }

            _isRefreshingEnvironmentOptions = true;
            _isSyncingEnvironment = true;
            try
            {
                var projectNode = RootNodes.FirstOrDefault(node => node.Type == "Project");
                var environmentText = GetProjectSettingValue(projectNode, "Environment", "dev");
                var parsedOptions = environmentText
                    .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(option => option.Trim())
                    .Where(option => !string.IsNullOrWhiteSpace(option))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (parsedOptions.Count == 0)
                {
                    parsedOptions.Add("dev");
                }

                var optionsUnchanged = EnvironmentOptions.Count == parsedOptions.Count
                    && EnvironmentOptions.Zip(parsedOptions, (current, next) => string.Equals(current, next, StringComparison.OrdinalIgnoreCase)).All(match => match);

                if (!optionsUnchanged)
                {
                    EnvironmentOptions.Clear();
                    foreach (var option in parsedOptions)
                    {
                        EnvironmentOptions.Add(option);
                    }
                }

                var envVariable = GetProjectVariableValue(projectNode, "env", string.Empty);
                var selected = _selectedEnvironment;

                if (string.IsNullOrWhiteSpace(selected)
                    || !parsedOptions.Any(option => string.Equals(option, selected, StringComparison.OrdinalIgnoreCase)))
                {
                    selected = envVariable;
                }

                if (!parsedOptions.Any(option => string.Equals(option, selected, StringComparison.OrdinalIgnoreCase)))
                {
                    selected = parsedOptions[0];
                }

                if (!string.Equals(_selectedEnvironment, selected, StringComparison.Ordinal))
                {
                    _selectedEnvironment = selected;
                    OnPropertyChanged(nameof(SelectedEnvironment));
                    OnPropertyChanged(nameof(HttpUrlResolved));
                    OnPropertyChanged(nameof(SqlConnectionResolved));
                    OnPropertyChanged(nameof(SqlQueryResolved));
                    OnPropertyChanged(nameof(DatasetResolvedSourcePath));

                    if (string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                    {
                        RefreshDatasetPreview();
                    }
                }

                if (SetProjectVariable("env", selected))
                {
                    UpdateProjectVariablesPreview();
                }
            }
            finally
            {
                _isSyncingEnvironment = false;
                _isRefreshingEnvironmentOptions = false;
            }
        }

        private string GetProjectSettingValue(PlanNode? projectNode, string key, string fallback)
        {
            if (projectNode == null)
            {
                return fallback;
            }

            var setting = projectNode.Settings.FirstOrDefault(current => string.Equals(current.Key, key, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(setting?.Value) ? fallback : setting.Value;
        }

        private string GetProjectVariableValue(PlanNode? projectNode, string key, string fallback)
        {
            if (projectNode == null)
            {
                return fallback;
            }

            var variable = projectNode.Variables.FirstOrDefault(current => string.Equals(current.Key, key, StringComparison.OrdinalIgnoreCase));
            return variable?.Value ?? fallback;
        }

        private bool SetProjectVariable(string key, string value)
        {
            var projectNode = RootNodes.FirstOrDefault(node => node.Type == "Project");
            if (projectNode == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var existing = projectNode.Variables.FirstOrDefault(variable => string.Equals(variable.Key, key, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                projectNode.Variables.Add(new NodeSetting(key, value));
                return true;
            }

            var changed = false;
            if (!string.Equals(existing.Key, key, StringComparison.Ordinal))
            {
                existing.Key = key;
                changed = true;
            }

            if (!string.Equals(existing.Value, value, StringComparison.Ordinal))
            {
                existing.Value = value;
                changed = true;
            }

            return changed;
        }

        private void UpdateProjectVariablesPreview()
        {
            var projectNode = RootNodes.FirstOrDefault(node => node.Type == "Project");
            if (projectNode == null)
            {
                VariablesPreview = "{}";
                return;
            }

            var variables = BuildDictionaryWithOverwrite(projectNode.Variables)
                .ToDictionary(entry => entry.Key, entry => (object)entry.Value, StringComparer.OrdinalIgnoreCase);

            var context = _lastExecutionContext ?? new Test_Automation.Models.ExecutionContext();
            foreach (var entry in variables)
            {
                context.SetVariable(entry.Key, entry.Value);
            }

            _lastExecutionContext = context;
            VariablesPreview = JsonSerializer.Serialize(variables, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        private ComponentExecutor CreateExecutorWithHighlight()
        {
            var executor = new ComponentExecutor();
            executor.ComponentStarted += result => _ = SetNodeHighlightAsync(result.ComponentId, true);
            executor.ComponentCompleted += result => _ = HandleComponentCompletedAsync(result);
            executor.Trace += message => _ = AppendRuntimeLogAsync(message);
            return executor;
        }

        private async Task AppendRuntimeLogAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (string.Equals(SelectedTraceLevel, "Off", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(SelectedTraceLevel, "Errors", StringComparison.OrdinalIgnoreCase)
                && !IsErrorTraceMessage(message))
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var line = $"[{now}] {message}";
                _runtimeTraceLogBuffer = string.IsNullOrWhiteSpace(_runtimeTraceLogBuffer)
                    ? line
                    : string.Join("\n", new[] { _runtimeTraceLogBuffer, line });
                if (string.IsNullOrWhiteSpace(PreviewLogs) || string.Equals(PreviewLogs, "Logs will appear here.", StringComparison.Ordinal))
                {
                    PreviewLogs = line;
                    return;
                }

                PreviewLogs = string.Join("\n", new[] { PreviewLogs, line });
            });
        }

        private static bool IsErrorTraceMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.Contains("failed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("exception", StringComparison.OrdinalIgnoreCase)
                || message.Contains("error", StringComparison.OrdinalIgnoreCase)
                || message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                || message.Contains("missing", StringComparison.OrdinalIgnoreCase);
        }

        private void AppendRuntimeTraceBufferToPreviewLogs()
        {
            if (string.IsNullOrWhiteSpace(_runtimeTraceLogBuffer))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(PreviewLogs) || string.Equals(PreviewLogs, "Logs will appear here.", StringComparison.Ordinal))
            {
                PreviewLogs = _runtimeTraceLogBuffer;
                return;
            }

            if (!PreviewLogs.Contains(_runtimeTraceLogBuffer, StringComparison.Ordinal))
            {
                PreviewLogs = string.Join("\n", new[] { PreviewLogs, _runtimeTraceLogBuffer });
            }
        }

        private async Task HandleComponentCompletedAsync(ExecutionResult result)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                var node = FindNodeById(result.ComponentId);
                if (node == null)
                {
                    return;
                }

                node.IsHighlighted = false;
                ApplyAssertionStatusesToNode(node, result);
            });
        }

        private static void ApplyAssertionStatusesToNode(PlanNode node, ExecutionResult result)
        {
            foreach (var rule in node.Assertions)
            {
                rule.LastResultState = "NotRun";
                rule.LastMessage = string.Empty;
            }

            node.AssertFailedCount = 0;
            node.ExpectFailedCount = 0;
            node.AssertionPassedCount = 0;

            if (result.AssertionResults == null || result.AssertionResults.Count == 0)
            {
                return;
            }

            foreach (var evaluation in result.AssertionResults)
            {
                if (evaluation.Index < 0 || evaluation.Index >= node.Assertions.Count)
                {
                    continue;
                }

                var rule = node.Assertions[evaluation.Index];
                rule.LastMessage = evaluation.Message;

                if (evaluation.Passed)
                {
                    rule.LastResultState = "Passed";
                    node.AssertionPassedCount++;
                    continue;
                }

                if (string.Equals(evaluation.Mode, "Expect", StringComparison.OrdinalIgnoreCase))
                {
                    rule.LastResultState = "ExpectFailed";
                    node.ExpectFailedCount++;
                }
                else
                {
                    rule.LastResultState = "AssertFailed";
                    node.AssertFailedCount++;
                }
            }
        }

        private static void ResetAssertionStateForSubtree(PlanNode node)
        {
            node.AssertFailedCount = 0;
            node.ExpectFailedCount = 0;
            node.AssertionPassedCount = 0;
            foreach (var rule in node.Assertions)
            {
                rule.LastResultState = "NotRun";
                rule.LastMessage = string.Empty;
            }

            foreach (var child in node.Children)
            {
                ResetAssertionStateForSubtree(child);
            }
        }

        private async Task SetNodeHighlightAsync(string componentId, bool isHighlighted)
        {
            if (string.IsNullOrWhiteSpace(componentId))
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                var node = FindNodeById(componentId);
                if (node == null)
                {
                    return;
                }

                node.IsHighlighted = isHighlighted;
            });
        }

        private PlanNode? FindNodeById(string componentId)
        {
            foreach (var root in RootNodes)
            {
                var match = FindNodeById(root, componentId);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static PlanNode? FindNodeById(PlanNode node, string componentId)
        {
            if (string.Equals(node.Id, componentId, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                var match = FindNodeById(child, componentId);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private Test_Automation.Componentes.Component? BuildComponentTree(PlanNode node)
        {
            if (node == null || !node.IsEnabled || node.Type == "Project")
            {
                return null;
            }

            var component = ComponentFactory.CreateComponent(node.Type);
            component.SetName(node.Name);
            component.SetId(node.Id);

            var settings = new Dictionary<string, string>();
            foreach (var setting in node.Settings)
            {
                if (string.IsNullOrWhiteSpace(setting.Key))
                {
                    continue;
                }

                settings[setting.Key] = setting.Value;
            }

            component.Settings = settings;

            var extractors = node.Extractors
                .Select(rule => new VariableExtractionRule(rule.Source, rule.JsonPath, rule.VariableName))
                .ToList();

            component.Extractors = extractors;

            var assertions = node.Assertions
                .Select(rule => new AssertionRule(rule.Source, rule.JsonPath, rule.Condition, rule.Expected, rule.Mode))
                .ToList();

            component.Assertions = assertions;

            foreach (var child in node.Children)
            {
                var childComponent = BuildComponentTree(child);
                if (childComponent != null)
                {
                    component.AddChild(childComponent);
                }
            }

            return component;
        }

        private static string[] GetAllowedChildren(string parentType)
        {
            if (parentType == "Project")
            {
                return new[] { "TestPlan" };
            }

            if (parentType == "TestPlan")
            {
                return new[] { "Threads" };
            }

            if (parentType == "Threads" || parentType == "If" || parentType == "Loop" || parentType == "Foreach")
            {
                return new[] { "Config", "If", "Loop", "Foreach" }.Concat(StepTypes).ToArray();
            }

            return Array.Empty<string>();
        }

        private void AddChildNode(PlanNode parent, string childType)
        {
            var child = new PlanNode(childType, childType) { Parent = parent };
            parent.Children.Add(child);
            SelectedNode = child;
            RefreshJsonPreview();
        }

        private void RemoveNode(PlanNode node)
        {
            if (node.Parent == null)
            {
                RootNodes.Remove(node);
                if (ReferenceEquals(SelectedNode, node))
                {
                    SelectedNode = null;
                }
                RefreshJsonPreview();
                return;
            }

            node.Parent.Children.Remove(node);
            if (ReferenceEquals(SelectedNode, node))
            {
                SelectedNode = null;
            }
            RefreshJsonPreview();
        }

        private void CloneNode(PlanNode sourceNode)
        {
            var clonedNode = DeepCloneNode(sourceNode);

            if (sourceNode.Parent == null)
            {
                if (clonedNode.Type == "Project" && RootNodes.Any(node => node.Type == "Project"))
                {
                    MessageBox.Show("Only one Project root is allowed.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var sourceIndex = RootNodes.IndexOf(sourceNode);
                var insertAt = sourceIndex >= 0 ? sourceIndex + 1 : RootNodes.Count;
                RootNodes.Insert(insertAt, clonedNode);
                SelectedNode = clonedNode;
                RefreshJsonPreview();
                return;
            }

            clonedNode.Parent = sourceNode.Parent;
            var siblings = sourceNode.Parent.Children;
            var currentIndex = siblings.IndexOf(sourceNode);
            var targetIndex = currentIndex >= 0 ? currentIndex + 1 : siblings.Count;
            siblings.Insert(targetIndex, clonedNode);
            SelectedNode = clonedNode;
            RefreshJsonPreview();
        }

        private PlanNode DeepCloneNode(PlanNode source)
        {
            var copy = new PlanNode(source.Type, source.Name)
            {
                IsEnabled = source.IsEnabled
            };

            copy.Settings.Clear();
            foreach (var setting in source.Settings)
            {
                copy.Settings.Add(new NodeSetting(setting.Key, setting.Value));
            }

            copy.Variables.Clear();
            foreach (var variable in source.Variables)
            {
                copy.Variables.Add(new NodeSetting(variable.Key, variable.Value));
            }

            copy.Extractors.Clear();
            foreach (var extractor in source.Extractors)
            {
                copy.Extractors.Add(new VariableExtractionRule(extractor.Source, extractor.JsonPath, extractor.VariableName));
            }

            copy.Assertions.Clear();
            foreach (var assertion in source.Assertions)
            {
                copy.Assertions.Add(new AssertionRule(assertion.Source, assertion.JsonPath, assertion.Condition, assertion.Expected, assertion.Mode));
            }

            foreach (var child in source.Children)
            {
                var clonedChild = DeepCloneNode(child);
                clonedChild.Parent = copy;
                copy.Children.Add(clonedChild);
            }

            return copy;
        }

        private void PlanTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            SelectedNode = e.NewValue as PlanNode;
        }

        private void AddSettingButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null)
            {
                return;
            }

            var setting = new NodeSetting("Key", "Value");
            SelectedNode.Settings.Add(setting);
            RefreshJsonPreview();
        }

        private void RemoveSettingButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || sender is not Button button || button.DataContext is not NodeSetting setting)
            {
                return;
            }

            SelectedNode.Settings.Remove(setting);
            RefreshJsonPreview();
        }

        private void AddProjectVariableButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || SelectedNode.Type != "Project")
            {
                return;
            }

            SelectedNode.Variables.Add(new NodeSetting("var", "Value"));
            RefreshJsonPreview();
        }

        private void RemoveProjectVariableButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || SelectedNode.Type != "Project" || sender is not Button button || button.DataContext is not NodeSetting setting)
            {
                return;
            }

            SelectedNode.Variables.Remove(setting);
            RefreshJsonPreview();
        }

        private void AddProjectUrlBaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || !string.Equals(SelectedNode.Type, "Project", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var entry = new ApiCatalogBaseUrlEntry
            {
                Name = "New Base URL",
                BaseUrl = "https://api.example.com"
            };

            RegisterApiCatalogBaseEntry(entry);
            _projectUrlCatalogEntries.Add(entry);
            SyncApiCatalogSettingFromEditor();
        }

        private void RemoveProjectUrlBaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || !string.Equals(SelectedNode.Type, "Project", StringComparison.OrdinalIgnoreCase)
                || sender is not Button button || button.DataContext is not ApiCatalogBaseUrlEntry entry)
            {
                return;
            }

            UnregisterApiCatalogBaseEntry(entry);
            _projectUrlCatalogEntries.Remove(entry);
            SyncApiCatalogSettingFromEditor();
        }

        private void AddProjectUrlEndpointButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || !string.Equals(SelectedNode.Type, "Project", StringComparison.OrdinalIgnoreCase)
                || sender is not Button button || button.DataContext is not ApiCatalogBaseUrlEntry entry)
            {
                return;
            }

            var endpoint = new ApiCatalogEndpointEntry
            {
                Name = "New Endpoint",
                Path = "/resource",
                Method = "GET",
                Variables = "{}"
            };

            entry.Endpoints.Add(endpoint);
        }

        private void RemoveProjectUrlEndpointButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || !string.Equals(SelectedNode.Type, "Project", StringComparison.OrdinalIgnoreCase)
                || sender is not Button button || button.DataContext is not ApiCatalogEndpointEntry endpoint)
            {
                return;
            }

            var owner = _projectUrlCatalogEntries.FirstOrDefault(entry => entry.Endpoints.Contains(endpoint));
            if (owner == null)
            {
                return;
            }

            owner.Endpoints.Remove(endpoint);
        }

        private void BrowseDatasetFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Select Dataset File",
                Filter = "All supported|*.xlsx;*.xlsm;*.xls;*.csv;*.json;*.xml|Excel|*.xlsx;*.xlsm;*.xls|CSV|*.csv|JSON|*.json|XML|*.xml|All files|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            DatasetSourcePath = dialog.FileName;
            RefreshDatasetPreview();
        }

        private void RefreshDatasetPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshDatasetPreview();
        }

        private void RefreshDatasetPreview()
        {
            _datasetPreviewTable = new DataTable("DatasetPreview");

            if (!string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
            {
                DatasetPreviewStatus = "Select a Dataset component to preview rows.";
                OnPropertyChanged(nameof(DatasetPreviewRows));
                return;
            }

            var resolvedSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Format"] = DatasetFormat,
                ["SourcePath"] = ResolveWithProjectVariables(DatasetSourcePath),
                ["SheetName"] = ResolveWithProjectVariables(DatasetSheetName),
                ["CsvDelimiter"] = ResolveWithProjectVariables(DatasetCsvDelimiter),
                ["CsvHasHeader"] = DatasetCsvHasHeader ? "true" : "false",
                ["JsonArrayPath"] = ResolveWithProjectVariables(DatasetJsonArrayPath),
                ["XmlRowPath"] = ResolveWithProjectVariables(DatasetXmlRowPath),
                ["MaxRows"] = ResolveWithProjectVariables(DatasetMaxRows)
            };

            try
            {
                var rows = Test_Automation.Componentes.Dataset.LoadRows(resolvedSettings);
                var columnNames = rows
                    .SelectMany(row => row.Keys)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (columnNames.Count == 0)
                {
                    _datasetPreviewTable.Columns.Add("NoData", typeof(string));
                    var infoRow = _datasetPreviewTable.NewRow();
                    infoRow["NoData"] = "No rows were loaded from source.";
                    _datasetPreviewTable.Rows.Add(infoRow);
                    DatasetPreviewStatus = $"Loaded 0 row(s) from {resolvedSettings["SourcePath"]}.";
                    OnPropertyChanged(nameof(DatasetPreviewRows));
                    return;
                }

                foreach (var columnName in columnNames)
                {
                    _datasetPreviewTable.Columns.Add(columnName, typeof(string));
                }

                foreach (var row in rows)
                {
                    var tableRow = _datasetPreviewTable.NewRow();
                    foreach (var columnName in columnNames)
                    {
                        tableRow[columnName] = row.TryGetValue(columnName, out var value)
                            ? (value?.ToString() ?? string.Empty)
                            : string.Empty;
                    }

                    _datasetPreviewTable.Rows.Add(tableRow);
                }

                DatasetPreviewStatus = $"Loaded {rows.Count} row(s) from {resolvedSettings["SourcePath"]}.";
            }
            catch (Exception ex)
            {
                DatasetPreviewStatus = $"Dataset preview failed: {ex.Message}";
            }

            OnPropertyChanged(nameof(DatasetPreviewRows));
        }

        private void DatasetPreviewGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            // Use indexer binding so keys like "data.year" are treated as literal column names.
            if (e.Column is DataGridTextColumn textColumn)
            {
                textColumn.Binding = new Binding($"[{e.PropertyName}]");
            }
        }

        private void AddEndpointParameterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: ApiCatalogEndpointEntry endpoint })
            {
                return;
            }

            endpoint.Parameters.Add(new ApiCatalogParameterEntry());
        }

        private void RemoveEndpointParameterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: ApiCatalogParameterEntry param })
            {
                return;
            }

            foreach (var baseEntry in _projectUrlCatalogEntries)
            {
                foreach (var ep in baseEntry.Endpoints)
                {
                    if (ep.Parameters.Contains(param))
                    {
                        ep.Parameters.Remove(param);
                        return;
                    }
                }
            }
        }

        private void ApplyHttpCatalogSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "Http", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedHttpCatalogBase) || string.IsNullOrWhiteSpace(SelectedHttpCatalogEndpoint))
            {
                MessageBox.Show("Choose both Base URL and Endpoint before applying.", "Apply Catalog", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ApplySelectedCatalogEndpointToCurrentNode();
        }

        private void ClearHttpCatalogSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "Http", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _isApplyingCatalogSelection = true;
            try
            {
                SetSettingValue("CatalogBase", string.Empty);
                SetSettingValue("CatalogEndpoint", string.Empty);
            }
            finally
            {
                _isApplyingCatalogSelection = false;
            }

            RefreshCatalogEndpointOptions();
            OnPropertyChanged(nameof(SelectedHttpCatalogBase));
            OnPropertyChanged(nameof(SelectedHttpCatalogEndpoint));
        }

        private void ApplyGraphQlCatalogSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "GraphQl", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedGraphQlCatalogBase) || string.IsNullOrWhiteSpace(SelectedGraphQlCatalogEndpoint))
            {
                MessageBox.Show("Choose both Base URL and Endpoint before applying.", "Apply Catalog", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ApplySelectedCatalogEndpointToCurrentNode();
        }

        private void ClearGraphQlCatalogSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.Equals(SelectedNode?.Type, "GraphQl", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _isApplyingCatalogSelection = true;
            try
            {
                SetSettingValue("CatalogBase", string.Empty);
                SetSettingValue("CatalogEndpoint", string.Empty);
            }
            finally
            {
                _isApplyingCatalogSelection = false;
            }

            RefreshCatalogEndpointOptions();
            OnPropertyChanged(nameof(SelectedGraphQlCatalogBase));
            OnPropertyChanged(nameof(SelectedGraphQlCatalogEndpoint));
        }

        private void PlanTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            var item = FindParentTreeViewItem(e.OriginalSource as DependencyObject);
            _draggedNode = item?.DataContext as PlanNode;
        }

        private void PlanTreeView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedNode == null)
            {
                return;
            }

            var mousePos = e.GetPosition(null);
            var diff = _dragStartPoint - mousePos;

            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            DragDrop.DoDragDrop(PlanTreeView, new DataObject(typeof(PlanNode), _draggedNode), DragDropEffects.Move);
            _draggedNode = null;
        }

        private void PlanTreeView_DragOver(object sender, DragEventArgs e)
        {
            if (!TryGetDragNodes(e, out var sourceNode, out var targetNode))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var canReorder = CanReorder(sourceNode, targetNode);
            var canMove = CanMoveToParent(sourceNode, targetNode) || CanMoveToSiblingParent(sourceNode, targetNode);
            e.Effects = (canReorder || canMove) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void PlanTreeView_Drop(object sender, DragEventArgs e)
        {
            if (!TryGetDragNodes(e, out var sourceNode, out var targetNode))
            {
                e.Handled = true;
                return;
            }

            if (!CanReorder(sourceNode, targetNode))
            {
                if (TryMoveToParent(sourceNode, targetNode))
                {
                    e.Handled = true;
                    RefreshJsonPreview();
                    return;
                }

                if (TryMoveToSiblingParent(sourceNode, targetNode))
                {
                    e.Handled = true;
                    RefreshJsonPreview();
                    return;
                }

                e.Handled = true;
                return;
            }

            var sourceParent = sourceNode.Parent;

            if (sourceParent == null)
            {
                ReorderInCollection(RootNodes, sourceNode, targetNode);
                e.Handled = true;
                RefreshJsonPreview();
                return;
            }

            ReorderInCollection(sourceParent.Children, sourceNode, targetNode);
            e.Handled = true;
            RefreshJsonPreview();
        }

        private void TreeViewItem_DragEnter(object sender, DragEventArgs e)
        {
            UpdateDragOverVisual(sender, e, true);
        }

        private void TreeViewItem_DragOver(object sender, DragEventArgs e)
        {
            UpdateDragOverVisual(sender, e, false);
        }

        private void TreeViewItem_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                item.ClearValue(Control.BackgroundProperty);
            }
        }

        private void TreeViewItem_Drop(object sender, DragEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                item.ClearValue(Control.BackgroundProperty);
            }
        }

        private void UpdateDragOverVisual(object sender, DragEventArgs e, bool autoExpand)
        {
            if (sender is not TreeViewItem item)
            {
                return;
            }

            if (item.DataContext is not PlanNode targetNode)
            {
                return;
            }

            if (!TryGetDragSource(e, out var sourceNode))
            {
                item.ClearValue(Control.BackgroundProperty);
                return;
            }

            var canReorder = CanReorder(sourceNode, targetNode);
            var canMove = CanMoveToParent(sourceNode, targetNode) || CanMoveToSiblingParent(sourceNode, targetNode);

            if (canReorder || canMove)
            {
                item.Background = Brushes.LightGreen;
                if (autoExpand && !item.IsExpanded)
                {
                    item.IsExpanded = true;
                }
            }
            else
            {
                item.Background = Brushes.LightCoral;
            }
        }

        private bool TryGetDragSource(DragEventArgs e, out PlanNode sourceNode)
        {
            sourceNode = null!;

            if (!e.Data.GetDataPresent(typeof(PlanNode)))
            {
                return false;
            }

            sourceNode = e.Data.GetData(typeof(PlanNode)) as PlanNode ?? null!;
            return sourceNode != null;
        }

        private bool TryGetDragNodes(DragEventArgs e, out PlanNode sourceNode, out PlanNode targetNode)
        {
            sourceNode = null!;
            targetNode = null!;

            if (!e.Data.GetDataPresent(typeof(PlanNode)))
            {
                return false;
            }

            var source = e.Data.GetData(typeof(PlanNode)) as PlanNode;
            if (source == null)
            {
                return false;
            }

            var position = e.GetPosition(PlanTreeView);
            var target = GetNodeAtPosition(position);
            if (target == null)
            {
                return false;
            }

            sourceNode = source;
            targetNode = target;
            return true;
        }

        private bool CanReorder(PlanNode sourceNode, PlanNode targetNode)
        {
            if (ReferenceEquals(sourceNode, targetNode))
            {
                return false;
            }

            return ReferenceEquals(sourceNode.Parent, targetNode.Parent);
        }

        private bool CanMoveToParent(PlanNode sourceNode, PlanNode targetNode)
        {
            if (ReferenceEquals(sourceNode, targetNode))
            {
                return false;
            }

            if (!CanAcceptChild(targetNode, sourceNode))
            {
                return false;
            }

            return !IsDescendant(targetNode, sourceNode);
        }

        private bool CanMoveToSiblingParent(PlanNode sourceNode, PlanNode targetNode)
        {
            if (targetNode.Parent == null)
            {
                return false;
            }

            if (!CanAcceptChild(targetNode.Parent, sourceNode))
            {
                return false;
            }

            return !IsDescendant(targetNode.Parent, sourceNode);
        }

        private bool TryMoveToParent(PlanNode sourceNode, PlanNode targetNode)
        {
            if (!CanMoveToParent(sourceNode, targetNode))
            {
                return false;
            }

            RemoveFromParent(sourceNode);
            sourceNode.Parent = targetNode;
            targetNode.Children.Add(sourceNode);
            return true;
        }

        private bool TryMoveToSiblingParent(PlanNode sourceNode, PlanNode targetNode)
        {
            var newParent = targetNode.Parent;
            if (newParent == null || !CanMoveToSiblingParent(sourceNode, targetNode))
            {
                return false;
            }

            RemoveFromParent(sourceNode);
            sourceNode.Parent = newParent;
            var insertIndex = newParent.Children.IndexOf(targetNode);
            if (insertIndex < 0)
            {
                newParent.Children.Add(sourceNode);
            }
            else
            {
                newParent.Children.Insert(insertIndex, sourceNode);
            }

            return true;
        }

        private void RemoveFromParent(PlanNode node)
        {
            if (node.Parent == null)
            {
                RootNodes.Remove(node);
                return;
            }

            node.Parent.Children.Remove(node);
        }

        private static bool CanAcceptChild(PlanNode parent, PlanNode child)
        {
            return GetAllowedChildren(parent.Type)
                .Any(type => string.Equals(type, child.Type, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsDescendant(PlanNode potentialChild, PlanNode potentialAncestor)
        {
            var current = potentialChild.Parent;
            while (current != null)
            {
                if (ReferenceEquals(current, potentialAncestor))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        private PlanNode? GetNodeAtPosition(Point position)
        {
            var hit = PlanTreeView.InputHitTest(position) as DependencyObject;
            var targetItem = FindParentTreeViewItem(hit);
            return targetItem?.DataContext as PlanNode;
        }

        private static void ReorderInCollection(ObservableCollection<PlanNode> collection, PlanNode sourceNode, PlanNode targetNode)
        {
            var oldIndex = collection.IndexOf(sourceNode);
            var newIndex = collection.IndexOf(targetNode);
            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
            {
                return;
            }

            collection.Move(oldIndex, newIndex);
        }

        private static TreeViewItem? FindParentTreeViewItem(DependencyObject? child)
        {
            while (child != null)
            {
                if (child is TreeViewItem item)
                {
                    return item;
                }

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }

        private void RegisterNode(PlanNode node)
        {
            node.PropertyChanged += PlanNode_PropertyChanged;
            node.Children.CollectionChanged += NodeChildren_CollectionChanged;
            node.Settings.CollectionChanged += NodeSettings_CollectionChanged;
            node.Variables.CollectionChanged += NodeVariables_CollectionChanged;
            node.Extractors.CollectionChanged += NodeExtractors_CollectionChanged;
            node.Assertions.CollectionChanged += NodeAssertions_CollectionChanged;

            foreach (var setting in node.Settings)
            {
                setting.PropertyChanged += NodeSetting_PropertyChanged;
            }

            foreach (var variable in node.Variables)
            {
                variable.PropertyChanged += NodeVariableSetting_PropertyChanged;
            }

            foreach (var extractor in node.Extractors)
            {
                extractor.PropertyChanged += NodeExtractor_PropertyChanged;
            }

            foreach (var assertion in node.Assertions)
            {
                assertion.PropertyChanged += NodeAssertion_PropertyChanged;
            }
        }

        private void UnregisterNode(PlanNode node)
        {
            node.PropertyChanged -= PlanNode_PropertyChanged;
            node.Children.CollectionChanged -= NodeChildren_CollectionChanged;
            node.Settings.CollectionChanged -= NodeSettings_CollectionChanged;
            node.Variables.CollectionChanged -= NodeVariables_CollectionChanged;
            node.Extractors.CollectionChanged -= NodeExtractors_CollectionChanged;
            node.Assertions.CollectionChanged -= NodeAssertions_CollectionChanged;

            foreach (var setting in node.Settings)
            {
                setting.PropertyChanged -= NodeSetting_PropertyChanged;
            }

            foreach (var variable in node.Variables)
            {
                variable.PropertyChanged -= NodeVariableSetting_PropertyChanged;
            }

            foreach (var extractor in node.Extractors)
            {
                extractor.PropertyChanged -= NodeExtractor_PropertyChanged;
            }

            foreach (var assertion in node.Assertions)
            {
                assertion.PropertyChanged -= NodeAssertion_PropertyChanged;
            }

            foreach (var child in node.Children)
            {
                UnregisterNode(child);
            }
        }

        private void RootNodes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<PlanNode>())
                {
                    UnregisterNode(item);
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<PlanNode>())
                {
                    RegisterNode(item);
                }
            }

            RefreshJsonPreview();
            RefreshEnvironmentOptions();
            RefreshApiCatalogState();
            RebuildVariableUsageMap();
        }

        private void NodeChildren_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<PlanNode>())
                {
                    UnregisterNode(item);
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<PlanNode>())
                {
                    RegisterNode(item);
                }
            }

            RefreshJsonPreview();
            RebuildVariableUsageMap();
        }

        private void NodeSettings_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<NodeSetting>())
                {
                    item.PropertyChanged -= NodeSetting_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<NodeSetting>())
                {
                    item.PropertyChanged += NodeSetting_PropertyChanged;
                }
            }

            RebuildExtractorSourceOptions();
            RefreshJsonPreview();
            RefreshEnvironmentOptions();
            RefreshApiCatalogState();
            RebuildVariableUsageMap();
        }

        private void NodeVariables_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            var node = FindNodeByVariablesCollection(sender);
            if (node != null)
            {
                NormalizeDuplicateVariables(node);
            }

            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<NodeSetting>())
                {
                    item.PropertyChanged -= NodeVariableSetting_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<NodeSetting>())
                {
                    item.PropertyChanged += NodeVariableSetting_PropertyChanged;
                }
            }

            RefreshJsonPreview();
            if (node != null && string.Equals(node.Type, "Project", StringComparison.OrdinalIgnoreCase))
            {
                UpdateProjectVariablesPreview();
                RefreshEnvironmentOptions();
                OnPropertyChanged(nameof(HttpUrlResolved));
                OnPropertyChanged(nameof(SqlConnectionResolved));
                OnPropertyChanged(nameof(SqlQueryResolved));
                OnPropertyChanged(nameof(DatasetResolvedSourcePath));

                if (string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    RefreshDatasetPreview();
                }
            }
            RebuildVariableUsageMap();
            OnPropertyChanged(nameof(ProjectVariablesForEditor));
        }

        private void NodeExtractors_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<VariableExtractionRule>())
                {
                    item.PropertyChanged -= NodeExtractor_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<VariableExtractionRule>())
                {
                    item.PropertyChanged += NodeExtractor_PropertyChanged;
                }
            }

            RefreshJsonPreview();
            RebuildVariableUsageMap();
        }

        private void NodeAssertions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<AssertionRule>())
                {
                    item.PropertyChanged -= NodeAssertion_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<AssertionRule>())
                {
                    item.PropertyChanged += NodeAssertion_PropertyChanged;
                }
            }

            RefreshJsonPreview();
        }

        private void PlanNode_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifySelectedNodeEditorProperties();
            RefreshJsonPreview();
            RebuildVariableUsageMap();
        }

        private void NodeSetting_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifySelectedNodeEditorProperties();
            RebuildExtractorSourceOptions();
            RefreshJsonPreview();

            var shouldRebuildVariableMap = false;
            if (sender is NodeSetting changedSetting)
            {
                var ownerNode = RootNodes
                    .Select(root => FindNodeContainingSetting(root, changedSetting))
                    .FirstOrDefault(found => found != null);

                if (ownerNode != null
                    && string.Equals(ownerNode.Type, "Project", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(changedSetting.Key, "Environment", StringComparison.OrdinalIgnoreCase))
                {
                    RefreshEnvironmentOptions();
                }

                shouldRebuildVariableMap = e.PropertyName == nameof(NodeSetting.Key)
                    || (e.PropertyName == nameof(NodeSetting.Value) && IsVariableSettingKey(changedSetting.Key));

                if (ownerNode != null
                    && string.Equals(ownerNode.Type, "Project", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(changedSetting.Key, "UrlCatalog", StringComparison.OrdinalIgnoreCase))
                {
                    RefreshApiCatalogState();
                }

                if (ownerNode != null
                    && ReferenceEquals(ownerNode, SelectedNode)
                    && string.Equals(ownerNode.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(changedSetting.Key, "SourcePath", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "Format", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "SheetName", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "CsvDelimiter", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "CsvHasHeader", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "JsonArrayPath", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "XmlRowPath", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "MaxRows", StringComparison.OrdinalIgnoreCase))
                    {
                        RefreshDatasetPreview();
                    }
                }

                if (ownerNode != null
                    && ReferenceEquals(ownerNode, SelectedNode)
                    && (string.Equals(changedSetting.Key, "CatalogBase", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "CatalogEndpoint", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(changedSetting.Key, "UrlCatalog", StringComparison.OrdinalIgnoreCase)))
                {
                    SyncCatalogSelectionFromCurrentNode();
                }
            }

            if (shouldRebuildVariableMap)
            {
                RebuildVariableUsageMap();
            }
        }

        private void NodeVariableSetting_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            var shouldRebuildVariableMap = false;
            PlanNode? ownerNode = null;
            if (sender is NodeSetting variable)
            {
                ownerNode = RootNodes
                    .Select(root => FindNodeContainingVariable(root, variable))
                    .FirstOrDefault(found => found != null);

                if (ownerNode != null)
                {
                    if (e.PropertyName == nameof(NodeSetting.Key))
                    {
                        NormalizeDuplicateVariables(ownerNode, variable);
                        shouldRebuildVariableMap = true;
                    }
                }
            }

            RefreshJsonPreview();
            if (ownerNode != null && string.Equals(ownerNode.Type, "Project", StringComparison.OrdinalIgnoreCase))
            {
                UpdateProjectVariablesPreview();
                OnPropertyChanged(nameof(HttpUrlResolved));
                OnPropertyChanged(nameof(SqlConnectionResolved));
                OnPropertyChanged(nameof(SqlQueryResolved));
                OnPropertyChanged(nameof(DatasetResolvedSourcePath));

                if (string.Equals(SelectedNode?.Type, "Dataset", StringComparison.OrdinalIgnoreCase))
                {
                    RefreshDatasetPreview();
                }
                if (string.Equals((sender as NodeSetting)?.Key, "env", StringComparison.OrdinalIgnoreCase)
                    || e.PropertyName == nameof(NodeSetting.Key))
                {
                    RefreshEnvironmentOptions();
                }
            }

            if (shouldRebuildVariableMap)
            {
                RebuildVariableUsageMap();
            }

            OnPropertyChanged(nameof(ProjectVariablesForEditor));
        }

        private void NodeExtractor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifySelectedNodeEditorProperties();
            RefreshJsonPreview();
            RebuildVariableUsageMap();
        }

        private void NodeAssertion_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifySelectedNodeEditorProperties();

            var affectsPersistence = e.PropertyName == nameof(AssertionRule.Source)
                || e.PropertyName == nameof(AssertionRule.JsonPath)
                || e.PropertyName == nameof(AssertionRule.Mode)
                || e.PropertyName == nameof(AssertionRule.Condition)
                || e.PropertyName == nameof(AssertionRule.Expected);

            if (affectsPersistence && sender is AssertionRule rule)
            {
                rule.LastResultState = "NotRun";
                rule.LastMessage = "Rule changed. Run component to get fresh status.";
            }

            if (affectsPersistence)
            {
                RefreshJsonPreview();
            }
        }

        private void RefreshJsonPreview()
        {
            var model = RootNodes.Select(BuildNodeObject).ToList();
            JsonPreview = JsonSerializer.Serialize(model, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            RefreshComponentPreview();
            RefreshAssertionJsonTreePanel();
        }

        private void RefreshComponentPreview()
        {
            if (SelectedNode == null)
            {
                PreviewRequest = "Select a component to see request preview.";
                PreviewResponse = "Select a component to see response preview.";
                PreviewLogs = "Logs will appear here.";
                AssertionPreview = "Select a component to see assertion preview.";
                ResetHttpDetailPreviews();
                return;
            }

            var nodeType = SelectedNode.Type;
            var nodeId = SelectedNode.Id;
            var nodeName = SelectedNode.Name;
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var nodeExecutionResults = FilterPreviewResults(GetExecutionResults(nodeId), lastPerComponent: false);

            if (!string.Equals(nodeType, "Http", StringComparison.OrdinalIgnoreCase))
            {
                ResetHttpDetailPreviews();
            }

            if (string.Equals(nodeType, "Project", StringComparison.OrdinalIgnoreCase))
            {
                PopulateProjectPreview(now, nodeName);
                return;
            }

            if (string.Equals(nodeType, "TestPlan", StringComparison.OrdinalIgnoreCase))
            {
                PopulateTestPlanPreview(now, nodeName);
                return;
            }

            var scopedResults = FilterPreviewResults(GetExecutionResultsForScope(SelectedNode), lastPerComponent: true);
            SetAssertionPreview(nodeType, nodeName, scopedResults);

            if (nodeType == "Http")
            {
                var method = GetSettingValue("Method", "GET");
                var url = ResolveWithProjectVariables(GetSettingValue("Url", "https://api.example.com"));
                var latestHttpExecution = nodeExecutionResults
                    .OrderByDescending(result => result.EndTime ?? result.StartTime)
                    .FirstOrDefault();
                var lastHttp = GetLastExecutionData<HttpData>(nodeId);
                var httpRequestRuns = nodeExecutionResults
                    .Where(result => result.Data is HttpData)
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        method = (result.Data as HttpData)?.Method,
                        url = (result.Data as HttpData)?.Url,
                        headers = (result.Data as HttpData)?.Headers,
                        body = (result.Data as HttpData)?.Body
                    })
                    .ToList();
                var httpRuns = nodeExecutionResults
                    .Where(result => result.Data is HttpData)
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        responseStatus = (result.Data as HttpData)?.ResponseStatus,
                        responseBody = (result.Data as HttpData)?.ResponseBody
                    })
                    .ToList();

                if (latestHttpExecution != null
                    && latestHttpExecution.Data is not HttpData
                    && string.Equals(latestHttpExecution.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    HttpRequestHeadersPreview = "{}";
                    HttpRequestCookiesPreview = "[]";
                    HttpRequestMetadataPreview = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        method,
                        url,
                        status = latestHttpExecution.Status,
                        durationMs = latestHttpExecution.DurationMs,
                        threadIndex = latestHttpExecution.ThreadIndex,
                        startTime = latestHttpExecution.StartTime,
                        endTime = latestHttpExecution.EndTime,
                        error = latestHttpExecution.Error
                    }, new JsonSerializerOptions { WriteIndented = true });

                    HttpResponseHeadersPreview = "{}";
                    HttpResponseCookiesPreview = "[]";
                    HttpResponseMetadataPreview = JsonSerializer.Serialize(new
                    {
                        status = latestHttpExecution.Status,
                        error = latestHttpExecution.Error,
                        message = "Request failed before receiving an HTTP response.",
                        durationMs = latestHttpExecution.DurationMs,
                        threadIndex = latestHttpExecution.ThreadIndex
                    }, new JsonSerializerOptions { WriteIndented = true });

                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        type = "Http",
                        method,
                        url,
                        status = latestHttpExecution.Status,
                        error = latestHttpExecution.Error,
                        threadIndex = latestHttpExecution.ThreadIndex,
                        durationMs = latestHttpExecution.DurationMs
                    }, new JsonSerializerOptions { WriteIndented = true });

                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        status = latestHttpExecution.Status,
                        error = latestHttpExecution.Error,
                        message = "Request failed before receiving an HTTP response."
                    }, new JsonSerializerOptions { WriteIndented = true });

                    PreviewLogs = $"[{now}] HTTP preview refreshed\n[{now}] Target: {method} {url}\n[{now}] Last run failed: {latestHttpExecution.Error}";
                    AppendExtractionPreview(now);
                    return;
                }

                var latestHttpData = latestHttpExecution?.Data as HttpData;
                var requestHeaders = latestHttpData?.Headers ?? BuildRequestHeadersFromSettings();
                HttpRequestHeadersPreview = JsonSerializer.Serialize(requestHeaders, new JsonSerializerOptions { WriteIndented = true });
                HttpRequestCookiesPreview = JsonSerializer.Serialize(ExtractCookiesFromHeaders(requestHeaders, "Cookie"), new JsonSerializerOptions { WriteIndented = true });
                HttpRequestMetadataPreview = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    method,
                    url,
                    authType = GetSettingValue("AuthType", "WindowsIntegrated"),
                    bodyLength = (latestHttpData?.Body ?? HttpBody ?? string.Empty).Length,
                    status = latestHttpExecution?.Status ?? "not-run",
                    durationMs = latestHttpExecution?.DurationMs,
                    threadIndex = latestHttpExecution?.ThreadIndex,
                    startTime = latestHttpExecution?.StartTime,
                    endTime = latestHttpExecution?.EndTime
                }, new JsonSerializerOptions { WriteIndented = true });

                var responseHeaders = latestHttpData?.ResponseHeaders ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                HttpResponseHeadersPreview = JsonSerializer.Serialize(responseHeaders, new JsonSerializerOptions { WriteIndented = true });
                HttpResponseCookiesPreview = JsonSerializer.Serialize(ExtractCookiesFromHeaders(responseHeaders, "Set-Cookie"), new JsonSerializerOptions { WriteIndented = true });
                HttpResponseMetadataPreview = JsonSerializer.Serialize(new
                {
                    status = latestHttpExecution?.Status ?? "not-run",
                    httpStatus = latestHttpData?.ResponseStatus,
                    contentType = TryGetHeaderValue(responseHeaders, "Content-Type"),
                    bodyLength = (latestHttpData?.ResponseBody ?? string.Empty).Length,
                    durationMs = latestHttpExecution?.DurationMs,
                    threadIndex = latestHttpExecution?.ThreadIndex,
                    error = latestHttpExecution?.Error
                }, new JsonSerializerOptions { WriteIndented = true });

                if (httpRequestRuns.Count > 0)
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        runs = httpRequestRuns
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        type = "Http",
                        method,
                        url
                    }, new JsonSerializerOptions { WriteIndented = true });
                }

                if (httpRuns.Count > 0)
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        runs = httpRuns
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
                else if (lastHttp != null)
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        status = lastHttp.ResponseStatus,
                        body = lastHttp.ResponseBody,
                        headers = lastHttp.Headers
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        message = "Response will be available after execution.",
                        component = nodeName,
                        type = "Http"
                    }, new JsonSerializerOptions { WriteIndented = true });
                }

                PreviewLogs = $"[{now}] HTTP preview refreshed\n[{now}] Target: {method} {url}";
                AppendExtractionPreview(now);
                return;
            }

            if (nodeType == "GraphQl")
            {
                var endpoint = GetSettingValue("Endpoint", "https://api.example.com/graphql");
                var query = GetSettingValue("Query", "query { health }");
                var variables = GetSettingValue("Variables", "{}");
                var latestGraphExecution = nodeExecutionResults
                    .OrderByDescending(result => result.EndTime ?? result.StartTime)
                    .FirstOrDefault();
                var lastGraphQl = GetLastExecutionData<GraphQlData>(nodeId);
                var graphRequestRuns = nodeExecutionResults
                    .Where(result => result.Data is GraphQlData)
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        endpoint = (result.Data as GraphQlData)?.Endpoint,
                        query = (result.Data as GraphQlData)?.Query,
                        variables = (result.Data as GraphQlData)?.Variables,
                        headers = (result.Data as GraphQlData)?.Headers
                    })
                    .ToList();
                var graphRuns = nodeExecutionResults
                    .Where(result => result.Data is GraphQlData)
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        responseStatus = (result.Data as GraphQlData)?.ResponseStatus,
                        responseBody = (result.Data as GraphQlData)?.ResponseBody
                    })
                    .ToList();

                if (latestGraphExecution != null
                    && latestGraphExecution.Data is not GraphQlData
                    && string.Equals(latestGraphExecution.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        type = "GraphQl",
                        endpoint,
                        query,
                        variables,
                        status = latestGraphExecution.Status,
                        error = latestGraphExecution.Error,
                        threadIndex = latestGraphExecution.ThreadIndex,
                        durationMs = latestGraphExecution.DurationMs
                    }, new JsonSerializerOptions { WriteIndented = true });

                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        status = latestGraphExecution.Status,
                        error = latestGraphExecution.Error,
                        message = "Request failed before receiving a GraphQL response."
                    }, new JsonSerializerOptions { WriteIndented = true });

                    PreviewLogs = $"[{now}] GraphQL preview refreshed\n[{now}] Endpoint: {endpoint}\n[{now}] Last run failed: {latestGraphExecution.Error}";
                    AppendExtractionPreview(now);
                    return;
                }

                if (graphRequestRuns.Count > 0)
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        runs = graphRequestRuns
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        type = "GraphQl",
                        endpoint,
                        query,
                        variables
                    }, new JsonSerializerOptions { WriteIndented = true });
                }

                if (graphRuns.Count > 0)
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        runs = graphRuns
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
                else if (lastGraphQl != null)
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        status = lastGraphQl.ResponseStatus,
                        body = lastGraphQl.ResponseBody
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        message = "Response will be available after execution.",
                        component = nodeName,
                        type = "GraphQl"
                    }, new JsonSerializerOptions { WriteIndented = true });
                }

                PreviewLogs = $"[{now}] GraphQL preview refreshed\n[{now}] Endpoint: {endpoint}";
                AppendExtractionPreview(now);
                return;
            }

            if (nodeType == "Sql")
            {
                var provider = NormalizeSqlProvider(GetSettingValue("Provider", "SqlServer"));
                var connection = GetSettingValue("Connection", "Server=.;Database=master;Trusted_Connection=True;");
                var query = GetSettingValue("Query", "SELECT 1");
                var latestSqlExecution = nodeExecutionResults
                    .OrderByDescending(result => result.EndTime ?? result.StartTime)
                    .FirstOrDefault();
                var lastSql = GetLastExecutionData<SqlData>(nodeId);
                var sqlRequestRuns = nodeExecutionResults
                    .Where(result => result.Data is SqlData)
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        provider = (result.Data as SqlData)?.Provider,
                        connection = (result.Data as SqlData)?.ConnectionString,
                        query = (result.Data as SqlData)?.Query
                    })
                    .ToList();
                var sqlRuns = nodeExecutionResults
                    .Where(result => result.Data is SqlData)
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        rows = (result.Data as SqlData)?.QueryResult
                    })
                    .ToList();

                if (latestSqlExecution != null
                    && latestSqlExecution.Data is not SqlData
                    && string.Equals(latestSqlExecution.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        type = "Sql",
                        provider,
                        connection,
                        query,
                        status = latestSqlExecution.Status,
                        error = latestSqlExecution.Error,
                        threadIndex = latestSqlExecution.ThreadIndex,
                        durationMs = latestSqlExecution.DurationMs
                    }, new JsonSerializerOptions { WriteIndented = true });

                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        status = latestSqlExecution.Status,
                        error = latestSqlExecution.Error,
                        message = "Execution failed before returning SQL data."
                    }, new JsonSerializerOptions { WriteIndented = true });

                    PreviewLogs = $"[{now}] SQL preview refreshed\n[{now}] Executing: {query}\n[{now}] Last run failed: {latestSqlExecution.Error}";
                    AppendExtractionPreview(now);
                    return;
                }

                if (sqlRequestRuns.Count > 0)
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        runs = sqlRequestRuns
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        type = "Sql",
                        provider,
                        connection,
                        query
                    }, new JsonSerializerOptions { WriteIndented = true });
                }

                if (sqlRuns.Count > 0)
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        runs = sqlRuns
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
                else if (lastSql != null)
                {
                    lastSql.Properties.TryGetValue("rowsAffected", out var rowsAffected);
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        rows = lastSql.QueryResult,
                        affectedRows = rowsAffected
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        message = "Response will be available after execution.",
                        component = nodeName,
                        type = "Sql"
                    }, new JsonSerializerOptions { WriteIndented = true });
                }

                PreviewLogs = $"[{now}] SQL preview refreshed\n[{now}] Executing: {query}";
                AppendExtractionPreview(now);
                return;
            }

            if (nodeType == "Threads")
            {
                var threadCount = GetSettingValue("ThreadCount", "1");
                var rampUp = GetSettingValue("RampUpSeconds", "1");
                var childIds = GetDescendantIds(SelectedNode).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var lastResults = FilterPreviewResults((_lastExecutionContext?.Results ?? new List<ExecutionResult>())
                    .Where(result => childIds.Contains(result.ComponentId))
                    , lastPerComponent: true)
                    .Select(result => (object)new
                    {
                        name = result.ComponentName,
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        passed = result.Passed,
                        error = result.Error,
                        data = result.Data
                    })
                    .ToList();

                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = "Threads",
                    threadCount,
                    rampUpSeconds = rampUp
                }, new JsonSerializerOptions { WriteIndented = true });

                PreviewResponse = JsonSerializer.Serialize(new
                {
                    childResults = lastResults,
                    message = lastResults.Count == 0
                        ? "Run the TestPlan to see thread results."
                        : "Last thread results"
                }, new JsonSerializerOptions { WriteIndented = true });

                PreviewLogs = $"[{now}] Threads preview refreshed\n[{now}] ThreadCount: {threadCount}, RampUpSeconds: {rampUp}";
                AppendExtractionPreview(now);
                return;
            }

            if (nodeType == "Script")
            {
                var language = GetSettingValue("Language", "CSharp");
                var code = GetSettingValue("Code", string.Empty);
                var latestScriptExecution = nodeExecutionResults
                    .OrderByDescending(result => result.EndTime ?? result.StartTime)
                    .FirstOrDefault();
                var scriptRequestRuns = nodeExecutionResults
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        language = (result.Data as ScriptData)?.ScriptLanguage,
                        code = (result.Data as ScriptData)?.ScriptCode
                    })
                    .ToList();
                var scriptRuns = nodeExecutionResults
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        durationMs = result.DurationMs,
                        status = result.Status,
                        output = (result.Data as ScriptData)?.ExecutionResult,
                        error = result.Error
                    })
                    .ToList();

                if (latestScriptExecution != null
                    && latestScriptExecution.Data is not ScriptData
                    && string.Equals(latestScriptExecution.Status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        type = "Script",
                        language,
                        code,
                        status = latestScriptExecution.Status,
                        error = latestScriptExecution.Error,
                        threadIndex = latestScriptExecution.ThreadIndex,
                        durationMs = latestScriptExecution.DurationMs
                    }, new JsonSerializerOptions { WriteIndented = true });

                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        status = latestScriptExecution.Status,
                        error = latestScriptExecution.Error,
                        message = "Script failed before returning execution data."
                    }, new JsonSerializerOptions { WriteIndented = true });

                    PreviewLogs = $"[{now}] Script preview refreshed\n[{now}] Last run failed: {latestScriptExecution.Error}";
                    AppendExtractionPreview(now);
                    return;
                }

                if (scriptRequestRuns.Count > 0)
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        runs = scriptRequestRuns
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    PreviewRequest = JsonSerializer.Serialize(new
                    {
                        component = nodeName,
                        type = "Script",
                        language,
                        code
                    }, new JsonSerializerOptions { WriteIndented = true });
                }

                if (scriptRuns.Count > 0)
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        runs = scriptRuns
                    }, new JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    PreviewResponse = JsonSerializer.Serialize(new
                    {
                        message = "Response will be available after execution.",
                        component = nodeName,
                        type = "Script"
                    }, new JsonSerializerOptions { WriteIndented = true });
                }

                PreviewLogs = $"[{now}] Script preview refreshed";
                AppendExtractionPreview(now);
                return;
            }

            var settings = SelectedNode.Settings
                .Where(setting => !string.IsNullOrWhiteSpace(setting.Key))
                .ToDictionary(setting => setting.Key, setting => setting.Value);

            var genericRuns = nodeExecutionResults
                .Select(result => new
                {
                    threadIndex = result.ThreadIndex,
                    startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    durationMs = result.DurationMs,
                    status = result.Status,
                    error = result.Error,
                    data = result.Data
                })
                .ToList();
            var latestGenericExecution = nodeExecutionResults
                .OrderByDescending(result => result.EndTime ?? result.StartTime)
                .FirstOrDefault();

            if (latestGenericExecution != null
                && latestGenericExecution.Data == null
                && string.Equals(latestGenericExecution.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = nodeType,
                    settings,
                    status = latestGenericExecution.Status,
                    error = latestGenericExecution.Error,
                    threadIndex = latestGenericExecution.ThreadIndex,
                    durationMs = latestGenericExecution.DurationMs
                }, new JsonSerializerOptions { WriteIndented = true });

                PreviewResponse = JsonSerializer.Serialize(new
                {
                    status = latestGenericExecution.Status,
                    error = latestGenericExecution.Error,
                    message = "Execution failed before returning component data."
                }, new JsonSerializerOptions { WriteIndented = true });

                PreviewLogs = $"[{now}] {nodeType} preview refreshed.\n[{now}] Last run failed: {latestGenericExecution.Error}";
                AppendExtractionPreview(now);
                return;
            }

            if (genericRuns.Count > 0)
            {
                PreviewRequest = JsonSerializer.Serialize(new
                {
                    runs = genericRuns
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = nodeType,
                    settings
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            if (genericRuns.Count > 0)
            {
                PreviewResponse = JsonSerializer.Serialize(new
                {
                    runs = genericRuns
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                PreviewResponse = JsonSerializer.Serialize(new
                {
                    message = "Preview available when this component is executed.",
                    component = nodeName,
                    type = nodeType
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            PreviewLogs = $"[{now}] {nodeType} preview refreshed.";
            AppendExtractionPreview(now);
        }

        private void PopulateTestPlanPreview(string timestamp, string nodeName)
        {
            if (SelectedNode == null)
            {
                return;
            }

            var testPlanNode = SelectedNode;
            var componentIds = GetDescendantIds(testPlanNode)
                .Append(testPlanNode.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var allPlanResults = (_lastExecutionContext?.Results ?? new List<ExecutionResult>())
                .Where(result => componentIds.Contains(result.ComponentId))
                .OrderBy(result => result.StartTime)
                .ToList();

            var planResults = FilterPreviewResults(allPlanResults, lastPerComponent: true);

            SetAssertionPreview("TestPlan", nodeName, planResults);

            var assertionSummary = BuildAssertionSummary(planResults);
            var componentBreakdown = planResults
                .GroupBy(result => new { result.ComponentId, result.ComponentName })
                .Select(group =>
                {
                    var latest = group
                        .OrderByDescending(result => result.EndTime ?? result.StartTime)
                        .First();
                    var componentAssertionSummary = BuildAssertionSummary(group);

                    return new
                    {
                        componentId = group.Key.ComponentId,
                        componentName = group.Key.ComponentName,
                        componentType = FindNodeById(group.Key.ComponentId)?.Type ?? "Unknown",
                        runs = group.Count(),
                        latestStatus = latest.Status,
                        latestDurationMs = latest.DurationMs,
                        latestThreadIndex = latest.ThreadIndex,
                        latestStartTime = latest.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        latestEndTime = latest.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        latestError = string.IsNullOrWhiteSpace(latest.Error) ? null : latest.Error,
                        assertionSummary = new
                        {
                            assertFailed = componentAssertionSummary.AssertFailed,
                            expectFailed = componentAssertionSummary.ExpectFailed,
                            passed = componentAssertionSummary.Passed
                        },
                        assertionDetails = BuildAssertionDetails(group),
                        latestData = latest.Data
                    };
                })
                .OrderBy(component => component.componentName)
                .ToList();

            var requestRuns = planResults
                .Select(result => new
                {
                    componentId = result.ComponentId,
                    componentName = result.ComponentName,
                    componentType = FindNodeById(result.ComponentId)?.Type ?? "Unknown",
                    threadIndex = result.ThreadIndex,
                    startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    status = result.Status,
                    durationMs = result.DurationMs,
                    request = BuildComponentRequestSnapshot(result.Data)
                })
                .ToList();

            var responseRuns = planResults
                .Select(result => new
                {
                    componentId = result.ComponentId,
                    componentName = result.ComponentName,
                    componentType = FindNodeById(result.ComponentId)?.Type ?? "Unknown",
                    threadIndex = result.ThreadIndex,
                    startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    status = result.Status,
                    passed = result.Passed,
                    durationMs = result.DurationMs,
                    error = result.Error,
                    response = BuildComponentResponseSnapshot(result.Data)
                })
                .ToList();

            var planStatus = planResults.Count == 0
                ? "not-run"
                : (planResults.Any(result => string.Equals(result.Status, "failed", StringComparison.OrdinalIgnoreCase))
                    ? "failed"
                    : "passed");

            var runtimeVariables = _lastExecutionContext?.Variables
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            VariablesPreview = JsonSerializer.Serialize(new
            {
                scope = "TestPlan",
                id = testPlanNode.Id,
                name = testPlanNode.Name,
                runtime = runtimeVariables
            }, new JsonSerializerOptions { WriteIndented = true });

            PreviewRequest = JsonSerializer.Serialize(new
            {
                component = nodeName,
                type = "TestPlan",
                id = testPlanNode.Id,
                enabled = testPlanNode.IsEnabled,
                plannedComponents = GetDescendantIds(testPlanNode).Count(),
                executedComponents = componentBreakdown.Count,
                runs = requestRuns,
                structure = BuildPreviewNodeStructure(testPlanNode)
            }, new JsonSerializerOptions { WriteIndented = true });

            PreviewResponse = JsonSerializer.Serialize(new
            {
                status = planStatus,
                summary = new
                {
                    executedComponents = componentBreakdown.Count,
                    totalRuns = planResults.Count,
                    passedRuns = planResults.Count(result => result.Passed),
                    failedRuns = planResults.Count(result => !result.Passed),
                    assertionSummary = new
                    {
                        assertFailed = assertionSummary.AssertFailed,
                        expectFailed = assertionSummary.ExpectFailed,
                        passed = assertionSummary.Passed
                    },
                    assertionDetails = BuildAssertionDetails(planResults)
                },
                runs = responseRuns,
                components = componentBreakdown
            }, new JsonSerializerOptions { WriteIndented = true });

            if (planResults.Count == 0)
            {
                PreviewLogs = string.Join("\n", new[]
                {
                    $"[{timestamp}] TestPlan preview refreshed.",
                    $"[{timestamp}] No component executions found for this TestPlan.",
                    $"[{timestamp}] Run this TestPlan or use Project run to populate assertions/results."
                });
                return;
            }

            var logLines = new List<string>
            {
                $"[{timestamp}] TestPlan preview refreshed.",
                $"[{timestamp}] Runs: {planResults.Count}, Components: {componentBreakdown.Count}, Passed: {planResults.Count(result => result.Passed)}, Failed: {planResults.Count(result => !result.Passed)}",
                $"[{timestamp}] Assertions: passed={assertionSummary.Passed}, assertFailed={assertionSummary.AssertFailed}, expectFailed={assertionSummary.ExpectFailed}"
            };

            foreach (var component in componentBreakdown)
            {
                logLines.Add($"[{timestamp}] - {component.componentName} ({component.componentType}): status={component.latestStatus}, runs={component.runs}, assertFailed={component.assertionSummary.assertFailed}, expectFailed={component.assertionSummary.expectFailed}");
            }

            PreviewLogs = string.Join("\n", logLines);
            AppendExtractionPreview(timestamp);
        }

        private void PopulateProjectPreview(string timestamp, string nodeName)
        {
            if (SelectedNode == null)
            {
                return;
            }

            var projectNode = SelectedNode;
            var testPlanNodes = projectNode.Children
                .Where(node => string.Equals(node.Type, "TestPlan", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var allResults = _lastExecutionContext?.Results ?? new List<ExecutionResult>();
            var previewResults = FilterPreviewResults(allResults, lastPerComponent: true);
            var projectAssertionSummary = BuildAssertionSummary(previewResults);
            SetAssertionPreview("Project", nodeName, previewResults);

            var requestRuns = previewResults
                .Select(result => new
                {
                    componentId = result.ComponentId,
                    componentName = result.ComponentName,
                    componentType = FindNodeById(result.ComponentId)?.Type ?? "Unknown",
                    threadIndex = result.ThreadIndex,
                    startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    status = result.Status,
                    durationMs = result.DurationMs,
                    request = BuildComponentRequestSnapshot(result.Data)
                })
                .ToList();

            var testPlanRequests = testPlanNodes
                .Select(testPlan =>
                {
                    var componentIds = GetDescendantIds(testPlan)
                        .Append(testPlan.Id)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var planResults = allResults
                        .Where(result => componentIds.Contains(result.ComponentId))
                        .OrderBy(result => result.StartTime)
                        .ToList();
                    planResults = FilterPreviewResults(planResults, lastPerComponent: true);

                    var componentRequests = planResults
                        .GroupBy(result => new { result.ComponentId, result.ComponentName })
                        .Select(group =>
                        {
                            var latest = group
                                .OrderByDescending(result => result.EndTime ?? result.StartTime)
                                .First();

                            return new
                            {
                                componentId = group.Key.ComponentId,
                                componentName = group.Key.ComponentName,
                                componentType = FindNodeById(group.Key.ComponentId)?.Type ?? "Unknown",
                                latestStatus = latest.Status,
                                latestThreadIndex = latest.ThreadIndex,
                                latestStartTime = latest.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                latestEndTime = latest.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                latestDurationMs = latest.DurationMs,
                                latestRequest = BuildComponentRequestSnapshot(latest.Data),
                                runs = group.Select(result => new
                                {
                                    threadIndex = result.ThreadIndex,
                                    startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                    endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                    status = result.Status,
                                    durationMs = result.DurationMs,
                                    request = BuildComponentRequestSnapshot(result.Data)
                                }).ToList()
                            };
                        })
                        .OrderBy(component => component.componentName)
                        .ToList();

                    var planStatus = planResults.Count == 0
                        ? "not-run"
                        : (planResults.Any(result => string.Equals(result.Status, "failed", StringComparison.OrdinalIgnoreCase))
                            ? "failed"
                            : "passed");

                    return new
                    {
                        id = testPlan.Id,
                        name = testPlan.Name,
                        status = planStatus,
                        totalRuns = planResults.Count,
                        components = componentRequests
                    };
                })
                .ToList();

            var responseRuns = previewResults
                .Select(result => new
                {
                    componentId = result.ComponentId,
                    componentName = result.ComponentName,
                    componentType = FindNodeById(result.ComponentId)?.Type ?? "Unknown",
                    threadIndex = result.ThreadIndex,
                    startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    status = result.Status,
                    passed = result.Passed,
                    durationMs = result.DurationMs,
                    error = result.Error,
                    response = BuildComponentResponseSnapshot(result.Data)
                })
                .ToList();

            var testPlanExecutions = testPlanNodes
                .Select(testPlan =>
                {
                    var componentIds = GetDescendantIds(testPlan)
                        .Append(testPlan.Id)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var planResults = allResults
                        .Where(result => componentIds.Contains(result.ComponentId))
                        .OrderBy(result => result.StartTime)
                        .ToList();
                    planResults = FilterPreviewResults(planResults, lastPerComponent: true);

                    var componentRuns = planResults
                        .GroupBy(result => new { result.ComponentId, result.ComponentName })
                        .Select(group =>
                        {
                            var latest = group
                                .OrderByDescending(result => result.EndTime ?? result.StartTime)
                                .First();
                            var componentAssertionSummary = BuildAssertionSummary(group);

                            return new
                            {
                                componentId = group.Key.ComponentId,
                                componentName = group.Key.ComponentName,
                                componentType = FindNodeById(group.Key.ComponentId)?.Type ?? "Unknown",
                                runs = group.Count(),
                                latestStatus = latest.Status,
                                latestDurationMs = latest.DurationMs,
                                latestThreadIndex = latest.ThreadIndex,
                                latestStartTime = latest.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                latestEndTime = latest.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                latestError = string.IsNullOrWhiteSpace(latest.Error) ? null : latest.Error,
                                assertionSummary = new
                                {
                                    assertFailed = componentAssertionSummary.AssertFailed,
                                    expectFailed = componentAssertionSummary.ExpectFailed,
                                    passed = componentAssertionSummary.Passed
                                },
                                assertionDetails = BuildAssertionDetails(group),
                                latestData = latest.Data
                            };
                        })
                        .OrderBy(component => component.componentName)
                        .ToList();

                    var planAssertionSummary = BuildAssertionSummary(planResults);
                    var planStatus = planResults.Count == 0
                        ? "not-run"
                        : (planResults.Any(result => string.Equals(result.Status, "failed", StringComparison.OrdinalIgnoreCase))
                            ? "failed"
                            : "passed");

                    return new
                    {
                        id = testPlan.Id,
                        name = testPlan.Name,
                        enabled = testPlan.IsEnabled,
                        status = planStatus,
                        plannedComponents = GetDescendantIds(testPlan).Count(),
                        executedComponents = componentRuns.Count,
                        totalRuns = planResults.Count,
                        assertionSummary = new
                        {
                            assertFailed = planAssertionSummary.AssertFailed,
                            expectFailed = planAssertionSummary.ExpectFailed,
                            passed = planAssertionSummary.Passed
                        },
                        assertionDetails = BuildAssertionDetails(planResults),
                        components = componentRuns
                    };
                })
                .ToList();

            var testPlanResponses = testPlanNodes
                .Select(testPlan =>
                {
                    var componentIds = GetDescendantIds(testPlan)
                        .Append(testPlan.Id)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var planResults = allResults
                        .Where(result => componentIds.Contains(result.ComponentId))
                        .OrderBy(result => result.StartTime)
                        .ToList();
                    planResults = FilterPreviewResults(planResults, lastPerComponent: true);

                    var componentResponses = planResults
                        .GroupBy(result => new { result.ComponentId, result.ComponentName })
                        .Select(group =>
                        {
                            var latest = group
                                .OrderByDescending(result => result.EndTime ?? result.StartTime)
                                .First();

                            return new
                            {
                                componentId = group.Key.ComponentId,
                                componentName = group.Key.ComponentName,
                                componentType = FindNodeById(group.Key.ComponentId)?.Type ?? "Unknown",
                                latestStatus = latest.Status,
                                latestPassed = latest.Passed,
                                latestDurationMs = latest.DurationMs,
                                latestThreadIndex = latest.ThreadIndex,
                                latestStartTime = latest.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                latestEndTime = latest.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                latestError = string.IsNullOrWhiteSpace(latest.Error) ? null : latest.Error,
                                latestResponse = BuildComponentResponseSnapshot(latest.Data),
                                runs = group.Select(result => new
                                {
                                    threadIndex = result.ThreadIndex,
                                    startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                    endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                                    status = result.Status,
                                    passed = result.Passed,
                                    durationMs = result.DurationMs,
                                    error = result.Error,
                                    response = BuildComponentResponseSnapshot(result.Data)
                                }).ToList()
                            };
                        })
                        .OrderBy(component => component.componentName)
                        .ToList();

                    var planStatus = planResults.Count == 0
                        ? "not-run"
                        : (planResults.Any(result => string.Equals(result.Status, "failed", StringComparison.OrdinalIgnoreCase))
                            ? "failed"
                            : "passed");

                    return new
                    {
                        id = testPlan.Id,
                        name = testPlan.Name,
                        status = planStatus,
                        totalRuns = planResults.Count,
                        passedRuns = planResults.Count(result => result.Passed),
                        failedRuns = planResults.Count(result => !result.Passed),
                        components = componentResponses
                    };
                })
                .ToList();

            var runtimeVariables = _lastExecutionContext?.Variables
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            var projectVariables = BuildDictionaryWithOverwrite(projectNode.Variables)
                .ToDictionary(entry => entry.Key, entry => (object)entry.Value, StringComparer.OrdinalIgnoreCase);

            VariablesPreview = JsonSerializer.Serialize(new
            {
                project = projectVariables,
                runtime = runtimeVariables
            }, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            PreviewRequest = JsonSerializer.Serialize(new
            {
                summary = new
                {
                    environment = SelectedEnvironment,
                    runMode = SelectedProjectRunMode,
                    previewDataMode = SelectedPreviewDataMode,
                    testPlanCount = testPlanNodes.Count,
                    plannedComponents = GetDescendantIds(projectNode).Count(),
                    executedComponents = previewResults.Select(result => result.ComponentId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    totalRuns = requestRuns.Count
                },
                testPlans = testPlanRequests
            }, new JsonSerializerOptions { WriteIndented = true });

            PreviewResponse = JsonSerializer.Serialize(new
            {
                status = _lastExecutionContext?.Status ?? "not-run",
                summary = new
                {
                    executedTestPlans = testPlanExecutions.Count(plan => !string.Equals(plan.status, "not-run", StringComparison.OrdinalIgnoreCase)),
                    executedComponents = previewResults.Select(result => result.ComponentId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    totalRuns = previewResults.Count,
                    passedRuns = previewResults.Count(result => result.Passed),
                    failedRuns = previewResults.Count(result => !result.Passed)
                },
                testPlans = testPlanResponses
            }, new JsonSerializerOptions { WriteIndented = true });

            if (previewResults.Count == 0)
            {
                PreviewLogs = string.Join("\n", new[]
                {
                    $"[{timestamp}] Project preview refreshed.",
                    $"[{timestamp}] No component executions found yet.",
                    $"[{timestamp}] Run using 'Run TestPlans' to see combined trace across all TestPlans."
                });
                return;
            }

            var logLines = new List<string>
            {
                $"[{timestamp}] Project preview refreshed.",
                $"[{timestamp}] TestPlans: {testPlanNodes.Count}, Runs: {previewResults.Count}, Passed: {previewResults.Count(result => result.Passed)}, Failed: {previewResults.Count(result => !result.Passed)}"
            };

            foreach (var plan in testPlanExecutions)
            {
                logLines.Add($"[{timestamp}] - {plan.name}: {plan.status}, runs={plan.totalRuns}, components={plan.executedComponents}, assertFailed={plan.assertionSummary.assertFailed}, expectFailed={plan.assertionSummary.expectFailed}");
            }

            PreviewLogs = string.Join("\n", logLines);
            AppendExtractionPreview(timestamp);
        }

        private static (int AssertFailed, int ExpectFailed, int Passed) BuildAssertionSummary(IEnumerable<ExecutionResult> results)
        {
            var assertFailed = 0;
            var expectFailed = 0;
            var passed = 0;

            foreach (var result in results)
            {
                if (result.AssertionResults == null)
                {
                    continue;
                }

                foreach (var assertion in result.AssertionResults)
                {
                    if (assertion.Passed)
                    {
                        passed++;
                        continue;
                    }

                    if (string.Equals(assertion.Mode, "Expect", StringComparison.OrdinalIgnoreCase))
                    {
                        expectFailed++;
                    }
                    else
                    {
                        assertFailed++;
                    }
                }
            }

            return (assertFailed, expectFailed, passed);
        }

        private static List<object> BuildAssertionDetails(IEnumerable<ExecutionResult> results)
        {
            return results
                .Where(result => result.AssertionResults != null && result.AssertionResults.Count > 0)
                .SelectMany(result => result.AssertionResults.Select(assertion => (object)new
                {
                    componentId = result.ComponentId,
                    componentName = result.ComponentName,
                        threadIndex = result.ThreadIndex,
                        startTime = result.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        endTime = result.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    componentStatus = result.Status,
                    passed = assertion.Passed,
                    mode = assertion.Mode,
                    source = assertion.Source,
                    jsonPath = assertion.JsonPath,
                    condition = assertion.Condition,
                    expected = assertion.Expected,
                    actual = assertion.Actual,
                    message = assertion.Message
                }))
                .ToList();
        }

        private void SetAssertionPreview(string scopeType, string scopeName, IEnumerable<ExecutionResult> results)
        {
            var materialized = results.ToList();
            var summary = BuildAssertionSummary(materialized);
            var details = BuildAssertionDetails(materialized);

            AssertionPreview = JsonSerializer.Serialize(new
            {
                scope = new
                {
                    type = scopeType,
                    name = scopeName
                },
                previewDataMode = SelectedPreviewDataMode,
                summary = new
                {
                    total = details.Count,
                    passed = summary.Passed,
                    assertFailed = summary.AssertFailed,
                    expectFailed = summary.ExpectFailed
                },
                details
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        private static object BuildComponentRequestSnapshot(object? data)
        {
            return data switch
            {
                HttpData http => new
                {
                    method = http.Method,
                    url = http.Url,
                    headers = http.Headers,
                    body = http.Body
                },
                GraphQlData graphQl => new
                {
                    endpoint = graphQl.Endpoint,
                    query = graphQl.Query,
                    variables = graphQl.Variables,
                    headers = graphQl.Headers
                },
                SqlData sql => new
                {
                    connectionString = sql.ConnectionString,
                    query = sql.Query
                },
                ScriptData script => new
                {
                    language = script.ScriptLanguage,
                    code = script.ScriptCode
                },
                AssertData assertion => new
                {
                    expected = assertion.ExpectedValue,
                    actual = assertion.ActualValue,
                    op = assertion.Operator
                },
                TimerData timer => new
                {
                    delayMs = timer.DelayMs
                },
                null => new
                {
                    message = "No request payload"
                },
                _ => data
            };
        }

        private static object BuildComponentResponseSnapshot(object? data)
        {
            return data switch
            {
                HttpData http => new
                {
                    responseStatus = http.ResponseStatus,
                    responseHeaders = http.ResponseHeaders,
                    responseBody = http.ResponseBody
                },
                GraphQlData graphQl => new
                {
                    responseStatus = graphQl.ResponseStatus,
                    responseBody = graphQl.ResponseBody
                },
                SqlData sql => new
                {
                    rows = sql.QueryResult,
                    rowsCount = sql.QueryResult?.Count ?? 0
                },
                ScriptData script => new
                {
                    output = script.ExecutionResult
                },
                AssertData assertion => new
                {
                    passed = assertion.Passed,
                    errorMessage = assertion.ErrorMessage,
                    expected = assertion.ExpectedValue,
                    actual = assertion.ActualValue
                },
                TimerData timer => new
                {
                    executed = timer.Executed,
                    delayMs = timer.DelayMs
                },
                null => new
                {
                    message = "No response payload"
                },
                _ => data
            };
        }

        private static object BuildPreviewNodeStructure(PlanNode node)
        {
            return new
            {
                id = node.Id,
                name = node.Name,
                type = node.Type,
                enabled = node.IsEnabled,
                children = node.Children.Select(BuildPreviewNodeStructure).ToList()
            };
        }

        private void AppendExtractionPreview(string timestamp)
        {
            if (SelectedNode == null || SelectedNode.Extractors.Count == 0)
            {
                return;
            }

            var lines = new List<string>
            {
                $"[{timestamp}] Variable extraction preview:"
            };

            foreach (var extractor in SelectedNode.Extractors)
            {
                if (string.IsNullOrWhiteSpace(extractor.VariableName) || string.IsNullOrWhiteSpace(extractor.Source))
                {
                    continue;
                }

                var sourceValue = ResolvePreviewSourceValue(extractor.Source);
                if (string.IsNullOrEmpty(sourceValue))
                {
                    lines.Add($"[{timestamp}] - {extractor.VariableName}: <missing source> ({extractor.Source})");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(extractor.JsonPath))
                {
                    lines.Add($"[{timestamp}] - {extractor.VariableName} = {sourceValue}");
                    continue;
                }

                var jsonPath = extractor.JsonPath.Trim();
                if (string.Equals(jsonPath, "$", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(jsonPath, "$.", StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add($"[{timestamp}] - {extractor.VariableName} = {sourceValue}");
                    continue;
                }

                if (TryExtractJsonPath(sourceValue, extractor.JsonPath, out var extracted))
                {
                    lines.Add($"[{timestamp}] - {extractor.VariableName} = {extracted}");
                }
                else
                {
                    lines.Add($"[{timestamp}] - {extractor.VariableName}: <path not found>");
                }
            }

            PreviewLogs = string.Join("\n", new[] { PreviewLogs }.Concat(lines));
        }

        private void ResetHttpDetailPreviews()
        {
            HttpRequestHeadersPreview = "Select an HTTP component to see request headers.";
            HttpRequestCookiesPreview = "Select an HTTP component to see request cookies.";
            HttpRequestMetadataPreview = "Select an HTTP component to see request metadata.";
            HttpResponseHeadersPreview = "Select an HTTP component to see response headers.";
            HttpResponseCookiesPreview = "Select an HTTP component to see response cookies.";
            HttpResponseMetadataPreview = "Select an HTTP component to see response metadata.";
        }

        private Dictionary<string, string> BuildRequestHeadersFromSettings()
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var rawHeaders = GetSettingValue("Headers", "{}");
            if (!string.IsNullOrWhiteSpace(rawHeaders))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(rawHeaders);
                    if (parsed != null)
                    {
                        foreach (var item in parsed)
                        {
                            headers[item.Key] = item.Value;
                        }
                    }
                }
                catch
                {
                }
            }

            return headers;
        }

        private static List<string> ExtractCookiesFromHeaders(Dictionary<string, string> headers, string cookieHeaderName)
        {
            if (!headers.TryGetValue(cookieHeaderName, out var cookieHeader) || string.IsNullOrWhiteSpace(cookieHeader))
            {
                return new List<string>();
            }

            return cookieHeader
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(cookie => cookie.Trim())
                .Where(cookie => !string.IsNullOrWhiteSpace(cookie))
                .ToList();
        }

        private static string? TryGetHeaderValue(Dictionary<string, string> headers, string headerName)
        {
            return headers.TryGetValue(headerName, out var value) ? value : null;
        }

        private void ClearRequestButton_Click(object sender, RoutedEventArgs e)
        {
            PreviewRequest = string.Empty;
        }

        private void ClearResponseButton_Click(object sender, RoutedEventArgs e)
        {
            PreviewResponse = string.Empty;
        }

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            PreviewLogs = string.Empty;
            _runtimeTraceLogBuffer = string.Empty;
        }

        private void ClearVariablesButton_Click(object sender, RoutedEventArgs e)
        {
            VariablesPreview = string.Empty;
        }

        private void ClearAssertionsButton_Click(object sender, RoutedEventArgs e)
        {
            AssertionPreview = string.Empty;
        }

        private void ClearChildrenPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || _lastExecutionContext == null)
            {
                return;
            }

            var descendantIds = GetDescendantIds(SelectedNode)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _lastExecutionContext.Results.RemoveAll(result => descendantIds.Contains(result.ComponentId));
            RefreshComponentPreview();
        }

        private void ClearSelectedNodePreviewWithChildren(PlanNode selectedNode)
        {
            if (selectedNode == null || _lastExecutionContext == null)
            {
                return;
            }

            var idsToClear = GetDescendantIds(selectedNode)
                .Concat(new[] { selectedNode.Id })
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _lastExecutionContext.Results.RemoveAll(result => idsToClear.Contains(result.ComponentId));
            RefreshComponentPreview();
        }

        private TData? GetLastExecutionData<TData>(string componentId) where TData : ComponentData
        {
            if (_lastExecutionContext == null)
            {
                return null;
            }

            return _lastExecutionContext.Results
                .Where(result => result.Data is TData && string.Equals(result.ComponentId, componentId, StringComparison.OrdinalIgnoreCase))
                .Select(result => result.Data)
                .OfType<TData>()
                .LastOrDefault();
        }

        private List<ExecutionResult> GetExecutionResults(string componentId)
        {
            if (_lastExecutionContext == null)
            {
                return new List<ExecutionResult>();
            }

            return _lastExecutionContext.Results
                .Where(result => string.Equals(result.ComponentId, componentId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(result => result.ThreadIndex)
                .ThenBy(result => result.StartTime)
                .ToList();
        }

        private List<ExecutionResult> GetExecutionResultsForScope(PlanNode node)
        {
            if (_lastExecutionContext == null)
            {
                return new List<ExecutionResult>();
            }

            var scopedIds = GetDescendantIds(node)
                .Append(node.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return _lastExecutionContext.Results
                .Where(result => scopedIds.Contains(result.ComponentId))
                .OrderBy(result => result.ThreadIndex)
                .ThenBy(result => result.StartTime)
                .ToList();
        }

        private static IEnumerable<string> GetDescendantNames(PlanNode node)
        {
            foreach (var child in node.Children)
            {
                yield return child.Name;
                foreach (var descendant in GetDescendantNames(child))
                {
                    yield return descendant;
                }
            }
        }

        private static IEnumerable<string> GetDescendantIds(PlanNode node)
        {
            foreach (var child in node.Children)
            {
                yield return child.Id;
                foreach (var descendant in GetDescendantIds(child))
                {
                    yield return descendant;
                }
            }
        }

        private string? ResolvePreviewSourceValue(string source)
        {
            if (string.IsNullOrWhiteSpace(source) || SelectedNode == null)
            {
                return null;
            }

            if (string.Equals(source, "PreviewRequest", StringComparison.OrdinalIgnoreCase))
            {
                return PreviewRequest;
            }

            if (string.Equals(source, "PreviewResponse", StringComparison.OrdinalIgnoreCase))
            {
                return ExpandEmbeddedJsonStringsInJsonText(PreviewResponse);
            }

            if (string.Equals(source, "PreviewLogs", StringComparison.OrdinalIgnoreCase))
            {
                return PreviewLogs;
            }

            var settings = SelectedNode.Settings
                .Where(setting => !string.IsNullOrWhiteSpace(setting.Key))
                .ToDictionary(setting => setting.Key, setting => setting.Value);

            if (settings.TryGetValue(source, out var value))
            {
                return value;
            }

            return null;
        }

        private static string ExpandEmbeddedJsonStringsInJsonText(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                return jsonText;
            }

            try
            {
                using var doc = JsonDocument.Parse(jsonText);
                var normalized = ConvertJsonElementForExtraction(doc.RootElement);
                return JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return jsonText;
            }
        }

        private static object? ConvertJsonElementForExtraction(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    var obj = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var property in element.EnumerateObject())
                    {
                        obj[property.Name] = ConvertJsonElementForExtraction(property.Value);
                    }

                    return obj;
                }
                case JsonValueKind.Array:
                {
                    var list = new List<object?>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ConvertJsonElementForExtraction(item));
                    }

                    return list;
                }
                case JsonValueKind.String:
                {
                    var text = element.GetString() ?? string.Empty;
                    if (TryParseJsonString(text, out var parsed))
                    {
                        return ConvertJsonElementForExtraction(parsed);
                    }

                    return text;
                }
                case JsonValueKind.Number:
                    return element.GetRawText();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                default:
                    return null;
            }
        }

        private static bool TryParseJsonString(string? text, out JsonElement parsedElement)
        {
            parsedElement = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.Trim();
            if (!(trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                && !(trimmed.StartsWith("[") && trimmed.EndsWith("]")))
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                parsedElement = doc.RootElement.Clone();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryExtractJsonPath(string json, string path, out string extracted)
        {
            extracted = string.Empty;
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var element = doc.RootElement;
                var normalized = path.Trim();
                if (normalized.StartsWith("$"))
                {
                    normalized = normalized.TrimStart('$');
                    if (normalized.StartsWith("."))
                    {
                        normalized = normalized.Substring(1);
                    }
                }

                var segments = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
                foreach (var segment in segments)
                {
                    if (!TryResolveSegment(ref element, segment))
                    {
                        return false;
                    }
                }

                extracted = element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString() ?? string.Empty,
                    JsonValueKind.Number => element.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => "null",
                    _ => element.GetRawText()
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveSegment(ref JsonElement element, string segment)
        {
            var remaining = segment;
            while (remaining.Length > 0)
            {
                var bracketIndex = remaining.IndexOf('[');
                if (bracketIndex < 0)
                {
                    return TryResolvePropertyOrIndex(ref element, remaining);
                }

                var propertyName = remaining.Substring(0, bracketIndex);
                if (!string.IsNullOrEmpty(propertyName))
                {
                    if (!TryResolvePropertyOrIndex(ref element, propertyName))
                    {
                        return false;
                    }
                }

                var endBracket = remaining.IndexOf(']', bracketIndex + 1);
                if (endBracket < 0)
                {
                    return false;
                }

                var indexValue = remaining.Substring(bracketIndex + 1, endBracket - bracketIndex - 1);
                if (!int.TryParse(indexValue, out var index))
                {
                    return false;
                }

                if (element.ValueKind == JsonValueKind.String
                    && TryParseJsonString(element.GetString(), out var parsedFromString))
                {
                    element = parsedFromString;
                }

                if (element.ValueKind != JsonValueKind.Array || index < 0 || index >= element.GetArrayLength())
                {
                    return false;
                }

                element = element[index];
                remaining = remaining.Substring(endBracket + 1);
            }

            return true;
        }

        private static bool TryResolvePropertyOrIndex(ref JsonElement element, string token)
        {
            if (element.ValueKind == JsonValueKind.String
                && TryParseJsonString(element.GetString(), out var parsedFromString))
            {
                element = parsedFromString;
            }

            if (element.ValueKind == JsonValueKind.Array && int.TryParse(token, out var index))
            {
                if (index < 0 || index >= element.GetArrayLength())
                {
                    return false;
                }

                element = element[index];
                return true;
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!TryGetPropertyIgnoreCase(element, token, out var next))
            {
                return false;
            }

            element = next;
            return true;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement element, string token, out JsonElement next)
        {
            if (element.TryGetProperty(token, out next))
            {
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, token, StringComparison.OrdinalIgnoreCase))
                {
                    next = property.Value;
                    return true;
                }
            }

            next = default;
            return false;
        }

        private static object BuildNodeObject(PlanNode node)
        {
            var settings = BuildDictionaryWithOverwrite(node.Settings);

            var variables = BuildDictionaryWithOverwrite(node.Variables);

            var extractors = node.Extractors
                .Where(extractor => !string.IsNullOrWhiteSpace(extractor.Source) || !string.IsNullOrWhiteSpace(extractor.VariableName))
                .Select(extractor => new
                {
                    source = extractor.Source,
                    jsonPath = extractor.JsonPath,
                    variableName = extractor.VariableName
                })
                .ToList();

            var assertions = node.Assertions
                .Where(assertion => !string.IsNullOrWhiteSpace(assertion.Source))
                .Select(assertion => new
                {
                    mode = assertion.Mode,
                    source = assertion.Source,
                    jsonPath = assertion.JsonPath,
                    condition = assertion.Condition,
                    expected = assertion.Expected
                })
                .ToList();

            return new
            {
                type = node.Type,
                name = node.Name,
                enabled = node.IsEnabled,
                settings,
                variables,
                extractors,
                assertions,
                children = node.Children.Select(BuildNodeObject).ToList()
            };
        }

        private static NodeFileModel ToFileModel(PlanNode node)
        {
            return new NodeFileModel
            {
                Id = node.Id,
                Type = node.Type,
                Name = node.Name,
                Enabled = node.IsEnabled,
                Settings = BuildDictionaryWithOverwrite(node.Settings),
                Variables = BuildDictionaryWithOverwrite(node.Variables),
                Extractors = node.Extractors
                    .Where(extractor => !string.IsNullOrWhiteSpace(extractor.Source) || !string.IsNullOrWhiteSpace(extractor.VariableName))
                    .Select(extractor => new VariableExtractionFileModel
                    {
                        Source = extractor.Source,
                        JsonPath = extractor.JsonPath,
                        VariableName = extractor.VariableName
                    })
                    .ToList(),
                Assertions = node.Assertions
                    .Where(assertion => !string.IsNullOrWhiteSpace(assertion.Source))
                    .Select(assertion => new AssertionFileModel
                    {
                        Mode = assertion.Mode,
                        Source = assertion.Source,
                        JsonPath = assertion.JsonPath,
                        Condition = assertion.Condition,
                        Expected = assertion.Expected
                    })
                    .ToList(),
                Children = node.Children.Select(ToFileModel).ToList()
            };
        }

        private static PlanNode FromFileModel(NodeFileModel model, PlanNode? parent)
        {
            var node = new PlanNode(model.Type, model.Name, string.IsNullOrWhiteSpace(model.Id) ? null : model.Id)
            {
                Parent = parent,
                IsEnabled = model.Enabled
            };

            node.Settings.Clear();
            foreach (var setting in model.Settings)
            {
                node.Settings.Add(new NodeSetting(setting.Key, setting.Value));
            }

            node.Variables.Clear();
            if (model.Variables != null)
            {
                foreach (var variable in model.Variables)
                {
                    node.Variables.Add(new NodeSetting(variable.Key, variable.Value));
                }
            }

            node.Extractors.Clear();
            foreach (var extractor in model.Extractors)
            {
                node.Extractors.Add(new VariableExtractionRule(extractor.Source, extractor.JsonPath, extractor.VariableName));
            }

            node.Assertions.Clear();
            if (model.Assertions != null)
            {
                foreach (var assertion in model.Assertions)
                {
                    node.Assertions.Add(new AssertionRule(assertion.Source, assertion.JsonPath, assertion.Condition, assertion.Expected, assertion.Mode));
                }
            }

            foreach (var child in model.Children)
            {
                var childNode = FromFileModel(child, node);
                node.Children.Add(childNode);
            }

            return node;
        }

        private string GetSettingValue(string key, string fallback)
        {
            if (SelectedNode == null)
            {
                return fallback;
            }

            var normalizedKey = (key ?? string.Empty).Trim();
            var setting = SelectedNode.Settings.FirstOrDefault(current =>
                string.Equals(current.Key?.Trim(), normalizedKey, StringComparison.OrdinalIgnoreCase));
            return setting?.Value ?? fallback;
        }

        private void SetSettingValue(string key, string value)
        {
            if (SelectedNode == null)
            {
                return;
            }

            var normalizedKey = (key ?? string.Empty).Trim();
            var matchingSettings = SelectedNode.Settings
                .Where(current => string.Equals(current.Key?.Trim(), normalizedKey, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var setting = matchingSettings.FirstOrDefault();
            if (setting == null)
            {
                setting = new NodeSetting(normalizedKey, value);
                SelectedNode.Settings.Add(setting);
            }
            else if (setting.Value != value)
            {
                setting.Value = value;
            }

            // Keep only one setting per key so reads are deterministic.
            foreach (var duplicate in matchingSettings.Skip(1).ToList())
            {
                SelectedNode.Settings.Remove(duplicate);
            }

            RefreshJsonPreview();
        }

        public string GetVariableUniquenessLabel(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return string.Empty;
            }

            if (!_variableUsageMap.TryGetValue(variableName.Trim(), out var paths) || paths.Count == 0)
            {
                return "Unique";
            }

            return paths.Count > 1 ? $"Repeated ({paths.Count})" : "Unique";
        }

        public string GetVariableUsageTooltip(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return "Variable name is empty.";
            }

            if (!_variableUsageMap.TryGetValue(variableName.Trim(), out var paths) || paths.Count == 0)
            {
                return "No usage path found.";
            }

            return string.Join("\n", paths);
        }

        private void RebuildVariableUsageMap()
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in RootNodes)
            {
                BuildVariableUsageMap(root, map);
            }

            _variableUsageMap = map;
            VariableUsageVersion++;
        }

        private static void BuildVariableUsageMap(PlanNode node, Dictionary<string, List<string>> map)
        {
            var path = BuildNodePath(node);

            foreach (var variable in node.Variables)
            {
                AddVariableUsage(map, variable.Key, path);
            }

            foreach (var setting in node.Settings)
            {
                if (IsVariableSettingKey(setting.Key))
                {
                    AddVariableUsage(map, setting.Value, $"{path} -> {setting.Key}");
                }
            }

            foreach (var extractor in node.Extractors)
            {
                AddVariableUsage(map, extractor.VariableName, $"{path} -> Extractor");
            }

            if (string.Equals(node.Type, "VariableExtractor", StringComparison.OrdinalIgnoreCase))
            {
                var variableNameSetting = node.Settings.FirstOrDefault(setting => string.Equals(setting.Key, "VariableName", StringComparison.OrdinalIgnoreCase));
                AddVariableUsage(map, variableNameSetting?.Value, path);
            }

            foreach (var child in node.Children)
            {
                BuildVariableUsageMap(child, map);
            }
        }

        private static void AddVariableUsage(Dictionary<string, List<string>> map, string? variableName, string path)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return;
            }

            var key = variableName.Trim();
            if (!map.TryGetValue(key, out var paths))
            {
                paths = new List<string>();
                map[key] = paths;
            }

            if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                paths.Add(path);
            }
        }

        private static string BuildNodePath(PlanNode node)
        {
            var parts = new List<string>();
            var current = node;
            while (current != null)
            {
                if (!string.Equals(current.Type, "Project", StringComparison.OrdinalIgnoreCase))
                {
                    parts.Add(current.Name);
                }

                current = current.Parent;
            }

            parts.Reverse();
            return parts.Count == 0 ? "Project" : string.Join("->", parts);
        }

        private static Dictionary<string, string> BuildDictionaryWithOverwrite(IEnumerable<NodeSetting> source)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in source)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                dictionary[entry.Key.Trim()] = entry.Value;
            }

            return dictionary;
        }

        private PlanNode? GetProjectNode()
        {
            return RootNodes.FirstOrDefault(node => string.Equals(node.Type, "Project", StringComparison.OrdinalIgnoreCase));
        }

        private string GetProjectSettingValue(string key, string fallback)
        {
            var projectNode = GetProjectNode();
            if (projectNode == null)
            {
                return fallback;
            }

            var setting = projectNode.Settings.FirstOrDefault(current => string.Equals(current.Key, key, StringComparison.OrdinalIgnoreCase));
            return setting?.Value ?? fallback;
        }

        private void SetProjectSettingValue(string key, string value)
        {
            var projectNode = GetProjectNode();
            if (projectNode == null)
            {
                return;
            }

            var setting = projectNode.Settings.FirstOrDefault(current => string.Equals(current.Key, key, StringComparison.OrdinalIgnoreCase));
            if (setting == null)
            {
                setting = new NodeSetting(key, value);
                projectNode.Settings.Add(setting);
            }
            else if (!string.Equals(setting.Value, value, StringComparison.Ordinal))
            {
                setting.Value = value;
            }

            RefreshJsonPreview();
        }

        private void RefreshApiCatalogState()
        {
            if (_isSynchronizingCatalogEditor)
            {
                RebuildParsedApiCatalogFromEditor();
                SyncCatalogSelectionFromCurrentNode();
                return;
            }

            foreach (var existing in _projectUrlCatalogEntries.ToList())
            {
                UnregisterApiCatalogBaseEntry(existing);
            }

            _projectUrlCatalogEntries.Clear();

            foreach (var entry in ParseApiCatalog(ProjectUrlCatalogJson))
            {
                RegisterApiCatalogBaseEntry(entry);
                _projectUrlCatalogEntries.Add(entry);
            }

            RebuildParsedApiCatalogFromEditor();
            OnPropertyChanged(nameof(ProjectUrlCatalogEntries));
            OnPropertyChanged(nameof(ProjectUrlCatalogJson));
            SyncCatalogSelectionFromCurrentNode();
        }

        private void RegisterApiCatalogBaseEntry(ApiCatalogBaseUrlEntry entry)
        {
            entry.PropertyChanged += ApiCatalogBaseEntry_PropertyChanged;
            entry.Endpoints.CollectionChanged += ApiCatalogEndpoints_CollectionChanged;

            foreach (var endpoint in entry.Endpoints)
            {
                RegisterApiCatalogEndpointEntry(endpoint);
            }
        }

        private void UnregisterApiCatalogBaseEntry(ApiCatalogBaseUrlEntry entry)
        {
            entry.PropertyChanged -= ApiCatalogBaseEntry_PropertyChanged;
            entry.Endpoints.CollectionChanged -= ApiCatalogEndpoints_CollectionChanged;

            foreach (var endpoint in entry.Endpoints)
            {
                UnregisterApiCatalogEndpointEntry(endpoint);
            }
        }

        private void RegisterApiCatalogEndpointEntry(ApiCatalogEndpointEntry endpoint)
        {
            endpoint.PropertyChanged += ApiCatalogEndpointEntry_PropertyChanged;
            endpoint.Parameters.CollectionChanged += ApiCatalogEndpointParameters_CollectionChanged;
            foreach (var param in endpoint.Parameters)
            {
                RegisterApiCatalogParameterEntry(param);
            }
        }

        private void UnregisterApiCatalogEndpointEntry(ApiCatalogEndpointEntry endpoint)
        {
            endpoint.PropertyChanged -= ApiCatalogEndpointEntry_PropertyChanged;
            endpoint.Parameters.CollectionChanged -= ApiCatalogEndpointParameters_CollectionChanged;
            foreach (var param in endpoint.Parameters)
            {
                UnregisterApiCatalogParameterEntry(param);
            }
        }

        private void RegisterApiCatalogParameterEntry(ApiCatalogParameterEntry param)
        {
            param.PropertyChanged += ApiCatalogParameterEntry_PropertyChanged;
        }

        private void UnregisterApiCatalogParameterEntry(ApiCatalogParameterEntry param)
        {
            param.PropertyChanged -= ApiCatalogParameterEntry_PropertyChanged;
        }

        private void ApiCatalogBaseEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            SyncApiCatalogSettingFromEditor();
        }

        private void ApiCatalogEndpointEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            SyncApiCatalogSettingFromEditor();
        }

        private void ApiCatalogEndpoints_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var endpoint in e.OldItems.OfType<ApiCatalogEndpointEntry>())
                {
                    UnregisterApiCatalogEndpointEntry(endpoint);
                }
            }

            if (e.NewItems != null)
            {
                foreach (var endpoint in e.NewItems.OfType<ApiCatalogEndpointEntry>())
                {
                    RegisterApiCatalogEndpointEntry(endpoint);
                }
            }

            SyncApiCatalogSettingFromEditor();
        }

        private void ApiCatalogEndpointParameters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var param in e.OldItems.OfType<ApiCatalogParameterEntry>())
                {
                    UnregisterApiCatalogParameterEntry(param);
                }
            }

            if (e.NewItems != null)
            {
                foreach (var param in e.NewItems.OfType<ApiCatalogParameterEntry>())
                {
                    RegisterApiCatalogParameterEntry(param);
                }
            }

            SyncApiCatalogSettingFromEditor();
        }

        private void ApiCatalogParameterEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            SyncApiCatalogSettingFromEditor();
        }

        private void SyncApiCatalogSettingFromEditor()
        {
            if (_isSynchronizingCatalogEditor)
            {
                return;
            }

            _isSynchronizingCatalogEditor = true;
            try
            {
                RebuildParsedApiCatalogFromEditor();

                var serialized = JsonSerializer.Serialize(_projectUrlCatalogEntries.Select(entry => new
                {
                    name = entry.Name,
                    baseUrl = entry.BaseUrl,
                    endpoints = entry.Endpoints.Select(endpoint => new
                    {
                        name = endpoint.Name,
                        path = endpoint.Path,
                        method = endpoint.Method,
                        body = endpoint.Body,
                        parameters = endpoint.Parameters.Select(p => new { key = p.Key, value = p.Value }).ToList(),
                        headers = endpoint.Headers,
                        query = endpoint.Query,
                        variables = endpoint.Variables
                    }).ToList()
                }).ToList(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                SetProjectSettingValue("UrlCatalog", serialized);
                OnPropertyChanged(nameof(ProjectUrlCatalogJson));
                SyncCatalogSelectionFromCurrentNode();
            }
            finally
            {
                _isSynchronizingCatalogEditor = false;
            }
        }

        private void RebuildParsedApiCatalogFromEditor()
        {
            _parsedApiCatalog.Clear();
            var normalizedEntries = _projectUrlCatalogEntries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .ToList();

            foreach (var entry in normalizedEntries)
            {
                var parsedEntry = new ApiCatalogBaseUrlEntry
                {
                    Name = entry.Name.Trim(),
                    BaseUrl = entry.BaseUrl?.Trim() ?? string.Empty
                };

                foreach (var endpoint in entry.Endpoints.Where(endpoint => !string.IsNullOrWhiteSpace(endpoint.Name)))
                {
                    parsedEntry.Endpoints.Add(new ApiCatalogEndpointEntry
                    {
                        Name = endpoint.Name.Trim(),
                        Path = endpoint.Path ?? string.Empty,
                        Method = endpoint.Method ?? string.Empty,
                        Body = endpoint.Body ?? string.Empty,
                        Parameters = new ObservableCollection<ApiCatalogParameterEntry>(
                            endpoint.Parameters.Select(p => new ApiCatalogParameterEntry { Key = p.Key, Value = p.Value })),
                        Headers = endpoint.Headers ?? string.Empty,
                        Query = endpoint.Query ?? string.Empty,
                        Variables = endpoint.Variables ?? string.Empty
                    });
                }

                _parsedApiCatalog.Add(parsedEntry);
            }

            _catalogBaseUrlOptions.Clear();
            foreach (var entry in _parsedApiCatalog.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
            {
                _catalogBaseUrlOptions.Add(entry.Name);
            }

            OnPropertyChanged(nameof(CatalogBaseUrlOptions));
            OnPropertyChanged(nameof(CatalogEndpointOptions));
        }

        private void SyncCatalogSelectionFromCurrentNode()
        {
            RefreshCatalogEndpointOptions();
            OnPropertyChanged(nameof(CatalogEndpointOptions));
            OnPropertyChanged(nameof(SelectedHttpCatalogBase));
            OnPropertyChanged(nameof(SelectedHttpCatalogEndpoint));
            OnPropertyChanged(nameof(SelectedGraphQlCatalogBase));
            OnPropertyChanged(nameof(SelectedGraphQlCatalogEndpoint));
        }

        private void RefreshCatalogEndpointOptions()
        {
            _isApplyingCatalogSelection = true;
            try
            {
                _catalogEndpointOptions.Clear();

                // Read directly from settings to avoid property-setter side-effects.
                var baseName = GetSettingValue("CatalogBase", string.Empty);
                var selectedEndpoint = GetSettingValue("CatalogEndpoint", string.Empty);

                var baseEntry = _parsedApiCatalog.FirstOrDefault(entry => string.Equals(entry.Name, baseName, StringComparison.OrdinalIgnoreCase));
                if (baseEntry == null)
                {
                    // Keep currently stored selection visible even if base/options are not yet ready.
                    if (!string.IsNullOrWhiteSpace(selectedEndpoint))
                    {
                        _catalogEndpointOptions.Add(selectedEndpoint);
                    }
                    return;
                }

                foreach (var endpoint in baseEntry.Endpoints.OrderBy(endpoint => endpoint.Name, StringComparer.OrdinalIgnoreCase))
                {
                    _catalogEndpointOptions.Add(endpoint.Name);
                }

                if (_catalogEndpointOptions.Any(name => string.Equals(name, selectedEndpoint, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                // Keep persisted value visible/selected even if it no longer matches current catalog.
                if (!string.IsNullOrWhiteSpace(selectedEndpoint))
                {
                    _catalogEndpointOptions.Add(selectedEndpoint);
                }
            }
            finally
            {
                _isApplyingCatalogSelection = false;
            }
        }

        private void ApplySelectedCatalogEndpointToCurrentNode()
        {
            if (SelectedNode == null)
            {
                return;
            }

            var isHttp = string.Equals(SelectedNode.Type, "Http", StringComparison.OrdinalIgnoreCase);
            var isGraphQl = string.Equals(SelectedNode.Type, "GraphQl", StringComparison.OrdinalIgnoreCase);
            if (!isHttp && !isGraphQl)
            {
                return;
            }

            // Resolve from the latest stored project catalog to avoid stale in-memory options.
            var latestCatalog = ParseApiCatalog(ProjectUrlCatalogJson);

            var baseName = isHttp ? SelectedHttpCatalogBase : SelectedGraphQlCatalogBase;
            var endpointName = isHttp ? SelectedHttpCatalogEndpoint : SelectedGraphQlCatalogEndpoint;

            var baseEntry = latestCatalog.FirstOrDefault(entry => string.Equals(entry.Name, baseName, StringComparison.OrdinalIgnoreCase));
            if (baseEntry == null)
            {
                MessageBox.Show("Selected Base URL was not found in Project URLs.", "Apply Catalog", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var endpoint = baseEntry.Endpoints.FirstOrDefault(entry => string.Equals(entry.Name, endpointName, StringComparison.OrdinalIgnoreCase));
            if (endpoint == null)
            {
                MessageBox.Show("Selected Endpoint was not found under the chosen Base URL.", "Apply Catalog", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var resolvedUrl = CombineBaseUrlAndPath(baseEntry.BaseUrl, endpoint.Path);

            _isApplyingCatalogSelection = true;
            try
            {
                if (isHttp)
                {
                    HttpUrl = AppendHttpParameters(resolvedUrl, endpoint.Parameters);

                    if (!string.IsNullOrWhiteSpace(endpoint.Method))
                    {
                        HttpMethod = endpoint.Method;
                    }

                    if (endpoint.Body != null)
                    {
                        HttpBody = endpoint.Body;
                    }

                    if (!string.IsNullOrWhiteSpace(endpoint.Headers))
                    {
                        HttpHeaders = endpoint.Headers;
                        OnPropertyChanged(nameof(HttpHeaders));
                    }

                    OnPropertyChanged(nameof(HttpUrl));
                    OnPropertyChanged(nameof(HttpUrlResolved));
                    OnPropertyChanged(nameof(HttpMethod));
                    OnPropertyChanged(nameof(HttpBody));
                }
                else
                {
                    GraphQlEndpoint = resolvedUrl;

                    if (!string.IsNullOrWhiteSpace(endpoint.Query))
                    {
                        GraphQlQuery = endpoint.Query;
                    }
                    else if (!string.IsNullOrWhiteSpace(endpoint.Body))
                    {
                        GraphQlQuery = endpoint.Body;
                    }

                    if (!string.IsNullOrWhiteSpace(endpoint.Variables))
                    {
                        GraphQlVariables = endpoint.Variables;
                    }

                    OnPropertyChanged(nameof(GraphQlEndpoint));
                    OnPropertyChanged(nameof(GraphQlQuery));
                    OnPropertyChanged(nameof(GraphQlVariables));
                }
            }
            finally
            {
                _isApplyingCatalogSelection = false;
            }

            RefreshComponentPreview();
        }

        private static string CombineBaseUrlAndPath(string baseUrl, string path)
        {
            var normalizedBase = (baseUrl ?? string.Empty).Trim();
            var normalizedPath = (path ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return normalizedBase;
            }

            if (Uri.TryCreate(normalizedPath, UriKind.Absolute, out _))
            {
                return normalizedPath;
            }

            if (string.IsNullOrWhiteSpace(normalizedBase))
            {
                return normalizedPath;
            }

            return $"{normalizedBase.TrimEnd('/')}/{normalizedPath.TrimStart('/')}";
        }

        private static string AppendHttpParameters(string url, IEnumerable<ApiCatalogParameterEntry> parameters)
        {
            var pairs = parameters
                .Where(p => !string.IsNullOrWhiteSpace(p.Key))
                .Select(p => Uri.EscapeDataString(p.Key.Trim()) + "=" + Uri.EscapeDataString(p.Value?.Trim() ?? string.Empty))
                .ToList();

            if (pairs.Count == 0)
            {
                return url;
            }

            var separator = url.Contains("?", StringComparison.Ordinal) ? "&" : "?";
            return url + separator + string.Join("&", pairs);
        }

        private static List<ApiCatalogBaseUrlEntry> ParseApiCatalog(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<ApiCatalogBaseUrlEntry>();
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new CatalogParameterCollectionConverter() }
                };

                var array = JsonSerializer.Deserialize<List<ApiCatalogBaseUrlEntry>>(raw, options);
                if (array != null)
                {
                    return NormalizeCatalog(array);
                }

                var wrapped = JsonSerializer.Deserialize<ApiCatalogWrapper>(raw, options);
                if (wrapped?.BaseUrls != null)
                {
                    return NormalizeCatalog(wrapped.BaseUrls);
                }
            }
            catch
            {
                // Invalid JSON should not block editing; options will simply be empty.
            }

            return new List<ApiCatalogBaseUrlEntry>();
        }

        private static List<ApiCatalogBaseUrlEntry> NormalizeCatalog(IEnumerable<ApiCatalogBaseUrlEntry> source)
        {
            return source
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .Select(entry =>
                {
                    var normalizedEntry = new ApiCatalogBaseUrlEntry
                    {
                        Name = entry.Name.Trim(),
                        BaseUrl = entry.BaseUrl?.Trim() ?? string.Empty
                    };

                    foreach (var endpoint in (entry.Endpoints ?? new ObservableCollection<ApiCatalogEndpointEntry>())
                        .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint.Name)))
                    {
                        normalizedEntry.Endpoints.Add(new ApiCatalogEndpointEntry
                        {
                            Name = endpoint.Name.Trim(),
                            Path = endpoint.Path ?? string.Empty,
                            Method = endpoint.Method ?? string.Empty,
                            Body = endpoint.Body ?? string.Empty,
                            Parameters = new ObservableCollection<ApiCatalogParameterEntry>(
                                (endpoint.Parameters ?? new ObservableCollection<ApiCatalogParameterEntry>())
                                .Select(p => new ApiCatalogParameterEntry { Key = p.Key, Value = p.Value })),
                            Headers = endpoint.Headers ?? string.Empty,
                            Query = endpoint.Query ?? string.Empty,
                            Variables = endpoint.Variables ?? string.Empty
                        });
                    }

                    return normalizedEntry;
                })
                .ToList();
        }

        private sealed class ApiCatalogWrapper
        {
            public List<ApiCatalogBaseUrlEntry> BaseUrls { get; set; } = new();
        }

        private string ResolveWithProjectVariables(string template)
        {
            if (string.IsNullOrEmpty(template))
            {
                return template;
            }

            var projectNode = RootNodes.FirstOrDefault(node => node.Type == "Project");
            var variables = projectNode != null
                ? BuildDictionaryWithOverwrite(projectNode.Variables)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(SelectedEnvironment))
            {
                variables["env"] = SelectedEnvironment;
            }

            return System.Text.RegularExpressions.Regex.Replace(template, "\\$\\{([^}]+)\\}", match =>
            {
                var key = match.Groups[1].Value;
                return variables.TryGetValue(key, out var value) ? value : match.Value;
            });
        }

        private void NormalizeDuplicateVariables(PlanNode node, NodeSetting? preferred = null)
        {
            if (_isNormalizingVariables)
            {
                return;
            }

            _isNormalizingVariables = true;
            try
            {
                if (preferred != null && !string.IsNullOrWhiteSpace(preferred.Key))
                {
                    var duplicates = node.Variables
                        .Where(variable => !ReferenceEquals(variable, preferred)
                            && string.Equals(variable.Key, preferred.Key, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var duplicate in duplicates)
                    {
                        node.Variables.Remove(duplicate);
                    }
                }

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var index = node.Variables.Count - 1; index >= 0; index--)
                {
                    var variable = node.Variables[index];
                    if (string.IsNullOrWhiteSpace(variable.Key))
                    {
                        continue;
                    }

                    var key = variable.Key.Trim();
                    if (!seen.Add(key))
                    {
                        node.Variables.RemoveAt(index);
                    }
                    else if (!string.Equals(variable.Key, key, StringComparison.Ordinal))
                    {
                        variable.Key = key;
                    }
                }
            }
            finally
            {
                _isNormalizingVariables = false;
            }
        }

        private PlanNode? FindNodeByVariablesCollection(object? sender)
        {
            if (sender is not ObservableCollection<NodeSetting> variables)
            {
                return null;
            }

            foreach (var root in RootNodes)
            {
                var found = FindNodeByVariablesCollection(root, variables);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static PlanNode? FindNodeByVariablesCollection(PlanNode node, ObservableCollection<NodeSetting> variables)
        {
            if (ReferenceEquals(node.Variables, variables))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                var found = FindNodeByVariablesCollection(child, variables);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static PlanNode? FindNodeContainingVariable(PlanNode node, NodeSetting variable)
        {
            if (node.Variables.Contains(variable))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                var found = FindNodeContainingVariable(child, variable);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static PlanNode? FindNodeContainingSetting(PlanNode node, NodeSetting setting)
        {
            if (node.Settings.Contains(setting))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                var found = FindNodeContainingSetting(child, setting);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public static bool IsVariableSettingKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var normalized = key.Trim();
            if (string.Equals(normalized, "Variables", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return normalized.EndsWith("Variable", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "VariableName", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith("VariableName", StringComparison.OrdinalIgnoreCase);
        }

        private void NotifySelectedNodeEditorProperties()
        {
            OnPropertyChanged(nameof(IsProjectSelected));
            OnPropertyChanged(nameof(IsComponentSelected));
            OnPropertyChanged(nameof(HasSelectedNodeChildren));
            OnPropertyChanged(nameof(IsHttpSelected));
            OnPropertyChanged(nameof(IsGraphQlSelected));
            OnPropertyChanged(nameof(IsSqlSelected));
            OnPropertyChanged(nameof(IsDatasetSelected));
            OnPropertyChanged(nameof(IsTimerSelected));
            OnPropertyChanged(nameof(IsLoopSelected));
            OnPropertyChanged(nameof(IsIfSelected));
            OnPropertyChanged(nameof(IsThreadsSelected));
            OnPropertyChanged(nameof(IsAssertSelected));
            OnPropertyChanged(nameof(IsVariableExtractorSelected));
            OnPropertyChanged(nameof(IsScriptSelected));
            OnPropertyChanged(nameof(ProjectVariablesForEditor));

            OnPropertyChanged(nameof(ProjectDescription));
            OnPropertyChanged(nameof(ProjectEnvironment));
            OnPropertyChanged(nameof(ProjectUrlCatalogJson));
            // SelectedHttpCatalogBase/Endpoint and SelectedGraphQl* are handled by
            // SyncCatalogSelectionFromCurrentNode(), which runs before this method.
            OnPropertyChanged(nameof(SelectedEnvironment));
            OnPropertyChanged(nameof(HttpMethod));
            OnPropertyChanged(nameof(HttpUrl));
            OnPropertyChanged(nameof(HttpUrlResolved));
            OnPropertyChanged(nameof(HttpRequestHeadersPreview));
            OnPropertyChanged(nameof(HttpRequestCookiesPreview));
            OnPropertyChanged(nameof(HttpRequestMetadataPreview));
            OnPropertyChanged(nameof(HttpResponseHeadersPreview));
            OnPropertyChanged(nameof(HttpResponseCookiesPreview));
            OnPropertyChanged(nameof(HttpResponseMetadataPreview));
            OnPropertyChanged(nameof(HttpBody));
            OnPropertyChanged(nameof(HttpHeaders));
            OnPropertyChanged(nameof(HttpAuthType));
            RaiseHttpAuthVisibilityChanged();
            OnPropertyChanged(nameof(HttpAuthUsername));
            OnPropertyChanged(nameof(HttpAuthPassword));
            OnPropertyChanged(nameof(HttpAuthToken));
            OnPropertyChanged(nameof(HttpApiKeyName));
            OnPropertyChanged(nameof(HttpApiKeyValue));
            OnPropertyChanged(nameof(HttpApiKeyLocation));
            OnPropertyChanged(nameof(HttpOAuthTokenUrl));
            OnPropertyChanged(nameof(HttpOAuthClientId));
            OnPropertyChanged(nameof(HttpOAuthClientSecret));
            OnPropertyChanged(nameof(HttpOAuthScope));
            OnPropertyChanged(nameof(GraphQlEndpoint));
            OnPropertyChanged(nameof(GraphQlQuery));
            OnPropertyChanged(nameof(GraphQlVariables));
            OnPropertyChanged(nameof(GraphQlHeaders));
            OnPropertyChanged(nameof(GraphQlAuthType));
            RaiseGraphQlAuthVisibilityChanged();
            OnPropertyChanged(nameof(GraphQlAuthUsername));
            OnPropertyChanged(nameof(GraphQlAuthPassword));
            OnPropertyChanged(nameof(GraphQlAuthToken));
            OnPropertyChanged(nameof(GraphQlApiKeyName));
            OnPropertyChanged(nameof(GraphQlApiKeyValue));
            OnPropertyChanged(nameof(GraphQlApiKeyLocation));
            OnPropertyChanged(nameof(GraphQlOAuthTokenUrl));
            OnPropertyChanged(nameof(GraphQlOAuthClientId));
            OnPropertyChanged(nameof(GraphQlOAuthClientSecret));
            OnPropertyChanged(nameof(GraphQlOAuthScope));
            RefreshSqlAuthTypeOptions();
            OnPropertyChanged(nameof(SqlProvider));
            OnPropertyChanged(nameof(SqlConnection));
            OnPropertyChanged(nameof(SqlConnectionResolved));
            OnPropertyChanged(nameof(SqlQuery));
            OnPropertyChanged(nameof(SqlQueryResolved));
            OnPropertyChanged(nameof(SqlAuthType));
            OnPropertyChanged(nameof(SqlAuthTypeOptions));
            RaiseSqlAuthVisibilityChanged();
            OnPropertyChanged(nameof(SqlAuthUsername));
            OnPropertyChanged(nameof(SqlAuthPassword));
            OnPropertyChanged(nameof(DatasetFormat));
            OnPropertyChanged(nameof(DatasetSourcePath));
            OnPropertyChanged(nameof(DatasetResolvedSourcePath));
            OnPropertyChanged(nameof(DatasetSheetName));
            OnPropertyChanged(nameof(DatasetCsvDelimiter));
            OnPropertyChanged(nameof(DatasetCsvHasHeader));
            OnPropertyChanged(nameof(DatasetJsonArrayPath));
            OnPropertyChanged(nameof(DatasetXmlRowPath));
            OnPropertyChanged(nameof(DatasetMaxRows));
            OnPropertyChanged(nameof(DatasetPreviewRows));
            OnPropertyChanged(nameof(DatasetPreviewStatus));
            OnPropertyChanged(nameof(TimerDelayMs));
            OnPropertyChanged(nameof(LoopIterations));
            OnPropertyChanged(nameof(IfCondition));
            OnPropertyChanged(nameof(ThreadCount));
            OnPropertyChanged(nameof(RampUpSeconds));
            OnPropertyChanged(nameof(AssertExpected));
            OnPropertyChanged(nameof(AssertActual));
            OnPropertyChanged(nameof(ExtractorPattern));
            OnPropertyChanged(nameof(ExtractorVariableName));
            OnPropertyChanged(nameof(ScriptLanguage));
            OnPropertyChanged(nameof(ScriptCode));
        }

        private void RaiseHttpAuthVisibilityChanged()
        {
            OnPropertyChanged(nameof(HttpShowBasicFields));
            OnPropertyChanged(nameof(HttpShowBearerFields));
            OnPropertyChanged(nameof(HttpShowApiKeyFields));
            OnPropertyChanged(nameof(HttpShowOAuthFields));
        }

        private void RaiseGraphQlAuthVisibilityChanged()
        {
            OnPropertyChanged(nameof(GraphQlShowBasicFields));
            OnPropertyChanged(nameof(GraphQlShowBearerFields));
            OnPropertyChanged(nameof(GraphQlShowApiKeyFields));
            OnPropertyChanged(nameof(GraphQlShowOAuthFields));
        }

        private void RaiseSqlAuthVisibilityChanged()
        {
            OnPropertyChanged(nameof(SqlShowBasicFields));
        }

        private void RebuildExtractorSourceOptions()
        {
            ExtractorSourceOptions.Clear();

            foreach (var source in BaseExtractorSources)
            {
                ExtractorSourceOptions.Add(source);
            }

            if (SelectedNode == null)
            {
                return;
            }

            var keys = SelectedNode.Settings
                .Select(setting => setting.Key)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase);

            foreach (var key in keys)
            {
                if (!ExtractorSourceOptions.Contains(key))
                {
                    ExtractorSourceOptions.Add(key);
                }
            }

            RefreshAssertionJsonTreePanel();
        }

        private void AddExtractorButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null)
            {
                return;
            }

            SelectedNode.Extractors.Add(new VariableExtractionRule(string.Empty, string.Empty, string.Empty));
            RefreshJsonPreview();
        }

        private void RemoveExtractorButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || sender is not Button button || button.DataContext is not VariableExtractionRule extractor)
            {
                return;
            }

            SelectedNode.Extractors.Remove(extractor);
            RefreshJsonPreview();
        }

        private void ExploreJsonPathButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || sender is not Button button || button.DataContext == null)
            {
                return;
            }

            var (source, currentPath, applyPath) = button.DataContext switch
            {
                VariableExtractionRule extractor =>
                    (extractor.Source, extractor.JsonPath, new Action<string>(path => extractor.JsonPath = path)),
                AssertionRule assertion =>
                    (assertion.Source, assertion.JsonPath, new Action<string>(path => assertion.JsonPath = path)),
                _ => (string.Empty, string.Empty, new Action<string>(_ => { }))
            };

            if (string.IsNullOrWhiteSpace(source))
            {
                MessageBox.Show("Select a source first.", "JSON Path Explorer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sourceValue = ResolvePreviewSourceValue(source);
            if (string.IsNullOrWhiteSpace(sourceValue))
            {
                MessageBox.Show("Selected source is empty. Run or preview the component first.", "JSON Path Explorer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!TryParseJsonRoot(sourceValue, out var rootElement))
            {
                MessageBox.Show("Selected source is not a valid JSON document.", "JSON Path Explorer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedPath = ShowJsonPathPicker(rootElement, currentPath);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            applyPath(selectedPath);
            RefreshJsonPreview();
        }

        private void AddAssertionButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || SelectedNode.Type == "Project")
            {
                return;
            }

            var defaultSource = ExtractorSourceOptions.FirstOrDefault() ?? "PreviewResponse";
            SelectedNode.Assertions.Add(new AssertionRule(defaultSource, "$", "Equals", string.Empty, "Assert"));
            RefreshJsonPreview();
        }

        private void RemoveAssertionButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || sender is not Button button || button.DataContext is not AssertionRule assertion)
            {
                return;
            }

            SelectedNode.Assertions.Remove(assertion);
            RefreshJsonPreview();
        }

        private void AssertionTreeSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingAssertionTreeSource)
            {
                return;
            }

            _assertionTreeSource = AssertionTreeSourceComboBox?.SelectedItem as string ?? string.Empty;
            RefreshAssertionJsonTreePanel();
        }

        private void RefreshAssertionTreeButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshAssertionJsonTreePanel();
        }

        private void AssertionJsonTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (AssertionJsonTreeView?.SelectedItem is not TreeViewItem selectedItem
                || selectedItem.Tag is not AssertionTreeNodeTag tag)
            {
                if (AssertionTreeSelectedPathTextBlock != null)
                {
                    AssertionTreeSelectedPathTextBlock.Text = "Path: (none)";
                }

                if (AssertionTreeValuePreviewTextBox != null)
                {
                    AssertionTreeValuePreviewTextBox.Text = "Select a tree node to preview value.";
                }

                return;
            }

            if (AssertionTreeSelectedPathTextBlock != null)
            {
                AssertionTreeSelectedPathTextBlock.Text = $"Path: {tag.Path}";
            }

            if (AssertionTreeValuePreviewTextBox != null)
            {
                AssertionTreeValuePreviewTextBox.Text = selectedItem.DataContext is JsonElement element
                    ? GetJsonElementPreview(element)
                    : tag.Expected;
            }
        }

        private void AddSelectedTreeNodeAssertionButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || SelectedNode.Type == "Project")
            {
                return;
            }

            if (AssertionJsonTreeView?.SelectedItem is not TreeViewItem selectedItem
                || selectedItem.Tag is not AssertionTreeNodeTag tag)
            {
                MessageBox.Show("Select a tree item first.", "Assertion Tree", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.Equals(tag.Path, "$", StringComparison.Ordinal))
            {
                MessageBox.Show("Select a node below root '$' to create an assertion row.", "Assertion Tree", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AddOrUpdateAssertionFromTreeTag(tag);
            RefreshJsonPreview();
        }

        private void GenerateTreeAssertionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || SelectedNode.Type == "Project" || AssertionJsonTreeView == null)
            {
                return;
            }

            // Generate from selected node subtree; fall back to root when nothing is selected.
            var selectedTreeItem = AssertionJsonTreeView.SelectedItem as TreeViewItem;
            var generationRoot = selectedTreeItem ?? AssertionJsonTreeView.Items.OfType<TreeViewItem>().FirstOrDefault();
            if (generationRoot == null)
            {
                MessageBox.Show("No tree items available to generate assertions.", "Assertion Tree", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var allTags = EnumerateAssertionTreeTags(generationRoot)
                .Where(tag => tag.IsLeaf)
                .Where(tag => !string.Equals(tag.Path, "$", StringComparison.Ordinal))
                .ToList();

            if (allTags.Count == 0)
            {
                MessageBox.Show("Selected tree node has no child paths to generate assertion rows.", "Assertion Tree", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var changedCount = 0;
            foreach (var tag in allTags)
            {
                if (AddOrUpdateAssertionFromTreeTag(tag))
                {
                    changedCount++;
                }
            }

            RefreshJsonPreview();
            MessageBox.Show($"Generated/updated {changedCount} assertion row(s).", "Assertion Tree", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private bool AddOrUpdateAssertionFromTreeTag(AssertionTreeNodeTag tag)
        {
            if (SelectedNode == null || SelectedNode.Type == "Project")
            {
                return false;
            }

            var existing = SelectedNode.Assertions.FirstOrDefault(assertion =>
                string.Equals(assertion.Source, tag.Source, StringComparison.OrdinalIgnoreCase)
                && string.Equals(assertion.JsonPath, tag.Path, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                SelectedNode.Assertions.Add(new AssertionRule(tag.Source, tag.Path, "Equals", tag.Expected, "Assert"));
                return true;
            }

            var changed = false;
            if (!string.Equals(existing.Expected, tag.Expected, StringComparison.Ordinal))
            {
                existing.Expected = tag.Expected;
                changed = true;
            }

            if (!string.Equals(existing.Condition, "Equals", StringComparison.OrdinalIgnoreCase))
            {
                existing.Condition = "Equals";
                changed = true;
            }

            return changed;
        }

        private IEnumerable<AssertionTreeNodeTag> EnumerateAssertionTreeTags(TreeViewItem item)
        {
            if (item.Tag is AssertionTreeNodeTag tag)
            {
                yield return tag;
            }

            foreach (var child in item.Items.OfType<TreeViewItem>())
            {
                foreach (var nested in EnumerateAssertionTreeTags(child))
                {
                    yield return nested;
                }
            }
        }

        private void RefreshAssertionJsonTreePanel()
        {
            if (AssertionJsonTreeView == null
                || AssertionTreeSourceComboBox == null
                || AssertionTreeValuePreviewTextBox == null
                || AssertionTreeSelectedPathTextBlock == null)
            {
                return;
            }

            AssertionJsonTreeView.Items.Clear();
            AssertionTreeSelectedPathTextBlock.Text = "Path: (none)";
            AssertionTreeValuePreviewTextBox.Text = "Select a tree node to preview value.";

            if (SelectedNode == null || string.Equals(SelectedNode.Type, "Project", StringComparison.OrdinalIgnoreCase))
            {
                AssertionTreeValuePreviewTextBox.Text = "Select a component node to use assertion tree.";
                return;
            }

            var options = ExtractorSourceOptions.ToList();
            if (options.Count == 0)
            {
                AssertionTreeValuePreviewTextBox.Text = "No assertion sources available.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_assertionTreeSource) || !options.Contains(_assertionTreeSource))
            {
                _assertionTreeSource = options.First();
            }

            _isSyncingAssertionTreeSource = true;
            AssertionTreeSourceComboBox.SelectedItem = _assertionTreeSource;
            _isSyncingAssertionTreeSource = false;

            var sourceValue = ResolvePreviewSourceValue(_assertionTreeSource);
            if (string.IsNullOrWhiteSpace(sourceValue))
            {
                AssertionTreeValuePreviewTextBox.Text = "Source has no data yet. Run or preview component first.";
                return;
            }

            if (!TryParseJsonRoot(sourceValue, out var rootElement))
            {
                AssertionTreeValuePreviewTextBox.Text = "Selected source is not valid JSON.";
                return;
            }

            var rootItem = CreateAssertionTreePanelItem("$", "$", rootElement, _assertionTreeSource);
            rootItem.IsExpanded = true;
            AssertionJsonTreeView.Items.Add(rootItem);
            rootItem.IsSelected = true;
        }

        private static TreeViewItem CreateAssertionTreePanelItem(string label, string path, JsonElement element, string source)
        {
            var item = new TreeViewItem
            {
                Header = BuildJsonTreeLabel(label, element),
                Tag = new AssertionTreeNodeTag
                {
                    Source = source,
                    Path = path,
                    Expected = BuildExpectedValueFromElement(element),
                    IsLeaf = true
                },
                DataContext = element
            };

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    var childPath = path + "." + property.Name;
                    item.Items.Add(CreateAssertionTreePanelItem(property.Name, childPath, property.Value, source));
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var child in element.EnumerateArray())
                {
                    var childLabel = "[" + index + "]";
                    var childPath = path + childLabel;
                    item.Items.Add(CreateAssertionTreePanelItem(childLabel, childPath, child, source));
                    index++;
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                var stringValue = element.GetString() ?? string.Empty;
                if (TryParseJsonString(stringValue, out var embeddedJson))
                {
                    if (embeddedJson.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in embeddedJson.EnumerateObject())
                        {
                            var childPath = path + "." + property.Name;
                            item.Items.Add(CreateAssertionTreePanelItem(property.Name, childPath, property.Value, source));
                        }
                    }
                    else if (embeddedJson.ValueKind == JsonValueKind.Array)
                    {
                        var index = 0;
                        foreach (var child in embeddedJson.EnumerateArray())
                        {
                            var childLabel = "[" + index + "]";
                            var childPath = path + childLabel;
                            item.Items.Add(CreateAssertionTreePanelItem(childLabel, childPath, child, source));
                            index++;
                        }
                    }
                }
            }

            if (item.Tag is AssertionTreeNodeTag tag)
            {
                tag.IsLeaf = item.Items.Count == 0;
            }

            return item;
        }

        private void ExploreAssertionExpectedJsonButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || sender is not Button button || button.DataContext is not AssertionRule assertion)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(assertion.Source))
            {
                MessageBox.Show("Select a source first.", "Assertion JSON Tree", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sourceValue = ResolvePreviewSourceValue(assertion.Source);
            if (string.IsNullOrWhiteSpace(sourceValue))
            {
                MessageBox.Show("Selected source is empty. Run or preview the component first.", "Assertion JSON Tree", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!TryParseJsonRoot(sourceValue, out var rootElement))
            {
                MessageBox.Show("Selected source is not a valid JSON document.", "Assertion JSON Tree", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selection = ShowJsonSelectionPicker(rootElement, assertion.JsonPath, "Assertion JSON Tree", "Use Node");
            if (!selection.HasValue)
            {
                return;
            }

            assertion.JsonPath = selection.Value.Path;
            assertion.Expected = selection.Value.ExpectedValue;
            RefreshJsonPreview();
        }

        private void ScriptCodeTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (SelectedNode == null || !string.Equals(SelectedNode.Type, "Script", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var editor = new ScriptEditorWindow("Script Component Editor", ScriptLanguage, ScriptCode)
            {
                Owner = this
            };

            if (editor.ShowDialog() == true)
            {
                ScriptLanguage = string.IsNullOrWhiteSpace(editor.ScriptLanguage) ? "CSharp" : editor.ScriptLanguage;
                ScriptCode = editor.ScriptText;
            }

            e.Handled = true;
        }

        private void SqlQueryTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (SelectedNode == null || !string.Equals(SelectedNode.Type, "Sql", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            const string sqlInstructions =
                "SQL query editor. This editor supports multi-line SQL text.\n\n"
                + "Tips:\n"
                + "- Use ${varName} placeholders to inject runtime/project variables.\n"
                + "- Example: SELECT * FROM users WHERE env = '${env}'\n"
                + "- Use Save to apply changes to the SQL component query.";

            var editor = new ScriptEditorWindow(
                title: "SQL Query Editor",
                language: "Sql",
                script: SqlQuery,
                openScriptTabOnLoad: true,
                allowExecutionActions: false,
                lockLanguage: true,
                instructionsOverride: sqlInstructions)
            {
                Owner = this
            };

            if (editor.ShowDialog() == true)
            {
                SqlQuery = editor.ScriptText;
            }

            e.Handled = true;
        }

        private void AssertionExpectedTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.DataContext is not AssertionRule assertion)
            {
                return;
            }

            if (string.Equals(assertion.Condition, "Script", StringComparison.OrdinalIgnoreCase))
            {
                var (language, scriptBody) = ParseAssertionScript(assertion.Expected);
                var scriptEditor = new ScriptEditorWindow("Assertion Script Editor", language, scriptBody, openScriptTabOnLoad: true)
                {
                    Owner = this
                };

                if (scriptEditor.ShowDialog() == true)
                {
                    assertion.Expected = BuildAssertionScript(scriptEditor.ScriptLanguage, scriptEditor.ScriptText);
                    RefreshJsonPreview();
                }

                e.Handled = true;
                return;
            }

            var editedValue = ShowExpectedValueEditor(assertion.Expected);
            if (editedValue != null)
            {
                assertion.Expected = editedValue;
                RefreshJsonPreview();
            }

            e.Handled = true;
        }

        private string? ShowExpectedValueEditor(string currentValue)
        {
            var dialog = new Window
            {
                Title = "Assertion Expected Editor",
                Width = 800,
                Height = 460,
                MinWidth = 620,
                MinHeight = 320,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResize
            };

            var root = new Grid
            {
                Margin = new Thickness(12)
            };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var editor = new TextBox
            {
                Text = currentValue ?? string.Empty,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            };
            Grid.SetRow(editor, 0);
            root.Children.Add(editor);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 90,
                IsCancel = true
            };

            var saveButton = new Button
            {
                Content = "Save",
                Width = 90,
                Margin = new Thickness(8, 0, 0, 0),
                IsDefault = true
            };
            saveButton.Click += (_, _) => dialog.DialogResult = true;

            buttons.Children.Add(cancelButton);
            buttons.Children.Add(saveButton);

            Grid.SetRow(buttons, 1);
            root.Children.Add(buttons);

            dialog.Content = root;
            editor.Focus();

            return dialog.ShowDialog() == true ? editor.Text : null;
        }

        private static (string Language, string Script) ParseAssertionScript(string input)
        {
            var script = input ?? string.Empty;
            var normalized = script.Replace("\r\n", "\n");
            var lines = normalized.Split('\n');
            if (lines.Length == 0)
            {
                return ("CSharp", script);
            }

            var firstLine = lines[0].Trim();
            if (!firstLine.StartsWith("//lang", StringComparison.OrdinalIgnoreCase))
            {
                return ("CSharp", script);
            }

            var parts = firstLine.Split(':', 2, StringSplitOptions.TrimEntries);
            var language = parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1])
                ? parts[1]
                : "CSharp";
            var remainingScript = string.Join("\n", lines.Skip(1));
            return (language, remainingScript);
        }

        private static string BuildAssertionScript(string language, string script)
        {
            var normalizedLanguage = string.IsNullOrWhiteSpace(language) ? "CSharp" : language.Trim();
            var normalizedScript = script ?? string.Empty;
            if (string.Equals(normalizedLanguage, "CSharp", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedScript;
            }

            return $"//lang:{normalizedLanguage}\n{normalizedScript}";
        }

        private static bool TryParseJsonRoot(string json, out JsonElement rootElement)
        {
            rootElement = default;
            try
            {
                using var doc = JsonDocument.Parse(json);
                rootElement = doc.RootElement.Clone();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string? ShowJsonPathPicker(JsonElement rootElement, string currentPath)
        {
            var selection = ShowJsonSelectionPicker(rootElement, currentPath, "JSON Path Explorer", "Use Path");
            return selection?.Path;
        }

        private (string Path, string ExpectedValue)? ShowJsonSelectionPicker(
            JsonElement rootElement,
            string currentPath,
            string title,
            string confirmButtonText)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 900,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.CanResize,
                MinWidth = 700,
                MinHeight = 350
            };

            var root = new Grid
            {
                Margin = new Thickness(12)
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock
            {
                Text = "Select JSON path:",
                Margin = new Thickness(0, 0, 0, 8),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tree = new TreeView
            {
                Margin = new Thickness(0, 0, 0, 10)
            };

            var rootItem = CreateJsonTreeItem("$", "$", rootElement);
            rootItem.IsExpanded = true;
            tree.Items.Add(rootItem);

            var valuePreview = new TextBox
            {
                Margin = new Thickness(10, 0, 0, 10),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                IsReadOnly = true,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var selectedPathBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 10),
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas")
            };

            string selectedExpectedValue = string.Empty;

            tree.SelectedItemChanged += (_, _) =>
            {
                if (tree.SelectedItem is not TreeViewItem selectedItem)
                {
                    return;
                }

                selectedPathBox.Text = selectedItem.Tag as string ?? string.Empty;
                if (selectedItem.DataContext is JsonElement selectedElement)
                {
                    valuePreview.Text = GetJsonElementPreview(selectedElement);
                    selectedExpectedValue = BuildExpectedValueFromElement(selectedElement);
                }
                else
                {
                    valuePreview.Text = string.Empty;
                    selectedExpectedValue = string.Empty;
                }
            };

            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                var matched = FindTreeItemByPath(rootItem, currentPath);
                if (matched != null)
                {
                    ExpandParents(matched);
                    matched.IsSelected = true;
                }
            }
            else
            {
                rootItem.IsSelected = true;
            }

            Grid.SetColumn(tree, 0);
            Grid.SetColumn(valuePreview, 1);
            contentGrid.Children.Add(tree);
            contentGrid.Children.Add(valuePreview);

            Grid.SetRow(contentGrid, 1);
            root.Children.Add(contentGrid);

            Grid.SetRow(selectedPathBox, 2);
            root.Children.Add(selectedPathBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Margin = new Thickness(0, 0, 8, 0)
            };
            cancelButton.Click += (_, _) => dialog.DialogResult = false;

            var okButton = new Button
            {
                Content = confirmButtonText,
                Width = 90,
                IsDefault = true
            };
            okButton.Click += (_, _) => dialog.DialogResult = !string.IsNullOrWhiteSpace(selectedPathBox.Text);

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(okButton);
            Grid.SetRow(buttonPanel, 3);
            root.Children.Add(buttonPanel);

            dialog.Content = root;

            var accepted = dialog.ShowDialog();
            if (accepted != true || string.IsNullOrWhiteSpace(selectedPathBox.Text))
            {
                return null;
            }

            return (selectedPathBox.Text, selectedExpectedValue);
        }

        private static string BuildExpectedValueFromElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.GetRawText(),
                JsonValueKind.Array => element.GetRawText(),
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => GetJsonElementPreview(element)
            };
        }

        private static TreeViewItem CreateJsonTreeItem(string label, string path, JsonElement element)
        {
            var item = new TreeViewItem
            {
                Header = BuildJsonTreeLabel(label, element),
                Tag = path,
                DataContext = element
            };

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    var childPath = path + "." + property.Name;
                    item.Items.Add(CreateJsonTreeItem(property.Name, childPath, property.Value));
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var child in element.EnumerateArray())
                {
                    var childLabel = "[" + index + "]";
                    var childPath = path + childLabel;
                    item.Items.Add(CreateJsonTreeItem(childLabel, childPath, child));
                    index++;
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                var stringValue = element.GetString() ?? string.Empty;
                if (TryParseJsonString(stringValue, out var embeddedJson))
                {
                    if (embeddedJson.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in embeddedJson.EnumerateObject())
                        {
                            var childPath = path + "." + property.Name;
                            item.Items.Add(CreateJsonTreeItem(property.Name, childPath, property.Value));
                        }
                    }
                    else if (embeddedJson.ValueKind == JsonValueKind.Array)
                    {
                        var index = 0;
                        foreach (var child in embeddedJson.EnumerateArray())
                        {
                            var childLabel = "[" + index + "]";
                            var childPath = path + childLabel;
                            item.Items.Add(CreateJsonTreeItem(childLabel, childPath, child));
                            index++;
                        }
                    }
                }
            }

            return item;
        }

        private static string BuildJsonTreeLabel(string label, JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => label + " (object)",
                JsonValueKind.Array => label + " (array)",
                JsonValueKind.String => label + " = \"" + (element.GetString() ?? string.Empty) + "\"",
                JsonValueKind.Number => label + " = " + element.GetRawText(),
                JsonValueKind.True => label + " = true",
                JsonValueKind.False => label + " = false",
                JsonValueKind.Null => label + " = null",
                _ => label
            };
        }

        private static string GetJsonElementPreview(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => element.GetRawText()
            };
        }

        private static TreeViewItem? FindTreeItemByPath(TreeViewItem current, string path)
        {
            if (string.Equals(current.Tag as string, path, StringComparison.Ordinal))
            {
                return current;
            }

            foreach (var child in current.Items)
            {
                if (child is TreeViewItem childItem)
                {
                    var found = FindTreeItemByPath(childItem, path);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private static void ExpandParents(TreeViewItem item)
        {
            var current = item;
            while (current != null)
            {
                current.IsExpanded = true;
                current = current.Parent as TreeViewItem;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}