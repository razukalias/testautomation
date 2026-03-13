using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Test_Automation.Models.Editor
{
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
}
