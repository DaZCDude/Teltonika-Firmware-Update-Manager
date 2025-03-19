using Avalonia.Controls;
using System.Linq;
using TeltonikaFirmwareUpdateManager.Views;

namespace TeltonikaFirmwareUpdateManager;

public partial class AddIPWindow : Window
{
    public MainWindow MainWindowRef;

    public AddIPWindow()
    {
        InitializeComponent();

        //Auto focus on text input when opening the window.
        IPTextBox.AttachedToVisualTree += (sender, e) =>
        {
            IPTextBox.Focus();
        };
    }

    public bool ListBoxContainsString(ListBox listBox, string searchString)
    {
        // Iterate through the items in the ListBox
        foreach (var item in listBox.Items)
        {
            // Convert the item to a string (if it's not already)
            string itemText = item.ToString();

            // Check if the item matches the search string
            if (itemText == searchString)
            {
                return true; // The string was found
            }
        }

        return false; // The string was not found
    }

    private void CancelBtnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void AddIPBtnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ListBoxContainsString(MainWindowRef.IPListBox, IPTextBox.Text))
        {
            ErrorLabel.Content = "IP er allerede på listen.";
            return;
        }

        MainWindowRef.IPListBox.Items.Add(IPTextBox.Text);

        MainWindowRef.CheckIfIPIsPresentInList();
        MainWindowRef.SaveIPsToFile();
        Close();
    }

    private void IPTextBoxInput(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        // Get the current text from the text box
        string currentText = IPTextBox.Text;

        // Create a new string that only contains digits
        string newText = new string(currentText.Where(c => char.IsDigit(c) || c == '.').ToArray());

        // If the new text is different from the current text, update the text box
        if (newText != currentText)
        {
            IPTextBox.Text = newText;

            // Move the caret to the end of the text
            IPTextBox.CaretIndex = newText.Length;
        }

        if (IPTextBox.Text != "")
        {
            AddIPBtn.IsEnabled = true;
        }
        else
        {
            AddIPBtn.IsEnabled = false;
        }
    }
}