using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using StemSplitter.Models;
using StemSplitter.Services;

namespace StemSplitter.GUI;

public partial class MainWindow : Window
{
    private readonly AudioProcessor _audioProcessor;
    private readonly StemSeparator _stemSeparator;
    private string? _inputFilePath;
    private string? _outputDirectory;
    private string? _lastOutputDirectory;
    private bool _isProcessing;

    private static readonly string[] SupportedExtensions = { ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac" };

    public MainWindow()
    {
        InitializeComponent();
        _audioProcessor = new AudioProcessor();
        _stemSeparator = new StemSeparator();

        CheckDemucsInstallation();
    }

    private async void CheckDemucsInstallation()
    {
        var (isInstalled, version) = await _stemSeparator.CheckDemucsInstallationAsync();

        if (isInstalled)
        {
            AppendLog($"✓ Demucs {version ?? "unknown"} detected");
        }
        else
        {
            AppendLog("⚠ Demucs not found. Please install it:");
            AppendLog("  pip install demucs");
            StatusText.Text = "Demucs not installed";
            StatusText.Foreground = (SolidColorBrush)FindResource("ErrorBrush");
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Audio File",
            Filter = "Audio Files|*.mp3;*.wav;*.flac;*.ogg;*.m4a;*.aac|MP3 Files|*.mp3|WAV Files|*.wav|All Files|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() == true)
        {
            LoadAudioFile(dialog.FileName);
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearSelection();
    }

    private void ClearSelection()
    {
        _inputFilePath = null;
        InputFileText.Text = "Drag and drop audio file here or click Browse...";
        InputFileText.Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush");
        AudioInfoPanel.Visibility = Visibility.Collapsed;
        SeparateButton.IsEnabled = false;
        StatusText.Text = "Ready";
        StatusText.Foreground = (SolidColorBrush)FindResource("TextSecondaryBrush");
    }

    private void LoadAudioFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (!SupportedExtensions.Contains(extension))
        {
            MessageBox.Show(
                $"Unsupported file format: {extension}\n\nSupported formats: MP3, WAV, FLAC, OGG, M4A, AAC",
                "Unsupported Format",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _inputFilePath = filePath;
        InputFileText.Text = filePath;
        InputFileText.Foreground = (SolidColorBrush)FindResource("TextBrush");

        // Load audio info
        var info = _audioProcessor.GetAudioInfo(filePath);

        if (string.IsNullOrEmpty(info.Error))
        {
            AudioInfoPanel.Visibility = Visibility.Visible;
            DurationText.Text = info.Duration.ToString(@"mm\:ss");
            SampleRateText.Text = $"{info.SampleRate} Hz";
            ChannelsText.Text = info.Channels == 2 ? "Stereo" : info.Channels == 1 ? "Mono" : $"{info.Channels}ch";
            BitDepthText.Text = $"{info.BitsPerSample} bit";

            AppendLog($"Loaded: {Path.GetFileName(filePath)}");
            AppendLog($"  Duration: {info.Duration:mm\\:ss}, {info.SampleRate}Hz, {info.Channels}ch");
        }
        else
        {
            AudioInfoPanel.Visibility = Visibility.Collapsed;
            AppendLog($"Warning: Could not read audio info - {info.Error}");
        }

        SeparateButton.IsEnabled = true;
        StatusText.Text = "Ready to separate";
        StatusText.Foreground = (SolidColorBrush)FindResource("TextBrush");
    }

    private void OutputFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Output Folder",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _outputDirectory = dialog.SelectedPath;
            OutputFolderText.Text = dialog.SelectedPath;
        }
    }

    private async void SeparateButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_inputFilePath) || _isProcessing)
            return;

        _isProcessing = true;
        SetUIEnabled(false);

        var model = ((System.Windows.Controls.ComboBoxItem)ModelComboBox.SelectedItem).Tag?.ToString() ?? "htdemucs_6s";
        var format = ((System.Windows.Controls.ComboBoxItem)FormatComboBox.SelectedItem).Tag?.ToString() ?? "wav";
        var shifts = int.Parse(((System.Windows.Controls.ComboBoxItem)ShiftsComboBox.SelectedItem).Tag?.ToString() ?? "1");
        var cpuOnly = CpuOnlyCheckBox.IsChecked == true;

