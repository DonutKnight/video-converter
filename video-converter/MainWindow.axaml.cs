using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace video_converter
{
    public partial class MainWindow : Window
    {
        private ComboBox? fileComboBox;
        private ComboBox? formatComboBox;
        private Button? convertButton;
        private Button? selectFolderButton;
        private TextBlock? statusText;
        private TextBox? outputNameTextBox;
        private string selectedFolder = "";

        public MainWindow()
        {
            InitializeComponent();
            fileComboBox = this.FindControl<ComboBox>("FileComboBox");
            formatComboBox = this.FindControl<ComboBox>("FormatComboBox");
            convertButton = this.FindControl<Button>("ConvertButton");
            selectFolderButton = this.FindControl<Button>("SelectFolderButton");
            statusText = this.FindControl<TextBlock>("StatusText");
            outputNameTextBox = this.FindControl<TextBox>("OutputNameTextBox");

            var uri = new Uri("avares://video-converter/logo/icon.png");
            this.Icon = new WindowIcon(AssetLoader.Open(uri));

            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            selectFolderButton.Click += async (sender, e) =>
            {
                var dialog = new OpenFolderDialog();
#pragma warning disable CS0618
                var folder = await dialog.ShowAsync(this);
#pragma warning restore CS0618
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    selectedFolder = folder;
                    LoadVideoFiles(folder);
                }
            };

            convertButton.Click += async (sender, e) =>
            {
                if (fileComboBox?.SelectedItem == null)
                {
                    statusText?.SetValue(TextBlock.TextProperty, "Wybierz plik wideo!");
                    return;
                }

                string selectedFile = fileComboBox.SelectedItem as string;
                string targetFormat = (formatComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
                                      ?? formatComboBox.SelectedItem as string;

                if (string.IsNullOrWhiteSpace(selectedFile) || string.IsNullOrWhiteSpace(targetFormat))
                {
                    statusText?.SetValue(TextBlock.TextProperty, "Wybierz plik i format!");
                    return;
                }

                string? customName = outputNameTextBox?.Text;
                string outputFileName;
                if (!string.IsNullOrWhiteSpace(customName))
                {
                    foreach (var c in Path.GetInvalidFileNameChars())
                        customName = customName.Replace(c, '_');
                    outputFileName = customName + "." + targetFormat;
                }
                else
                {
                    outputFileName = Path.GetFileNameWithoutExtension(selectedFile) + "." + targetFormat;
                }

                string outputDir = Path.GetDirectoryName(selectedFile) ?? "";
                string outputPath = Path.Combine(outputDir, outputFileName);

                statusText?.SetValue(TextBlock.TextProperty, $"Konwertowanie do {targetFormat}...");

                try
                {
                    var converter = new VideoConverter();
                    converter.OnConversionCompleted += (outputFile) =>
                    {
                        statusText?.SetValue(TextBlock.TextProperty, $"Konwersja zakończona! Plik wynikowy: {outputFile}");
                    };

                    await converter.ConvertAsync(selectedFile, targetFormat, outputPath);

                    new LogManager().LogConversion(selectedFile, outputPath, targetFormat);
                }
                catch (Exception ex)
                {
                    statusText?.SetValue(TextBlock.TextProperty, $"Błąd podczas konwersji: {ex.Message}");
                }
            };
        }

        private void LoadVideoFiles(string folder)
        {
            var extensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".webm" };
            var files = Directory.GetFiles(folder)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            if (fileComboBox != null)
            {
                fileComboBox.ItemsSource = files;
                fileComboBox.SelectedIndex = files.Count > 0 ? 0 : -1;
            }
        }
    }

    public delegate void ConversionCompletedHandler(string outputFile);

    public interface IConverter
    {
        event ConversionCompletedHandler? OnConversionCompleted;
    }

    public abstract class ConverterBase : IConverter
    {
        public event ConversionCompletedHandler? OnConversionCompleted;
        public string? InputFile { get; set; }
        public string? OutputFile { get; set; }

        public abstract Task ConvertAsync(string inputFile, string targetFormat, string outputPath);

        protected void RaiseConversionCompleted(string outputFile)
        {
            OnConversionCompleted?.Invoke(outputFile);
        }
    }

    public class VideoConverter : ConverterBase
    {
        public override async Task ConvertAsync(string inputFile, string targetFormat, string outputPath)
        {
            InputFile = inputFile;
            OutputFile = outputPath;

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{inputFile}\" \"{outputPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();

                    string ffmpegOutput = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        RaiseConversionCompleted(outputPath);
                    }
                    else
                    {
                        throw new Exception($"ffmpeg error: {ffmpegOutput}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Conversion failed: {ex.Message}");
            }
        }
    }

    public class LogManager
    {
        private string logFilePath = "conversion_log.txt";

        public void LogConversion(string inputFile, string outputFile, string targetFormat)
        {
            try
            {
                string logEntry = $"{DateTime.Now}: Converted '{Path.GetFileName(inputFile)}' to '{Path.GetFileName(outputFile)}' (format: {targetFormat})";
                File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
            }
            catch { }
        }
    }
}