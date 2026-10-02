using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AgentWpf.TestApp;

/// <summary>The main window with one control of every kind the end-to-end tests need.</summary>
public partial class MainWindow : Window
{
    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        this.InitializeComponent();
        this.gridOrders.ItemsSource = Enumerable.Range(1, 1000)
            .Select(i => new Order(i, "Kunde " + i.ToString(CultureInfo.InvariantCulture)))
            .ToList();
    }

    private void OnGreet(object sender, RoutedEventArgs e) => this.lblResult.Text = "Hallo, " + this.txtName.Text;

    private void OnColorChanged(object sender, SelectionChangedEventArgs e)
        => this.lblColor.Text = "Farbe: " + ((this.cmbColor.SelectedItem as ComboBoxItem)?.Content ?? "-");

    private void OnDialog(object sender, RoutedEventArgs e)
    {
        var dialog = new ConfirmDialog { Owner = this };
        this.lblResult.Text = dialog.ShowDialog() == true ? "Bestätigt" : "Abgebrochen";
    }

    private void OnSecond(object sender, RoutedEventArgs e) => new ToolWindow().Show();

    private void OnDelayed(object sender, RoutedEventArgs e)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            this.btnLate.Visibility = Visibility.Visible;
        };
        timer.Start();
    }

    private void OnMenuOpen(object sender, RoutedEventArgs e) => this.lblResult.Text = "Menü Öffnen";

    private void OnMenuExit(object sender, RoutedEventArgs e) => this.Close();
}

/// <summary>A row of the orders grid.</summary>
/// <param name="Number">The order number.</param>
/// <param name="Customer">The customer name.</param>
public sealed record Order(int Number, string Customer);