        var options = new SeparationOptions
        {
            InputFile = _inputFilePath,
            OutputDirectory = _outputDirectory,
            Model = model,
            OutputFormat = format,
            Shifts = shifts,
            CpuOnly = cpuOnly
        };

        LogText.Text = string.Empty;
        AppendLog($"Starting separation with model: {model}");
        AppendLog($"Output format: {format.ToUpper()}, Shifts: {shifts}, CPU Only: {cpuOnly}");
        AppendLog("This may take several minutes...");
        AppendLog(new string('─', 50));

        ProgressBar.IsIndeterminate = true;
        StatusText.Text = "Separating...";
        StatusText.Foreground = (SolidColorBrush)FindResource("PrimaryBrush");

        var progress = new Progress<string>(msg =>
        {
            AppendLog(msg);

            // Try to parse progress percentage from Demucs output
            if (msg.Contains("%"))
            {
                var percentMatch = System.Text.RegularExpressions.Regex.Match(msg, @"(\d+)%");
                if (percentMatch.Success && int.TryParse(percentMatch.Groups[1].Value, out var percent))
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = percent;
                }
            }
        });

        try
        {
            var result = await Task.Run(() => _stemSeparator.SeparateAsync(options, progress));

            ProgressBar.IsIndeterminate = false;

            if (result.Success)
            {
                ProgressBar.Value = 100;
                _lastOutputDirectory = result.OutputDirectory;

                AppendLog(new string('─', 50));
                AppendLog("✓ Separation completed successfully!");
                AppendLog($"Output directory: {result.OutputDirectory}");
                AppendLog($"Processing time: {result.ProcessingTime:mm\\:ss}");
                AppendLog("");
                AppendLog("Extracted stems:");

                foreach (var (stemType, filePath) in result.StemFiles.OrderBy(x => x.Key.ToString()))
                {
                    AppendLog($"  • {stemType}: {Path.GetFileName(filePath)}");
                }

                StatusText.Text = $"Complete! {result.StemFiles.Count} stems extracted";
                StatusText.Foreground = (SolidColorBrush)FindResource("SuccessBrush");

                OpenOutputButton.Visibility = Visibility.Visible;

                MessageBox.Show(
                    $"Successfully extracted {result.StemFiles.Count} stems!\n\nOutput: {result.OutputDirectory}",
                    "Separation Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                ProgressBar.Value = 0;
                AppendLog(new string('─', 50));
                AppendLog($"✗ Error: {result.ErrorMessage}");

                StatusText.Text = "Failed";
                StatusText.Foreground = (SolidColorBrush)FindResource("ErrorBrush");

                MessageBox.Show(
                    $"Separation failed:\n\n{result.ErrorMessage}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = 0;
            AppendLog($"✗ Exception: {ex.Message}");

            StatusText.Text = "Error occurred";
            StatusText.Foreground = (SolidColorBrush)FindResource("ErrorBrush");

            MessageBox.Show(
                $"An error occurred:\n\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isProcessing = false;
            SetUIEnabled(true);
        }
    }

    private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastOutputDirectory) && Directory.Exists(_lastOutputDirectory))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _lastOutputDirectory,
                UseShellExecute = true
            });
        }
    }

    private void SetUIEnabled(bool enabled)
    {
        SeparateButton.IsEnabled = enabled && !string.IsNullOrEmpty(_inputFilePath);
        SeparateButton.Content = enabled ? "🎛️ Separate Stems" : "⏳ Processing...";
        ModelComboBox.IsEnabled = enabled;
        FormatComboBox.IsEnabled = enabled;
        ShiftsComboBox.IsEnabled = enabled;
        CpuOnlyCheckBox.IsEnabled = enabled;
    }

    private void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            if (!string.IsNullOrEmpty(LogText.Text))
                LogText.Text += "\n";

            LogText.Text += message;
            LogScrollViewer.ScrollToEnd();
        });
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0)
            {
                var ext = Path.GetExtension(files[0]).ToLowerInvariant();
                if (SupportedExtensions.Contains(ext))
                {
                    e.Effects = DragDropEffects.Copy;
                    DropZone.BorderBrush = (SolidColorBrush)FindResource("PrimaryBrush");
                    return;
                }
            }
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40));

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0)
            {
                LoadAudioFile(files[0]);
            }
        }
    }
}
