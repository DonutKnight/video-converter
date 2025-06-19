using Avalonia.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;

namespace video_converter
{
    public partial class MainWindow : Window
    {
        private TextBox inputFileTextBox;
        private ComboBox formatComboBox;
        private Button convertButton;
        private TextBlock statusText;

        public MainWindow()
        {
            InitializeComponent();
            InitializeUIComponents();
            SetupEventHandlers();
        }

        private void InitializeUIComponents()
        {
            inputFileTextBox = new TextBox { Watermark = "Select a video file..." };
            formatComboBox = new ComboBox
            {
                Items = new[] { "mp4", "mov", "avi", "mkv", "webm" },
                SelectedIndex = 0
            };
            convertButton = new Button { Content = "Convert" };
            statusText = new TextBlock();

            var stackPanel = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 10
            };

            stackPanel.Children.Add(new TextBlock { Text = "Input File:" });
            stackPanel.Children.Add(inputFileTextBox);

            var browseButton = new Button { Content = "Browse..." };
            stackPanel.Children.Add(browseButton);

            stackPanel.Children.Add(new TextBlock { Text = "Target Format:" });
            stackPanel.Children.Add(formatComboBox);
            stackPanel.Children.Add(convertButton);
            stackPanel.Children.Add(statusText);

            this.Content = stackPanel;
        }

        private void SetupEventHandlers()
        {
            var browseButton = (Button)((StackPanel)this.Content).Children[2];
            browseButton.Click += async (sender, e) =>
            {
                var openFileDialog = new OpenFileDialog();
                openFileDialog.Filters.Add(new FileDialogFilter { Name = "Video Files", Extensions = { "mp4", "mov", "avi", "mkv", "webm" } });
                var result = await openFileDialog.ShowAsync(this);

                if (result != null && result.Length > 0)
                {
                    inputFileTextBox.Text = result[0];
                }
            };

            convertButton.Click += async (sender, e) =>
            {
                if (string.IsNullOrWhiteSpace(inputFileTextBox.Text))
                {
                    statusText.Text = "Please select a file first!";
                    return;
                }

                if (!File.Exists(inputFileTextBox.Text))
                {
                    statusText.Text = "Selected file doesn't exist!";
                    return;
                }

                string selectedFile = inputFileTextBox.Text;
                string targetFormat = formatComboBox.SelectedItem as string;

                statusText.Text = $"Converting to {targetFormat}...";

                try
                {
                    var converter = new VideoConverter();
                    converter.OnConversionCompleted += (outputFile) =>
                    {
                        statusText.Text = $"Conversion complete! Output file: {outputFile}";
                    };

                    await converter.ConvertAsync(selectedFile, targetFormat);

                    new LogManager().LogConversion(selectedFile,
                        Path.Combine(Path.GetDirectoryName(selectedFile),
                        Path.GetFileNameWithoutExtension(selectedFile) + "." + targetFormat),
                        targetFormat);
                }
                catch (Exception ex)
                {
                    statusText.Text = $"Error during conversion: {ex.Message}";
                }
            };
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