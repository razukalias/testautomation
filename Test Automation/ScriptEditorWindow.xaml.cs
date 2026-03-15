using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Test_Automation.Services;

namespace Test_Automation
{
    public partial class ScriptEditorWindow : Window
    {
        private readonly bool _openScriptTabOnLoad;
        private readonly bool _allowExecutionActions;

        private static readonly string ScriptEditorLayoutStatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TestAutomation",
            "layout.script-editor.json");

        private sealed class ScriptEditorLayoutState
        {
            public double TopRowHeight { get; set; }
            public double LogRowHeight { get; set; }
        }

        public string ScriptLanguage => LanguageTextBox.Text?.Trim() ?? "CSharp";
        public string ScriptText => ScriptTextBox.Text ?? string.Empty;

        public ScriptEditorWindow(
            string title,
            string language,
            string script,
            bool openScriptTabOnLoad = false,
            bool allowExecutionActions = true,
            bool lockLanguage = false,
            string? instructionsOverride = null)
        {
            InitializeComponent();
            _openScriptTabOnLoad = openScriptTabOnLoad;
            _allowExecutionActions = allowExecutionActions;
            Title = string.IsNullOrWhiteSpace(title) ? "Script Editor" : title;
            LanguageTextBox.Text = string.IsNullOrWhiteSpace(language) ? "CSharp" : language;
            LanguageTextBox.IsReadOnly = lockLanguage;
            ScriptTextBox.Text = script ?? string.Empty;
            InstructionsTextBox.Text = string.IsNullOrWhiteSpace(instructionsOverride)
                ? BuildInstructions()
                : instructionsOverride;
            Loaded += (_, _) =>
            {
                LoadScriptEditorLayoutState();
                SetValidateButtonState(null);
                UpdateColumnRuler();
                UpdateLineNumbers();
                UpdateCaretPosition();
                if (_openScriptTabOnLoad)
                {
                    EditorTabControl.SelectedIndex = 1;
                    ScriptTextBox.Focus();
                }
                else
                {
                    EditorTabControl.SelectedIndex = 0;
                    InstructionsTextBox.Focus();
                }

                UpdateScriptTabUiState();
            };
            Closing += ScriptEditorWindow_Closing;
        }

