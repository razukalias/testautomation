using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;

namespace Test_Automation
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // WPF emits frequent transient Binding Error:4 messages for container lifecycle
            // (e.g., TreeViewItem ancestor lookups). They are non-fatal and clutter Output.
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Critical;
            base.OnStartup(e);
        }
    }

}
