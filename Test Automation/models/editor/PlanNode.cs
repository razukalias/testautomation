using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Test_Automation.Models.Editor
{
    public class PlanNode : INotifyPropertyChanged
    {
        public string Id { get; }
        public string Type { get; }

        private string _name;
        private bool _isEnabled;
        private bool _isExpanded;
        private bool _isHighlighted;
        private int _assertFailedCount;
        private int _expectFailedCount;
        private int _assertPassedCount;

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

        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                if (_isHighlighted == value) return;
                _isHighlighted = value;
                OnPropertyChanged();
            }
        }

        public int AssertFailedCount
        {
            get => _assertFailedCount;
            set
            {
                if (_assertFailedCount == value) return;
                _assertFailedCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AssertionSummaryLabel));
                OnPropertyChanged(nameof(HasAssertionSummary));
                OnPropertyChanged(nameof(AssertionSeverity));
            }
        }

        public int ExpectFailedCount
        {
            get => _expectFailedCount;
            set
            {
                if (_expectFailedCount == value) return;
                _expectFailedCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AssertionSummaryLabel));
                OnPropertyChanged(nameof(HasAssertionSummary));
                OnPropertyChanged(nameof(AssertionSeverity));
            }
        }

        public int AssertionPassedCount
        {
            get => _assertPassedCount;
            set
            {
                if (_assertPassedCount == value) return;
                _assertPassedCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AssertionSummaryLabel));
                OnPropertyChanged(nameof(HasAssertionSummary));
                OnPropertyChanged(nameof(AssertionSeverity));
            }
        }

        public PlanNode? Parent { get; set; }
        public ObservableCollection<PlanNode> Children { get; } = new ObservableCollection<PlanNode>();
        public ObservableCollection<NodeSetting> Settings { get; } = new ObservableCollection<NodeSetting>();
        public ObservableCollection<NodeSetting> Variables { get; } = new ObservableCollection<NodeSetting>();
        public ObservableCollection<VariableExtractionRule> Extractors { get; } = new ObservableCollection<VariableExtractionRule>();
        public ObservableCollection<AssertionRule> Assertions { get; } = new ObservableCollection<AssertionRule>();

        public string DisplayName => $"{Type}: {Name}";
        public bool HasAssertionSummary => AssertFailedCount + ExpectFailedCount + AssertionPassedCount > 0;
        public string AssertionSummaryLabel => $"A:{AssertFailedCount} E:{ExpectFailedCount} P:{AssertionPassedCount}";
        public string AssertionSeverity
        {
            get
            {
                if (AssertFailedCount > 0)
                {
                    return "AssertFailed";
                }

                if (ExpectFailedCount > 0)
                {
                    return "ExpectFailed";
                }

                if (AssertionPassedCount > 0)
                {
                    return "Passed";
                }

                return "None";
            }
        }

        public PlanNode(string type, string name, string? id = null)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString() : id;
            Type = type;
            _name = name;
            _isEnabled = true;
            _isExpanded = true;
            _isHighlighted = false;
            ApplyDefaultSettings(type);
        }

        private void ApplyDefaultSettings(string type)
        {
            if (type == "Project")
            {
                Settings.Add(new NodeSetting("Description", string.Empty));
                Settings.Add(new NodeSetting("Environment", "dev"));
            }
            else if (type == "Http")
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
                Settings.Add(new NodeSetting("Description", string.Empty));
                Settings.Add(new NodeSetting("Environment", "dev"));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