        private void EditorTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateScriptTabUiState();
        }

        private void UpdateScriptTabUiState()
        {
            if (EditorTabControl == null)
            {
                return;
            }

            var isScriptTabSelected = EditorTabControl.SelectedIndex == 1;
            var actionVisibility = _allowExecutionActions && isScriptTabSelected
                ? Visibility.Visible
                : Visibility.Collapsed;
            RunButton.Visibility = actionVisibility;
            ValidateButton.Visibility = actionVisibility;
        }

        private void ScriptEditorWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveScriptEditorLayoutState();
        }

        private void LoadScriptEditorLayoutState()
        {
            try
            {
                if (!File.Exists(ScriptEditorLayoutStatePath))
                {
                    return;
                }

                var json = File.ReadAllText(ScriptEditorLayoutStatePath);
                var state = JsonSerializer.Deserialize<ScriptEditorLayoutState>(json);
                if (state == null)
                {
                    return;
                }

                ApplyPixelLength(ScriptEditorTopRow, state.TopRowHeight, min: 220);
                ApplyPixelLength(ScriptEditorLogRow, state.LogRowHeight, min: 120);
            }
            catch
            {
                // Ignore bad persisted layout and use defaults.
            }
        }

        private void SaveScriptEditorLayoutState()
        {
            try
            {
                var state = new ScriptEditorLayoutState
                {
                    TopRowHeight = ScriptEditorTopRow.ActualHeight,
                    LogRowHeight = ScriptEditorLogRow.ActualHeight
                };

                var directory = Path.GetDirectoryName(ScriptEditorLayoutStatePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(ScriptEditorLayoutStatePath, json);
            }
            catch
            {
                // Ignore save failures.
            }
        }

        private static void ApplyPixelLength(RowDefinition definition, double value, double min)
        {
            if (definition == null || double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                return;
            }

            definition.Height = new GridLength(Math.Max(value, min), GridUnitType.Pixel);
        }

        private void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_allowExecutionActions)
            {
                return;
            }

            OutputExpander.IsExpanded = true;
            var result = ScriptEngine.Validate(ScriptLanguage, ScriptText);
            if (result.Success)
            {
                SetValidateButtonState(true);
                AppendLog("VALIDATE: success");
                return;
            }

            SetValidateButtonState(false);
            AppendLog("VALIDATE: failed");
            if (result.Diagnostics.Count > 0)
            {
                foreach (var diagnostic in result.Diagnostics)
                {
                    var location = diagnostic.Line.HasValue
                        ? $"line {diagnostic.Line}, col {diagnostic.Column ?? 1}"
                        : "unknown location";
                    AppendLog($"{diagnostic.Severity} {diagnostic.Code} ({location}): {diagnostic.Message}");
                }
            }
            else
            {
                AppendLog(result.Error);
            }

            HighlightLine(result.Line, result.Column);
        }

        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_allowExecutionActions)
            {
                return;
            }

            OutputExpander.IsExpanded = true;
            AppendLog($"RUN: started (language={ScriptLanguage})");

            var context = new Models.ExecutionContext();
            var outcome = await ScriptEngine.ExecuteAsync(
                ScriptLanguage,
                ScriptText,
                context,
                actual: string.Empty,
                trace: message => AppendLog($"script: {message}"));

            if (outcome.Success)
            {
                var resultText = outcome.Result?.ToString() ?? "<null>";
                AppendLog($"RUN: success, result={resultText}");
                HighlightLastLine();
                return;
            }

            AppendLog("RUN: failed");
            if (outcome.Diagnostics.Count > 0)
            {
                foreach (var diagnostic in outcome.Diagnostics)
                {
                    var location = diagnostic.Line.HasValue
                        ? $"line {diagnostic.Line}, col {diagnostic.Column ?? 1}"
                        : "unknown location";
                    AppendLog($"{diagnostic.Severity} {diagnostic.Code} ({location}): {diagnostic.Message}");
                }
            }
            else
            {
                AppendLog(outcome.Error);
            }

            HighlightLine(outcome.Line, outcome.Column);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ScriptTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SetValidateButtonState(null);
            UpdateLineNumbers();
            UpdateCaretPosition();
        }

        private void LanguageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SetValidateButtonState(null);
        }

        private void ScriptTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateCaretPosition();
        }

        private void ScriptTextBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            LineNumbersScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
            ColumnRulerScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            OutputLogTextBox.Clear();
        }

        private void UpdateLineNumbers()
        {
            var lineCount = Math.Max(ScriptTextBox.LineCount, 1);
            var builder = new StringBuilder();
            for (var i = 1; i <= lineCount; i++)
            {
                builder.AppendLine(i.ToString());
            }

            LineNumbersTextBlock.Text = builder.ToString();
        }

        private void UpdateColumnRuler()
        {
            const int maxColumn = 240;
            var tensBuilder = new StringBuilder("      ");
            for (var col = 10; col <= maxColumn; col += 10)
            {
                tensBuilder.Append(col.ToString().PadLeft(10));
            }

            var onesBuilder = new StringBuilder();
            for (var col = 1; col <= maxColumn; col++)
            {
                onesBuilder.Append(col % 10);
            }

            ColumnRulerTextBlock.Text = string.Join(Environment.NewLine, new[]
            {
                tensBuilder.ToString(),
                onesBuilder.ToString()
            });
        }

        private void UpdateCaretPosition()
        {
            var caretIndex = ScriptTextBox.CaretIndex;
            var line = ScriptTextBox.GetLineIndexFromCharacterIndex(caretIndex);
            var lineStart = ScriptTextBox.GetCharacterIndexFromLineIndex(Math.Max(line, 0));
            var column = Math.Max(caretIndex - lineStart, 0);
            CaretPositionTextBlock.Text = $"Ln {line + 1}, Col {column + 1}";
        }

        private void HighlightLine(int? line, int? column)
        {
            if (!line.HasValue || line.Value < 1)
            {
                return;
            }

            var lineIndex = Math.Max(line.Value - 1, 0);
            if (lineIndex >= ScriptTextBox.LineCount)
            {
                lineIndex = Math.Max(ScriptTextBox.LineCount - 1, 0);
            }

            var start = ScriptTextBox.GetCharacterIndexFromLineIndex(lineIndex);
            var end = lineIndex + 1 < ScriptTextBox.LineCount
                ? ScriptTextBox.GetCharacterIndexFromLineIndex(lineIndex + 1)
                : ScriptTextBox.Text.Length;

            var desiredColumn = Math.Max((column ?? 1) - 1, 0);
            var caret = Math.Min(start + desiredColumn, Math.Max(start, end));
            ScriptTextBox.Focus();
            ScriptTextBox.CaretIndex = caret;
            ScriptTextBox.Select(start, Math.Max(end - start, 0));
            ScriptTextBox.ScrollToLine(lineIndex);
        }

        private void HighlightLastLine()
        {
            var lines = (ScriptTextBox.Text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            var lastNonEmpty = lines
                .Select((value, index) => new { value, index })
                .Where(item => !string.IsNullOrWhiteSpace(item.value))
                .Select(item => item.index + 1)
                .DefaultIfEmpty(1)
                .Last();

            HighlightLine(lastNonEmpty, 1);
        }

        private void AppendLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => AppendLog(message));
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputLogTextBox.Text))
            {
                OutputLogTextBox.Text = line;
            }
            else
            {
                OutputLogTextBox.AppendText(Environment.NewLine + line);
            }

            OutputLogTextBox.ScrollToEnd();
        }

        private void SetValidateButtonState(bool? isSuccess)
        {
            if (ValidateButton == null)
            {
                return;
            }

            if (!isSuccess.HasValue)
            {
                ValidateButton.ClearValue(Control.BackgroundProperty);
                ValidateButton.ClearValue(Control.ForegroundProperty);
                ValidateButton.ClearValue(Control.BorderBrushProperty);
                return;
            }

            if (isSuccess.Value)
            {
                ValidateButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                ValidateButton.Foreground = Brushes.White;
                ValidateButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B5E20"));
                return;
            }

            ValidateButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828"));
            ValidateButton.Foreground = Brushes.White;
            ValidateButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E0000"));
        }

        private static string BuildInstructions()
        {
            var sb = new StringBuilder();
            sb.AppendLine("This editor supports C# scripting (Roslyn).");
            sb.AppendLine();
            sb.AppendLine("Available helpers inside script:");
            sb.AppendLine("- Vars: read-only dictionary of runtime variables");
            sb.AppendLine("- Var(\"name\"): read one variable value");
            sb.AppendLine("- VarText(\"name\"): read variable as string");
            sb.AppendLine("- SetVar(\"name\", value): create/update variable");
            sb.AppendLine("- log(\"message\"): write a line into the editor output panel");
            sb.AppendLine("- actual / Actual: current assertion actual value (for script assertions)");
            sb.AppendLine("- actualNumber / ActualNumber: parsed numeric actual when possible");
            sb.AppendLine();
            sb.AppendLine("Script Component examples:");
            sb.AppendLine("- Read variable:");
            sb.AppendLine("  VarText(\"token\")");
            sb.AppendLine();
            sb.AppendLine("- Set variable and return value:");
            sb.AppendLine("  var runId = Guid.NewGuid().ToString();");
            sb.AppendLine("  SetVar(\"runId\", runId);");
            sb.AppendLine("  runId");
            sb.AppendLine();
            sb.AppendLine("Assertion Script examples (must return bool):");
            sb.AppendLine("- actual == expected variable:");
            sb.AppendLine("  actual == VarText(\"expectedStatus\")");
            sb.AppendLine();
            sb.AppendLine("- numeric comparison:");
            sb.AppendLine("  actualNumber.HasValue && actualNumber.Value >= 200 && actualNumber.Value < 300");
            sb.AppendLine();
            sb.AppendLine("Tips:");
            sb.AppendLine("- Keep assertion scripts returning true/false.");
            sb.AppendLine("- Use Validate before Save to catch syntax errors.");
            sb.AppendLine("- Use Run to test and debug the script with log output.");
            sb.AppendLine("- For non-CSharp assertion scripts, add marker at top: //lang:YourLanguage");
            return sb.ToString();
        }
    }
}
