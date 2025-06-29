using Avalonia.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace video_converter
{
    public partial class MainWindow : Window
    {
        private ComboBox? fileComboBox;
        private ComboBox? formatComboBox;
        private Button? convertButton;
        private Button? selectFolderButton;
        private TextBlock? statusText;
        private string selectedFolder = "";

        public MainWindow()
        {
            InitializeComponent();
            fileComboBox = this.FindControl<ComboBox>("FileComboBox");
            formatComboBox = this.FindControl<ComboBox>("FormatComboBox");
            convertButton = this.FindControl<Button>("ConvertButton");
            selectFolderButton = this.FindControl<Button>("SelectFolderButton");
            statusText = this.FindControl<TextBlock>("StatusText");
            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            selectFolderButton.Click += async (sender, e) =>
            {
                var dialog = new OpenFolderDialog();
                var folder = await dialog.ShowAsync(this);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    selectedFolder = folder;
                    LoadVideoFiles(folder);
                }
            };

            convertButton.Click += async (sender, e) =>
            {
                if (fileComboBox.SelectedItem == null)
                {
                    statusText.Text = "Wybierz plik wideo!";
                    return;
                }

                string selectedFile = fileComboBox.SelectedItem as string;
                string targetFormat = (formatComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() 
                                      ?? formatComboBox.SelectedItem as string;

                if (string.IsNullOrWhiteSpace(selectedFile) || string.IsNullOrWhiteSpace(targetFormat))
                {
                    statusText.Text = "Wybierz plik i format!";
                    return;
                }

                statusText.Text = $"Konwertowanie do {targetFormat}...";

                try
                {
                    var converter = new VideoConverter();
                    converter.OnConversionCompleted += (outputFile) =>
                    {
                        statusText.Text = $"Konwersja zakończona! Plik wynikowy: {outputFile}";
                    };

                    await converter.ConvertAsync(selectedFile, targetFormat);

                    var dir = Path.GetDirectoryName(selectedFile) ?? "";
                    var outFile = Path.Combine(dir, Path.GetFileNameWithoutExtension(selectedFile) + "." + targetFormat);
                    new LogManager().LogConversion(selectedFile, outFile, targetFormat);
                }
                catch (Exception ex)
                {
                    statusText.Text = $"Błąd podczas konwersji: {ex.Message}";
                }
            };
        }

        private void LoadVideoFiles(string folder)
        {
            var extensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".webm" };
            var files = Directory.GetFiles(folder)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            fileComboBox.ItemsSource = files;
            fileComboBox.SelectedIndex = files.Count > 0 ? 0 : -1;
        }
    }

    public delegate void ConversionCompletedHandler(string outputFile);

    public interface IConverter
    {
        event ConversionCompletedHandler OnConversionCompleted;
        Task ConvertAsync(string inputFile, string targetFormat);
    }

    public abstract class ConverterBase : IConverter
    {
        public event ConversionCompletedHandler OnConversionCompleted;
        public string InputFile { get; set; }
        public string OutputFile { get; set; }

        public abstract Task ConvertAsync(string inputFile, string targetFormat);

        protected void RaiseConversionCompleted(string outputFile)
        {
            OnConversionCompleted?.Invoke(outputFile);
        }
    }

    public class VideoConverter : ConverterBase
    {
        public override async Task ConvertAsync(string inputFile, string targetFormat)
        {
            InputFile = inputFile;
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(inputFile);
            string directory = Path.GetDirectoryName(inputFile);
            OutputFile = Path.Combine(directory, fileNameWithoutExt + "." + targetFormat);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{inputFile}\" \"{OutputFile}\"",
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
                        RaiseConversionCompleted(OutputFile);
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

    public static class ConverterFactory
    {
        public static IConverter GetConverter(string inputFile)
        {
            return new VideoConverter();
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