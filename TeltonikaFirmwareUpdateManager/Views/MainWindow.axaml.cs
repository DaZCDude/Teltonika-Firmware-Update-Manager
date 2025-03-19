using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Renci.SshNet;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TeltonikaFirmwareUpdateManager.Views;

public partial class MainWindow : Window
{
    public string GlobalPassword = "";
    string FirmwarePath = "";

    public MainWindow()
    {
        InitializeComponent();

        LoadIPsFromFile();
        CheckIfIPIsPresentInList();
    }

    public void SaveIPsToFile()
    {
        // Define the file path relative to the .exe file
        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ip_list.txt");

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            foreach (string ip in IPListBox.Items)
            {
                writer.WriteLine(ip);
            }
        }
    }

    private void LoadIPsFromFile()
    {
        // Define the file path relative to the .exe file
        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ip_list.txt");

        if (File.Exists(filePath))
        {
            IPListBox.Items.Clear();
            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    IPListBox.Items.Add(line);
                }
            }
        }
    }

    private void EnableAllButtons(bool enable)
    {
        IPListBox.IsEnabled = enable;
        AddIPBtn.IsEnabled = enable;
        RemoveIPBtn.IsEnabled = enable;
        SearchModemBtn.IsEnabled = enable;
        SelectFirmwareBtn.IsEnabled = enable;
        UpdateSelectedBtn.IsEnabled = enable;
        UpdateAllBtn.IsEnabled = enable;

        if (IPListBox.Items.Count < 1)
        {
            //Make sure the Remove IP button is disabled if you remove an ip and then press update firmware.
            RemoveIPBtn.IsEnabled = false;
        }

        if (FirmwarePath == "")
        {
            //Since the EnableAllButtons function enables all buttons, i have to re-disable these 2 update buttons if theres no firmware path set yet.

            UpdateSelectedBtn.IsEnabled = false;
            UpdateAllBtn.IsEnabled = false;
        }
        /*
        else if (FoundModemListBox.SelectedItems.Count < 1)
        {
            UpdateSelectedBtn.IsEnabled = false;
        }
        */
    }

    private void AddIPBtnClick(object sender, RoutedEventArgs e)
    {
        var ownerWindow = this;
        AddIPWindow window = new AddIPWindow();
        window.MainWindowRef = ownerWindow;
        window.ShowDialog(ownerWindow);
    }

    private void RemoveIPBtnClick(object sender, RoutedEventArgs e)
    {
        IPListBox.Items.Remove(IPListBox.SelectedItem);

        SaveIPsToFile();
        CheckIfIPIsPresentInList();
    }

    //Check if to enable remove ip and search for modem button
    public void CheckIfIPIsPresentInList()
    {
        if (IPListBox.Items.Count > 0)
        {
            RemoveIPBtn.IsEnabled = true;
            SearchModemBtn.IsEnabled = true;
        }
        else
        {
            RemoveIPBtn.IsEnabled = false;
            SearchModemBtn.IsEnabled = false;
        }
    }

    private async void SearchModemBtnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(GlobalPassword))
        {
            var ownerWindow = this;
            var window = new PasswordWindow();
            window.MainWindowRef = ownerWindow;

            var result = await window.ShowDialog<bool?>(ownerWindow);
            if (result == false)
            {
                return;
            }
        }

        //Clear the search modem list before adding again
        FoundModemListBox.Items.Clear();

        SearchModemProgressBar.Value = 0;
        SearchModemProgressBar.Maximum = IPListBox.Items.Count;
        SearchModemProgressBar.IsVisible = true;

        EnableAllButtons(false);

        foreach (string ip in IPListBox.Items)
        {
            try
            {
                using (var client = new SshClient(ip, "root", GlobalPassword))
                {
                    await client.ConnectAsync(default); // Connect asynchronously

                    string hostname = client.RunCommand("ubus call system board | jsonfilter -e '@.hostname'").Result.Trim();

                    string version = client.RunCommand("cat /etc/version").Result.Trim();

                    client.Disconnect();

                    string combinedVersion = ip + " | Model: " + hostname + " | Version: " + version;

                    FoundModemListBox.Items.Add(combinedVersion);
                    SearchModemProgressBar.Value++;
                }
            }
            catch (Exception ex)
            {
                await MessageBoxManager.GetMessageBoxStandard("Fejl", "Fejl med IP " + ip + ": " + ex.Message, MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowWindowDialogAsync(this);
            }
        }

        SearchModemProgressBar.IsVisible = false;

        EnableAllButtons(true);
    }

    private async void SelectFirmwareBtnClick(object sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Vælg Firmware Fil",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Firmware Files (.bin)")
                {
                    Patterns = new[] { "*.bin" } // Filter for .bin files
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*.*" } // Allow all file types
                }
            }
        });

        if (files.Count >= 1)
        {
            // Get the file path
            var filePath = files[0].Path.LocalPath;

            // Set firmwarepath string to the selected filepath
            FirmwarePath = filePath;

            // Update the label with the file path
            FirmwarePathLabel.Text = filePath;

            EnableAllButtons(true);
        }
    }
    
    private void UpdateSelectedBtnClick(object sender, RoutedEventArgs e)
    {
        UpdateFirmware(false);
    }

    private void UpdateAllBtnClick(object sender, RoutedEventArgs e)
    {
        UpdateFirmware(true);
    }

    private async void UpdateFirmware(bool AllItems)
    {
        //If you dont have selected any item, dont update
        if (!AllItems)
        {
            if (FoundModemListBox.SelectedItems.Count < 1)
            {
                await MessageBoxManager.GetMessageBoxStandard("Fejl", "Vælg mindst 1 enhed først.").ShowWindowDialogAsync(this);
                return;
            }
        }

        var itemsToIterate = (AllItems ? FoundModemListBox.Items : FoundModemListBox.SelectedItems).Cast<string>().ToList();

        var box = MessageBoxManager.GetMessageBoxStandard("Advarsel", "Er du sikker på at du vil opdatere " + itemsToIterate.Count + " modem? Dette kan godt tage et stykke tid.", ButtonEnum.YesNo);

        var result = await box.ShowWindowDialogAsync(this);

        if (result == ButtonResult.No)
        {
            return;
        }

        FirmwareUpdateProgressBar.Value = 0;
        FirmwareUpdateProgressBar.Maximum = itemsToIterate.Count;
        FirmwareUpdateProgressBar.IsVisible = true;

        EnableAllButtons(false);

        foreach (string itemname in itemsToIterate)
        {
            // Get IP name which is before the | part in the item name
            string ItemIP = itemname.Substring(0, itemname.IndexOf(" |"));

            using (var scpClient = new ScpClient(ItemIP, "root", GlobalPassword))
            {
                await scpClient.ConnectAsync(default); // Connect asynchronously
                FirmwareUpdateStatusLabel.Text = "Uploader firmware gennem SCP til " + ItemIP;

                using (var fs = new FileStream(FirmwarePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous))
                {
                    await Task.Run(() => scpClient.Upload(fs, "/tmp/update.bin")); //Run in background thread
                    FirmwareUpdateStatusLabel.Text = "Firmware upload til " + ItemIP + " færdig!";
                }

                scpClient.Disconnect();
            }

            using (var sshClient = new SshClient(ItemIP, "root", GlobalPassword))
            {
                try
                {
                    FirmwareUpdateStatusLabel.Text = "Forbinder med SSH til " + ItemIP;
                    await sshClient.ConnectAsync(default);

                    FirmwareUpdateStatusLabel.Text = "Kører opdatering af firmware på " + ItemIP;
                    sshClient.RunCommand("sysupgrade /tmp/update.bin");
                }
                catch (Exception ex)
                {
                    FirmwareUpdateStatusLabel.Text = "Opdatering fuldført! Genstarter modem...";
                }
                finally
                {
                    if (sshClient.IsConnected)
                    {
                        sshClient.Disconnect();
                    }
                }
            }

            await Task.Delay(3000); // Wait 3 seconds between devices

            Dispatcher.UIThread.Post(() => FirmwareUpdateProgressBar.Value++);
        }

        FirmwareUpdateProgressBar.IsVisible = false;
        FirmwareUpdateStatusLabel.Text = "Fuldført!";

        EnableAllButtons(true);
    }
}