using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Test_Automation.Models.Editor
{
    public class AssertionRule : INotifyPropertyChanged
    {
        private const string DefaultMode = "Assert";
        private string _source;
        private string _jsonPath;
        private string _mode;
        private string _condition;
        private string _expected;
        private string _lastResultState;
        private string _lastMessage;

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

        public string Mode
        {
            get => _mode;
            set
            {
                var normalized = NormalizeMode(value);

                if (_mode == normalized) return;
                _mode = normalized;
                OnPropertyChanged();
            }
        }

        public string Condition
        {
            get => _condition;
            set
            {
                if (_condition == value) return;
                _condition = value;
                OnPropertyChanged();
            }
        }

        public string Expected
        {
            get => _expected;
            set
            {
                if (_expected == value) return;
                _expected = value;
                OnPropertyChanged();
            }
        }

        public string LastResultState
        {
            get => _lastResultState;
            set
            {
                if (_lastResultState == value) return;
                _lastResultState = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusLabel));
            }
        }

        public string LastMessage
        {
            get => _lastMessage;
            set
            {
                if (_lastMessage == value) return;
                _lastMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public string StatusLabel
        {
            get
            {
                return LastResultState switch
                {
                    "Passed" => "Passed",
                    "AssertFailed" => "Assert failed",
                    "ExpectFailed" => "Expect failed",
                    _ => "Not run"
                };
            }
        }

        public string StatusMessage => string.IsNullOrWhiteSpace(LastMessage) ? StatusLabel : LastMessage;

        public AssertionRule(string source, string jsonPath, string condition, string expected, string mode = DefaultMode)
        {
            _source = source;
            _jsonPath = jsonPath;
            _mode = NormalizeMode(mode);
            _condition = string.IsNullOrWhiteSpace(condition) ? "Equals" : condition;
            _expected = expected;
            _lastResultState = "NotRun";
            _lastMessage = string.Empty;
        }

        private static string NormalizeMode(string? mode)
        {
            if (string.Equals(mode, "Expect", System.StringComparison.OrdinalIgnoreCase))
            {
                return "Expect";
            }

            if (string.Equals(mode, "Assert and Stop", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "AssertAndStop", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "Assertion and Stop", System.StringComparison.OrdinalIgnoreCase))
            {
                return "Assert and Stop";
            }

            return "Assert";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
