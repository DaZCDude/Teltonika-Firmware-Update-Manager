using Avalonia.Controls;
using Avalonia.Media;
using Renci.SshNet;
using System.Threading.Tasks;
using TeltonikaFirmwareUpdateManager.Views;

namespace TeltonikaFirmwareUpdateManager;

public partial class PasswordWindow : Window
{
    public MainWindow MainWindowRef;

    public PasswordWindow()
    {
        InitializeComponent();

        //Auto focus on text input when opening the window.
        PasswordTextBox.AttachedToVisualTree += (sender, e) =>
        {
            PasswordTextBox.Focus();
        };
    }

    private void PasswordTextBoxInput(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        if (PasswordTextBox.Text != "")
        {
            AcceptBtn.IsEnabled = true;
        }
        else
        {
            AcceptBtn.IsEnabled = false;
        }
    }

    private void CancelBtnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }

    private async void AcceptBtnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        using (var client = new SshClient(MainWindowRef.IPListBox.Items[0].ToString(), "root", PasswordTextBox.Text))
        {
            try
            {
                ErrorLabel.Content = "Tjekker om adgangskode er korrekt...";
                ErrorLabel.Foreground = Brushes.White;
                await Task.Delay(500);

                client.Connect();
            }
            catch
            {
                ErrorLabel.Content = "Forkert Adgangskode!";
                ErrorLabel.Foreground = Brushes.Red;
                PasswordTextBox.Focus();
                return;
            }
        }
        MainWindowRef.GlobalPassword = PasswordTextBox.Text;
        Close(true);
    }
}