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
        public List<NodeFileModel> Children { get; set; } = new List<NodeFileModel>();
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

    public class PlanNode : INotifyPropertyChanged
    {
        public string Type { get; }

        private string _name;
        private bool _isEnabled;

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

        public PlanNode? Parent { get; set; }
        public ObservableCollection<PlanNode> Children { get; } = new ObservableCollection<PlanNode>();
        public ObservableCollection<NodeSetting> Settings { get; } = new ObservableCollection<NodeSetting>();

        public string DisplayName => $"{Type}: {Name}";

        public PlanNode(string type, string name)
        {
            Type = type;
            _name = name;
            _isEnabled = true;
            ApplyDefaultSettings(type);
        }

        private void ApplyDefaultSettings(string type)
        {
            if (type == "Http")
            {
                Settings.Add(new NodeSetting("Method", "GET"));
                Settings.Add(new NodeSetting("Url", "https://api.example.com"));
            }
            else if (type == "GraphQl")
            {
                Settings.Add(new NodeSetting("Endpoint", "https://api.example.com/graphql"));
                Settings.Add(new NodeSetting("Query", "query { health }"));
                Settings.Add(new NodeSetting("Variables", "{}"));
            }
            else if (type == "Sql")
            {
                Settings.Add(new NodeSetting("Connection", ""));
                Settings.Add(new NodeSetting("Query", "SELECT 1"));
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

        private PlanNode? _selectedNode;
        private string _jsonPreview = "{}";
        private string _previewRequest = "Select a component to see request preview.";
        private string _previewResponse = "Select a component to see response preview.";
        private string _previewLogs = "Logs will appear here.";

        public PlanNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode == value) return;
                _selectedNode = value;
                OnPropertyChanged();
                NotifySelectedNodeEditorProperties();
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

        public bool IsProjectSelected => SelectedNode?.Type == "Project";
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

            var cloneItem = new MenuItem { Header = "Clone" };
            cloneItem.Click += (_, _) => CloneNode(selectedNode);
            menu.Items.Add(cloneItem);

            var removeItem = new MenuItem { Header = "Delete" };
            removeItem.Click += (_, _) => RemoveNode(selectedNode);
            menu.Items.Add(removeItem);

            return menu;
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

            foreach (var setting in node.Settings)
            {
                setting.PropertyChanged += NodeSetting_PropertyChanged;
            }
        }

        private void UnregisterNode(PlanNode node)
        {
            node.PropertyChanged -= PlanNode_PropertyChanged;
            node.Children.CollectionChanged -= NodeChildren_CollectionChanged;
            node.Settings.CollectionChanged -= NodeSettings_CollectionChanged;

            foreach (var setting in node.Settings)
            {
                setting.PropertyChanged -= NodeSetting_PropertyChanged;
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
                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = "Http",
                    method,
                    url,
                    headers = new Dictionary<string, string>
                    {
                        ["Content-Type"] = "application/json"
                    },
                    body = "{ \"sample\": true }"
                }, new JsonSerializerOptions { WriteIndented = true });

                PreviewResponse = JsonSerializer.Serialize(new
                {
                    status = 200,
                    reason = "OK",
                    body = "{ \"result\": \"success\" }",
                    durationMs = 128
                }, new JsonSerializerOptions { WriteIndented = true });

                PreviewLogs = $"[{now}] HTTP request prepared\n[{now}] Target: {method} {url}\n[{now}] Mock response received: 200 OK";
                return;
            }

            if (nodeType == "GraphQl")
            {
                var endpoint = GetSettingValue("Endpoint", "https://api.example.com/graphql");
                var query = GetSettingValue("Query", "query { health }");
                var variables = GetSettingValue("Variables", "{}");
                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = "GraphQl",
                    endpoint,
                    query,
                    variables
                }, new JsonSerializerOptions { WriteIndented = true });

                PreviewResponse = JsonSerializer.Serialize(new
                {
                    status = 200,
                    data = new
                    {
                        health = "ok"
                    },
                    errors = new object[] { },
                    durationMs = 95
                }, new JsonSerializerOptions { WriteIndented = true });

                PreviewLogs = $"[{now}] GraphQL request prepared\n[{now}] Endpoint: {endpoint}\n[{now}] Mock GraphQL response received.";
                return;
            }

            if (nodeType == "Sql")
            {
                var connection = GetSettingValue("Connection", "Server=.;Database=master;Trusted_Connection=True;");
                var query = GetSettingValue("Query", "SELECT 1");
                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = "Sql",
                    connection,
                    query
                }, new JsonSerializerOptions { WriteIndented = true });

                PreviewResponse = JsonSerializer.Serialize(new
                {
                    rows = new[]
                    {
                        new Dictionary<string, object> { ["Result"] = 1 }
                    },
                    affectedRows = 1,
                    durationMs = 42
                }, new JsonSerializerOptions { WriteIndented = true });

                PreviewLogs = $"[{now}] SQL query prepared\n[{now}] Executing: {query}\n[{now}] Mock result returned with 1 row.";
                return;
            }

            if (nodeType == "Script")
            {
                var language = GetSettingValue("Language", "CSharp");
                var code = GetSettingValue("Code", string.Empty);
                PreviewRequest = JsonSerializer.Serialize(new
                {
                    component = nodeName,
                    type = "Script",
                    language,
                    code
                }, new JsonSerializerOptions { WriteIndented = true });

                PreviewResponse = JsonSerializer.Serialize(new
                {
                    output = "Script execution simulated.",
                    exitCode = 0,
                    durationMs = 15
                }, new JsonSerializerOptions { WriteIndented = true });

                PreviewLogs = $"[{now}] Script compiled (simulated)\n[{now}] Script executed with exit code 0.";
                return;
            }

            var settings = SelectedNode.Settings
                .Where(setting => !string.IsNullOrWhiteSpace(setting.Key))
                .ToDictionary(setting => setting.Key, setting => setting.Value);

            PreviewRequest = JsonSerializer.Serialize(new
            {
                component = nodeName,
                type = nodeType,
                settings
            }, new JsonSerializerOptions { WriteIndented = true });

            PreviewResponse = JsonSerializer.Serialize(new
            {
                message = "Preview available when this component is executed.",
                component = nodeName,
                type = nodeType
            }, new JsonSerializerOptions { WriteIndented = true });

            PreviewLogs = $"[{now}] {nodeType} preview refreshed.";
        }

        private static object BuildNodeObject(PlanNode node)
        {
            var settings = node.Settings
                .Where(setting => !string.IsNullOrWhiteSpace(setting.Key))
                .ToDictionary(setting => setting.Key, setting => setting.Value);

            return new
            {
                type = node.Type,
                name = node.Name,
                enabled = node.IsEnabled,
                settings,
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
            OnPropertyChanged(nameof(GraphQlEndpoint));
            OnPropertyChanged(nameof(GraphQlQuery));
            OnPropertyChanged(nameof(GraphQlVariables));
            OnPropertyChanged(nameof(SqlConnection));
            OnPropertyChanged(nameof(SqlQuery));
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}