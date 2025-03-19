using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using TeltonikaFirmwareUpdateManager.ViewModels;
using TeltonikaFirmwareUpdateManager.Views;
using System.Globalization;
using Avalonia.Controls;
using System.IO;
using System;

namespace TeltonikaFirmwareUpdateManager;

public partial class App : Application
{
    string language = "en-US";

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Define the file path relative to the .exe file
        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "language.txt");

        //If no language file exists, create one
        if (!File.Exists(filePath))
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("en-US");
            }
        }
        //If it exists, load the language text
        else
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                string line = reader.ReadLine();

                if (!string.IsNullOrEmpty(line))
                {
                    language = line;
                }
            }
        }

        switch (language)
        {
            case "en-US":
                Properties.Resources.Culture = null;
                break;
            case "da-DK":
                Properties.Resources.Culture = new CultureInfo("da-DK");
                break;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}