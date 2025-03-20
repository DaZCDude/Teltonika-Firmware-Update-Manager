using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CsvHelper;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        EnableAllButtons(true);
    }

    public class ModemCSVData
    {
        public string IP { get; set; }
        public string Model { get; set; }

        public string Version { get; set; }

        public string IMEI { get; set; }

        public string Serial { get; set; }

        public string Operator { get; set; }
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
        if (enable)
        {
            IPListBox.IsEnabled = true;
            AddIPBtn.IsEnabled = true;

            if (IPListBox.Items.Count < 1)
            {
                //Make sure the Remove IP button is disabled if you remove an ip and then press update firmware.
                RemoveIPBtn.IsEnabled = false;
                SearchModemBtn.IsEnabled = false;
            }
            else
            {
                RemoveIPBtn.IsEnabled = true;
                SearchModemBtn.IsEnabled = true;
            }

            if (FoundModemListBox.Items.Count < 1)
            {
                SelectFirmwareBtn.IsEnabled = false;
                ExportModemInfoBtn.IsEnabled = false;
            }
            else
            {
                SelectFirmwareBtn.IsEnabled = true;
                ExportModemInfoBtn.IsEnabled = true;
            }

            if (FirmwarePath == "")
            {
                //Since the EnableAllButtons function enables all buttons, i have to re-disable these 2 update buttons if theres no firmware path set yet.

                UpdateSelectedBtn.IsEnabled = false;
                UpdateAllBtn.IsEnabled = false;
            }
            else
            {
                UpdateSelectedBtn.IsEnabled = true;
                UpdateAllBtn.IsEnabled = true;
            }
        }
        else
        {
            IPListBox.IsEnabled = false;
            AddIPBtn.IsEnabled = false;
            RemoveIPBtn.IsEnabled = false;
            SearchModemBtn.IsEnabled = false;
            SelectFirmwareBtn.IsEnabled = false;
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
        //Create the new window as a dialog box
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
            //Create the new window as a dialog box
            var ownerWindow = this;
            var window = new PasswordWindow();
            window.MainWindowRef = ownerWindow;

            //Wait for dialog result
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

        //Disable all buttons
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

                    
                    // Create a new ListBoxItem
                    ListBoxItem item = new ListBoxItem
                    {
                        Content = combinedVersion, // Display text
                        Tag = ip // Store the IP as a tag
                    };
                    
                    FoundModemListBox.Items.Add(item);

                    SearchModemProgressBar.Value++;
                }
            }
            catch (Exception ex)
            {
                await MessageBoxManager.GetMessageBoxStandard(Properties.Resources.Error, Properties.Resources.ErrorWithIP + " " + ip + ": " + ex.Message, MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowWindowDialogAsync(this);
            }
        }

        SearchModemProgressBar.IsVisible = false;

        EnableAllButtons(true);
    }

    private async void ExportModemInfoBtnClick(object sender, RoutedEventArgs e)
    {
        // Get top level from the current control. Alternatively, you can use Window reference instead.
        var topLevel = TopLevel.GetTopLevel(this);

        // Start async operation to open the dialog.
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save CSV File",
            SuggestedFileName = "export.csv",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("CSV File")
                {
                    Patterns = new[] { "*.csv" }
                }
            }
        });
        
        //If file path exists
        if (file is not null)
        {
            //Write to CSV file using external csv library
            await using var stream = await file.OpenWriteAsync();
            using (var writer = new StreamWriter(stream))
            using (var csv = new CsvWriter(writer, CultureInfo.CurrentCulture))
            {
                foreach (var item in FoundModemListBox.Items.Cast<ListBoxItem>())
                {
                    string model;
                    string version;
                    string imei;
                    string serial;
                    string networkoperator;

                    using (var sshClient = new SshClient(item.Tag.ToString(), "root", GlobalPassword))
                    {
                        await sshClient.ConnectAsync(default);

                        model = sshClient.RunCommand("ubus call system board | jsonfilter -e '@.hostname'").Result.Trim();
                        version = sshClient.RunCommand("cat /etc/version").Result.Trim();
                        imei = sshClient.RunCommand("gsmctl -i").Result.Trim();
                        serial = sshClient.RunCommand("gsmctl -a").Result.Trim();
                        networkoperator = sshClient.RunCommand("gsmctl -o").Result.Trim();
                    }

                    var records = new List<ModemCSVData>
                    {
                        new ModemCSVData { IP = item.Tag.ToString(), Model = model, Version = version, IMEI = imei, Serial = serial, Operator = networkoperator }
                    };

                    await csv.WriteRecordsAsync(records);
                }
            }
        }
    }

    private async void SelectFirmwareBtnClick(object sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);

        //File picker for only .bin files and All Files
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Properties.Resources.SelectFirmwareFile,
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

    //This function is running async so the UI doesnt get unresponsive when running. Task is better than void for async functions
    private async Task UpdateFirmware(bool AllItems)
    {
        //If you dont have selected any item, dont update
        if (!AllItems)
        {
            if (FoundModemListBox.SelectedItems.Count < 1)
            {
                await MessageBoxManager.GetMessageBoxStandard(Properties.Resources.Error, Properties.Resources.NoDevicesSelected).ShowWindowDialogAsync(this);
                return;
            }
        }

        var itemsToIterate = (AllItems ? FoundModemListBox.Items : FoundModemListBox.SelectedItems)
            .Cast<ListBoxItem>() // Cast to ListBoxItem, NOT string
            .Select(item => item.Tag?.ToString()) // Get the Tag (IP)
            .Where(ip => !string.IsNullOrEmpty(ip)) // Filter out null values
            .ToList();

        var box = MessageBoxManager.GetMessageBoxStandard(Properties.Resources.Warning, Properties.Resources.UpdateConfirmation1 + " " + itemsToIterate.Count + " " + Properties.Resources.UpdateConfirmation2, ButtonEnum.YesNo);

        var result = await box.ShowWindowDialogAsync(this);

        //If the users says no in the messagebox asking for confirmation, stop the function
        if (result == ButtonResult.No)
        {
            return;
        }

        //Define the progressbar properties
        FirmwareUpdateProgressBar.Value = 0;
        FirmwareUpdateProgressBar.Maximum = itemsToIterate.Count;
        FirmwareUpdateProgressBar.IsVisible = true;

        EnableAllButtons(false);

        foreach (string ItemIP in itemsToIterate)
        {
            using (var scpClient = new ScpClient(ItemIP, "root", GlobalPassword))
            {
                await scpClient.ConnectAsync(default); // Connect asynchronously
                FirmwareUpdateStatusLabel.Text = Properties.Resources.UploadingFirmwareSCP + " " + ItemIP;

                using (var fs = new FileStream(FirmwarePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous))
                {
                    await Task.Run(() => scpClient.Upload(fs, "/tmp/update.bin")); //Run in background thread
                    FirmwareUpdateStatusLabel.Text = Properties.Resources.FirmwareUpload1 + ItemIP + Properties.Resources.FirmwareUpload2;
                }

                scpClient.Disconnect();
            }

            using (var sshClient = new SshClient(ItemIP, "root", GlobalPassword))
            {
                try
                {
                    FirmwareUpdateStatusLabel.Text = Properties.Resources.ConnectingSSH + " " + ItemIP;
                    await sshClient.ConnectAsync(default);

                    //Run the upgrade command on the device
                    FirmwareUpdateStatusLabel.Text = Properties.Resources.FirmwareUpdateStart + ItemIP;
                    
                    //Run the upgrade command async so the program wont crash
                    await Task.Run(() => sshClient.RunCommand("sysupgrade /tmp/update.bin"));
                }
                catch (Exception ex)
                {
                    FirmwareUpdateStatusLabel.Text = Properties.Resources.UpdateFinishedRestarting;
                }
                finally
                {
                    if (sshClient.IsConnected)
                    {
                        sshClient.Disconnect();
                    }
                }
            }

            //Wait 3 seconds between devices
            await Task.Delay(3000);

            Dispatcher.UIThread.Post(() => FirmwareUpdateProgressBar.Value++);
        }

        FirmwareUpdateProgressBar.IsVisible = false;
        FirmwareUpdateStatusLabel.Text = Properties.Resources.Finished;

        EnableAllButtons(true);
    }
}