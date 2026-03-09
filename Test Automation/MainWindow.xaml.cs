using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Test_Automation.Factories;
using Test_Automation.Models;
using Test_Automation.Services;

namespace Test_Automation
{
    public class ProjectFileModel
    {
        public int Version { get; set; } = 1;
        public NodeFileModel? Project { get; set; }
    }

    public class NodeFileModel
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
        public List<VariableExtractionFileModel> Extractors { get; set; } = new List<VariableExtractionFileModel>();
        public List<NodeFileModel> Children { get; set; } = new List<NodeFileModel>();
    }

    public class VariableExtractionFileModel
    {
        public string Source { get; set; } = string.Empty;
        public string JsonPath { get; set; } = string.Empty;
        public string VariableName { get; set; } = string.Empty;
    }

    public class NodeSetting : INotifyPropertyChanged
    {
        private string _key;
        private string _value;

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

        public NodeSetting(string key, string value)
        {
            _key = key;
            _value = value;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class VariableExtractionRule : INotifyPropertyChanged
    {
        private string _source;
        private string _jsonPath;
        private string _variableName;

        public string Source
        {
            get => _source;
            set
            {
                if (_source == value) return;
                _source = value;
                OnPropertyChanged();
            }
        }

        public string JsonPath
        {
            get => _jsonPath;
            set
            {
                if (_jsonPath == value) return;
                _jsonPath = value;
                OnPropertyChanged();
            }
        }

        public string VariableName
        {
            get => _variableName;
            set
            {
                if (_variableName == value) return;
                _variableName = value;
                OnPropertyChanged();
            }
        }

        public VariableExtractionRule(string source, string jsonPath, string variableName)
        {
            _source = source;
            _jsonPath = jsonPath;
            _variableName = variableName;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PlanNode : INotifyPropertyChanged
    {
        public string Type { get; }

        private string _name;
        private bool _isEnabled;
        private bool _isExpanded;

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public PlanNode? Parent { get; set; }
        public ObservableCollection<PlanNode> Children { get; } = new ObservableCollection<PlanNode>();
        public ObservableCollection<NodeSetting> Settings { get; } = new ObservableCollection<NodeSetting>();
        public ObservableCollection<VariableExtractionRule> Extractors { get; } = new ObservableCollection<VariableExtractionRule>();

        public string DisplayName => $"{Type}: {Name}";

        public PlanNode(string type, string name)
        {
            Type = type;
            _name = name;
            _isEnabled = true;
            _isExpanded = true;
            ApplyDefaultSettings(type);
        }

        private void ApplyDefaultSettings(string type)
        {
            if (type == "Http")
            {
                Settings.Add(new NodeSetting("Method", "GET"));
                Settings.Add(new NodeSetting("Url", "https://api.example.com"));
                Settings.Add(new NodeSetting("Body", ""));
                Settings.Add(new NodeSetting("Headers", "{}"));
                Settings.Add(new NodeSetting("AuthType", "WindowsIntegrated"));
                Settings.Add(new NodeSetting("AuthUsername", ""));
                Settings.Add(new NodeSetting("AuthPassword", ""));
                Settings.Add(new NodeSetting("AuthToken", ""));
                Settings.Add(new NodeSetting("ApiKeyName", ""));
                Settings.Add(new NodeSetting("ApiKeyValue", ""));
                Settings.Add(new NodeSetting("ApiKeyLocation", "Header"));
                Settings.Add(new NodeSetting("OAuthTokenUrl", ""));
                Settings.Add(new NodeSetting("OAuthClientId", ""));
                Settings.Add(new NodeSetting("OAuthClientSecret", ""));
                Settings.Add(new NodeSetting("OAuthScope", ""));
            }
            else if (type == "GraphQl")
            {
                Settings.Add(new NodeSetting("Endpoint", "https://api.example.com/graphql"));
                Settings.Add(new NodeSetting("Query", "query { health }"));
                Settings.Add(new NodeSetting("Variables", "{}"));
                Settings.Add(new NodeSetting("Headers", "{}"));
                Settings.Add(new NodeSetting("AuthType", "WindowsIntegrated"));
                Settings.Add(new NodeSetting("AuthUsername", ""));
                Settings.Add(new NodeSetting("AuthPassword", ""));
                Settings.Add(new NodeSetting("AuthToken", ""));
                Settings.Add(new NodeSetting("ApiKeyName", ""));
                Settings.Add(new NodeSetting("ApiKeyValue", ""));
                Settings.Add(new NodeSetting("ApiKeyLocation", "Header"));
                Settings.Add(new NodeSetting("OAuthTokenUrl", ""));
                Settings.Add(new NodeSetting("OAuthClientId", ""));
                Settings.Add(new NodeSetting("OAuthClientSecret", ""));
                Settings.Add(new NodeSetting("OAuthScope", ""));
            }
            else if (type == "Sql")
            {
                Settings.Add(new NodeSetting("Connection", ""));
                Settings.Add(new NodeSetting("Query", "SELECT 1"));
                Settings.Add(new NodeSetting("AuthType", "WindowsIntegrated"));
                Settings.Add(new NodeSetting("AuthUsername", ""));
                Settings.Add(new NodeSetting("AuthPassword", ""));
            }
            else if (type == "Timer")
            {
                Settings.Add(new NodeSetting("DelayMs", "1000"));
            }
            else if (type == "Loop")
            {
                Settings.Add(new NodeSetting("Iterations", "1"));
            }
            else if (type == "Foreach")
            {
                Settings.Add(new NodeSetting("SourceVariable", "items"));
            }
            else if (type == "If")
            {
                Settings.Add(new NodeSetting("Condition", "${status} == 200"));
            }
            else if (type == "Threads")
            {
                Settings.Add(new NodeSetting("ThreadCount", "1"));
                Settings.Add(new NodeSetting("RampUpSeconds", "1"));
            }
            else if (type == "Assert")
            {
                Settings.Add(new NodeSetting("Expected", ""));
                Settings.Add(new NodeSetting("Actual", ""));
            }
            else if (type == "VariableExtractor")
            {
                Settings.Add(new NodeSetting("Pattern", ""));
                Settings.Add(new NodeSetting("VariableName", ""));
            }
            else if (type == "Script")
            {
                Settings.Add(new NodeSetting("Language", "CSharp"));
                Settings.Add(new NodeSetting("Code", ""));
            }
            else if (type == "Config")
            {
                Settings.Add(new NodeSetting("BaseUrl", ""));
            }
            else if (type == "TestPlan")
            {
                Settings.Add(new NodeSetting("Description", ""));
            }
            else if (type == "Project")
            {
                Settings.Add(new NodeSetting("Description", ""));
                Settings.Add(new NodeSetting("Environment", "dev"));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

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

        private PlanNode? _selectedNode;
        private string _jsonPreview = "{}";
        private string _previewRequest = "Select a component to see request preview.";
        private string _previewResponse = "Select a component to see response preview.";
        private string _previewLogs = "Logs will appear here.";
        private string _variablesPreview = "{}";
        private Test_Automation.Models.ExecutionContext? _lastExecutionContext;

        public PlanNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode == value) return;
                _selectedNode = value;
                OnPropertyChanged();
                NotifySelectedNodeEditorProperties();
                RebuildExtractorSourceOptions();
                RefreshComponentPreview();
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

        public bool IsProjectSelected => SelectedNode?.Type == "Project";
        public bool IsComponentSelected => SelectedNode != null && SelectedNode.Type != "Project";
        public bool HasSelectedNodeChildren => SelectedNode != null && SelectedNode.Children.Count > 0;
        public bool IsHttpSelected => SelectedNode?.Type == "Http";
        public bool IsGraphQlSelected => SelectedNode?.Type == "GraphQl";
        public bool IsSqlSelected => SelectedNode?.Type == "Sql";
        public bool IsTimerSelected => SelectedNode?.Type == "Timer";
        public bool IsLoopSelected => SelectedNode?.Type == "Loop";
        public bool IsIfSelected => SelectedNode?.Type == "If";
        public bool IsThreadsSelected => SelectedNode?.Type == "Threads";
        public bool IsAssertSelected => SelectedNode?.Type == "Assert";
        public bool IsVariableExtractorSelected => SelectedNode?.Type == "VariableExtractor";
        public bool IsScriptSelected => SelectedNode?.Type == "Script";

        public string ProjectDescription
        {
            get => GetSettingValue("Description", string.Empty);
            set => SetSettingValue("Description", value);
        }

        public string ProjectEnvironment
        {
            get => GetSettingValue("Environment", "dev");
            set => SetSettingValue("Environment", value);
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
            get => GetSettingValue("Connection", string.Empty);
            set => SetSettingValue("Connection", value);
        }

        public string SqlQuery
        {
            get => GetSettingValue("Query", string.Empty);
            set => SetSettingValue("Query", value);
        }

        public string SqlAuthType
        {
            get => GetSettingValue("AuthType", "WindowsIntegrated");
            set
            {
                SetSettingValue("AuthType", value);
                RaiseSqlAuthVisibilityChanged();
            }
        }

        public bool SqlShowBasicFields => string.Equals(SqlAuthType, "Basic", StringComparison.OrdinalIgnoreCase);

        public string SqlAuthUsername
        {
            get => GetSettingValue("AuthUsername", string.Empty);
            set => SetSettingValue("AuthUsername", value);
        }

        public string SqlAuthPassword
        {
            get => GetSettingValue("AuthPassword", string.Empty);
            set => SetSettingValue("AuthPassword", value);
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

        private static readonly string[] StepTypes =
        {
            "Http", "GraphQl", "Sql", "Assert", "VariableExtractor", "Script", "Timer"
        };

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            RootNodes.CollectionChanged += RootNodes_CollectionChanged;
            RefreshJsonPreview();
        }

        private void AddProjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (RootNodes.Any(node => node.Type == "Project"))
            {
                MessageBox.Show("Only one Project root is allowed.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var root = new PlanNode("Project", "Project");
            RootNodes.Add(root);
            SelectedNode = root;
            RefreshJsonPreview();
        }

        private void SaveProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var projectNode = RootNodes.FirstOrDefault(node => node.Type == "Project");
            if (projectNode == null)
            {
                MessageBox.Show("Create or load a Project first.", "Save Project", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                FileName = $"{projectNode.Name}.json"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var model = new ProjectFileModel
            {
                Version = 1,
                Project = ToFileModel(projectNode)
            };

            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(dialog.FileName, json);
            MessageBox.Show("Project saved successfully.", "Save Project", MessageBoxButton.OK, MessageBoxImage.Information);
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

            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var model = JsonSerializer.Deserialize<ProjectFileModel>(json);

                if (model?.Project == null)
                {
                    MessageBox.Show("Invalid project file format.", "Load Project", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var root = FromFileModel(model.Project, null);
                if (root.Type != "Project")
                {
                    MessageBox.Show("Root node must be of type Project.", "Load Project", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                RootNodes.Clear();
                RootNodes.Add(root);
                SelectedNode = root;
                RefreshJsonPreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load project: {ex.Message}", "Load Project", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

            var runner = new TestPlanRunner();
            var startTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            PreviewLogs = $"[{startTimestamp}] Running TestPlan: {testPlanNode.Name}";
            VariablesPreview = "{}";

            try
            {
                var context = new Test_Automation.Models.ExecutionContext();
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
            }
            catch (Exception ex)
            {
                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Run failed: {ex.Message}"
                });
                VariablesPreview = "{}";
            }
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

            var executor = new Test_Automation.Services.ComponentExecutor();
            var startTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            PreviewLogs = $"[{startTimestamp}] Running: {selectedNode.Name}";
            VariablesPreview = "{}";

            try
            {
                var context = new Test_Automation.Models.ExecutionContext();
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
            }
            catch (Exception ex)
            {
                var endTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                PreviewLogs = string.Join("\n", new[]
                {
                    PreviewLogs,
                    $"[{endTimestamp}] Run failed: {ex.Message}"
                });
                VariablesPreview = "{}";
            }
        }

        private Test_Automation.Componentes.Component? BuildComponentTree(PlanNode node)
        {
            if (node == null || !node.IsEnabled || node.Type == "Project")
            {
                return null;
            }

            var component = ComponentFactory.CreateComponent(node.Type);
            component.SetName(node.Name);

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
            e.Effects = canReorder ? DragDropEffects.Move : DragDropEffects.None;
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
            node.Extractors.CollectionChanged += NodeExtractors_CollectionChanged;

            foreach (var setting in node.Settings)
            {
                setting.PropertyChanged += NodeSetting_PropertyChanged;
            }

            foreach (var extractor in node.Extractors)
            {
                extractor.PropertyChanged += NodeExtractor_PropertyChanged;
            }
        }

        private void UnregisterNode(PlanNode node)
        {
            node.PropertyChanged -= PlanNode_PropertyChanged;
            node.Children.CollectionChanged -= NodeChildren_CollectionChanged;
            node.Settings.CollectionChanged -= NodeSettings_CollectionChanged;
            node.Extractors.CollectionChanged -= NodeExtractors_CollectionChanged;

            foreach (var setting in node.Settings)
            {
                setting.PropertyChanged -= NodeSetting_PropertyChanged;
            }

            foreach (var extractor in node.Extractors)
            {
                extractor.PropertyChanged -= NodeExtractor_PropertyChanged;
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
        }

        private void PlanNode_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifySelectedNodeEditorProperties();
            RefreshJsonPreview();
        }

        private void NodeSetting_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifySelectedNodeEditorProperties();
            RebuildExtractorSourceOptions();
            RefreshJsonPreview();
        }

        private void NodeExtractor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifySelectedNodeEditorProperties();
            RefreshJsonPreview();
        }

        private void RefreshJsonPreview()
        {
            var model = RootNodes.Select(BuildNodeObject).ToList();
            JsonPreview = JsonSerializer.Serialize(model, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            RefreshComponentPreview();
        }

        private void RefreshComponentPreview()
        {
            if (SelectedNode == null)
            {
                PreviewRequest = "Select a component to see request preview.";
                PreviewResponse = "Select a component to see response preview.";
                PreviewLogs = "Logs will appear here.";
                return;
            }

            var nodeType = SelectedNode.Type;
            var nodeName = SelectedNode.Name;
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            if (nodeType == "Http")
            {
                var method = GetSettingValue("Method", "GET");
                var url = GetSettingValue("Url", "https://api.example.com");
                var lastHttp = GetLastExecutionData<HttpData>(nodeName);
                var httpRuns = GetExecutionResults(nodeName)
                    .Where(result => result.Data is HttpData)
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        durationMs = result.DurationMs,
                        status = result.Status,
                        responseStatus = (result.Data as HttpData)?.ResponseStatus,
                        responseBody = (result.Data as HttpData)?.ResponseBody
                    })
                    .ToList();
                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = "Http",
                    method,
                    url
                }, new JsonSerializerOptions { WriteIndented = true });

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
                var lastGraphQl = GetLastExecutionData<GraphQlData>(nodeName);
                var graphRuns = GetExecutionResults(nodeName)
                    .Where(result => result.Data is GraphQlData)
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        durationMs = result.DurationMs,
                        status = result.Status,
                        responseStatus = (result.Data as GraphQlData)?.ResponseStatus,
                        responseBody = (result.Data as GraphQlData)?.ResponseBody
                    })
                    .ToList();
                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = "GraphQl",
                    endpoint,
                    query,
                    variables
                }, new JsonSerializerOptions { WriteIndented = true });

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
                var connection = GetSettingValue("Connection", "Server=.;Database=master;Trusted_Connection=True;");
                var query = GetSettingValue("Query", "SELECT 1");
                var lastSql = GetLastExecutionData<SqlData>(nodeName);
                var sqlRuns = GetExecutionResults(nodeName)
                    .Where(result => result.Data is SqlData)
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        durationMs = result.DurationMs,
                        status = result.Status,
                        rows = (result.Data as SqlData)?.QueryResult
                    })
                    .ToList();
                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = "Sql",
                    connection,
                    query
                }, new JsonSerializerOptions { WriteIndented = true });

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
                var childNames = GetDescendantNames(SelectedNode).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var lastResults = _lastExecutionContext?.Results
                    .Where(result => childNames.Contains(result.ComponentName))
                    .Select(result => (object)new
                    {
                        name = result.ComponentName,
                        threadIndex = result.ThreadIndex,
                        durationMs = result.DurationMs,
                        status = result.Status,
                        passed = result.Passed,
                        error = result.Error,
                        data = result.Data
                    })
                    .ToList() ?? new List<object>();

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
                var scriptRuns = GetExecutionResults(nodeName)
                    .Select(result => new
                    {
                        threadIndex = result.ThreadIndex,
                        durationMs = result.DurationMs,
                        status = result.Status,
                        output = (result.Data as ScriptData)?.ExecutionResult,
                        error = result.Error
                    })
                    .ToList();
                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = "Script",
                    language,
                    code
                }, new JsonSerializerOptions { WriteIndented = true });

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

            var genericRuns = GetExecutionResults(nodeName)
                .Select(result => new
                {
                    threadIndex = result.ThreadIndex,
                    durationMs = result.DurationMs,
                    status = result.Status,
                    error = result.Error,
                    data = result.Data
                })
                .ToList();

            PreviewRequest = JsonSerializer.Serialize(new
            {
                component = nodeName,
                type = nodeType,
                settings
            }, new JsonSerializerOptions { WriteIndented = true });

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
        }

        private void ClearVariablesButton_Click(object sender, RoutedEventArgs e)
        {
            VariablesPreview = string.Empty;
        }

        private void ClearChildrenPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || _lastExecutionContext == null)
            {
                return;
            }

            var descendantNames = GetDescendantNames(SelectedNode)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _lastExecutionContext.Results.RemoveAll(result => descendantNames.Contains(result.ComponentName));
            RefreshComponentPreview();
        }

        private void ClearSelectedNodePreviewWithChildren(PlanNode selectedNode)
        {
            if (selectedNode == null || _lastExecutionContext == null)
            {
                return;
            }

            var namesToClear = GetDescendantNames(selectedNode)
                .Concat(new[] { selectedNode.Name })
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _lastExecutionContext.Results.RemoveAll(result => namesToClear.Contains(result.ComponentName));
            RefreshComponentPreview();
        }

        private TData? GetLastExecutionData<TData>(string componentName) where TData : ComponentData
        {
            if (_lastExecutionContext == null)
            {
                return null;
            }

            return _lastExecutionContext.Results
                .Where(result => result.Data is TData && string.Equals(result.ComponentName, componentName, StringComparison.OrdinalIgnoreCase))
                .Select(result => result.Data)
                .OfType<TData>()
                .LastOrDefault();
        }

        private List<ExecutionResult> GetExecutionResults(string componentName)
        {
            if (_lastExecutionContext == null)
            {
                return new List<ExecutionResult>();
            }

            return _lastExecutionContext.Results
                .Where(result => string.Equals(result.ComponentName, componentName, StringComparison.OrdinalIgnoreCase))
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
                return PreviewResponse;
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

            if (!element.TryGetProperty(token, out var next))
            {
                return false;
            }

            element = next;
            return true;
        }

        private static object BuildNodeObject(PlanNode node)
        {
            var settings = node.Settings
                .Where(setting => !string.IsNullOrWhiteSpace(setting.Key))
                .ToDictionary(setting => setting.Key, setting => setting.Value);

            var extractors = node.Extractors
                .Where(extractor => !string.IsNullOrWhiteSpace(extractor.Source) || !string.IsNullOrWhiteSpace(extractor.VariableName))
                .Select(extractor => new
                {
                    source = extractor.Source,
                    jsonPath = extractor.JsonPath,
                    variableName = extractor.VariableName
                })
                .ToList();

            return new
            {
                type = node.Type,
                name = node.Name,
                enabled = node.IsEnabled,
                settings,
                extractors,
                children = node.Children.Select(BuildNodeObject).ToList()
            };
        }

        private static NodeFileModel ToFileModel(PlanNode node)
        {
            return new NodeFileModel
            {
                Type = node.Type,
                Name = node.Name,
                Enabled = node.IsEnabled,
                Settings = node.Settings
                    .Where(setting => !string.IsNullOrWhiteSpace(setting.Key))
                    .ToDictionary(setting => setting.Key, setting => setting.Value),
                Extractors = node.Extractors
                    .Where(extractor => !string.IsNullOrWhiteSpace(extractor.Source) || !string.IsNullOrWhiteSpace(extractor.VariableName))
                    .Select(extractor => new VariableExtractionFileModel
                    {
                        Source = extractor.Source,
                        JsonPath = extractor.JsonPath,
                        VariableName = extractor.VariableName
                    })
                    .ToList(),
                Children = node.Children.Select(ToFileModel).ToList()
            };
        }

        private static PlanNode FromFileModel(NodeFileModel model, PlanNode? parent)
        {
            var node = new PlanNode(model.Type, model.Name)
            {
                Parent = parent,
                IsEnabled = model.Enabled
            };

            node.Settings.Clear();
            foreach (var setting in model.Settings)
            {
                node.Settings.Add(new NodeSetting(setting.Key, setting.Value));
            }

            node.Extractors.Clear();
            foreach (var extractor in model.Extractors)
            {
                node.Extractors.Add(new VariableExtractionRule(extractor.Source, extractor.JsonPath, extractor.VariableName));
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

            var setting = SelectedNode.Settings.FirstOrDefault(current => current.Key == key);
            return setting?.Value ?? fallback;
        }

        private void SetSettingValue(string key, string value)
        {
            if (SelectedNode == null)
            {
                return;
            }

            var setting = SelectedNode.Settings.FirstOrDefault(current => current.Key == key);
            if (setting == null)
            {
                setting = new NodeSetting(key, value);
                SelectedNode.Settings.Add(setting);
            }
            else if (setting.Value != value)
            {
                setting.Value = value;
            }

            RefreshJsonPreview();
        }

        private void NotifySelectedNodeEditorProperties()
        {
            OnPropertyChanged(nameof(IsProjectSelected));
            OnPropertyChanged(nameof(IsComponentSelected));
            OnPropertyChanged(nameof(HasSelectedNodeChildren));
            OnPropertyChanged(nameof(IsHttpSelected));
            OnPropertyChanged(nameof(IsGraphQlSelected));
            OnPropertyChanged(nameof(IsSqlSelected));
            OnPropertyChanged(nameof(IsTimerSelected));
            OnPropertyChanged(nameof(IsLoopSelected));
            OnPropertyChanged(nameof(IsIfSelected));
            OnPropertyChanged(nameof(IsThreadsSelected));
            OnPropertyChanged(nameof(IsAssertSelected));
            OnPropertyChanged(nameof(IsVariableExtractorSelected));
            OnPropertyChanged(nameof(IsScriptSelected));

            OnPropertyChanged(nameof(ProjectDescription));
            OnPropertyChanged(nameof(ProjectEnvironment));
            OnPropertyChanged(nameof(HttpMethod));
            OnPropertyChanged(nameof(HttpUrl));
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
            OnPropertyChanged(nameof(SqlConnection));
            OnPropertyChanged(nameof(SqlQuery));
            OnPropertyChanged(nameof(SqlAuthType));
            RaiseSqlAuthVisibilityChanged();
            OnPropertyChanged(nameof(SqlAuthUsername));
            OnPropertyChanged(nameof(SqlAuthPassword));
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}