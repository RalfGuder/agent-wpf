using System.Windows;

namespace AgentWpf.TestApp;

/// <summary>A modal confirmation dialog.</summary>
public partial class ConfirmDialog : Window
{
    /// <summary>Initializes a new instance of the <see cref="ConfirmDialog"/> class.</summary>
    public ConfirmDialog() => this.InitializeComponent();

    private void OnOk(object sender, RoutedEventArgs e) => this.DialogResult = true;
}
