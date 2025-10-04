using System;
using System.CodeDom;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos.Streams;
using YvesYtDownload.Services;

namespace YvesYtDownload;

public partial class YoutubeDownloader : Form
{
    private readonly string _ffmpegpath = Environment.CurrentDirectory + "\\ffmpeg.exe";
    private readonly IYoutubeService _youtubeService;
    private readonly ILogger<YoutubeDownloader> _logger;
    private readonly ILogger<YoutubeExplodeService> _serviceLogger;

    public YoutubeDownloader()
    {
        this.InitializeComponent();
        this.urlTextBox.TextChanged += this.UrlTextBox_TextChanged;
        this.outputDirectoryTextBox.Text = Environment.CurrentDirectory.ToString();
        
        _logger = new SimpleLogger<YoutubeDownloader>();
        _serviceLogger = new SimpleLogger<YoutubeExplodeService>();
        _youtubeService = new YoutubeExplodeService(_serviceLogger, _ffmpegpath);
    }

    private void UrlTextBox_TextChanged(object? sender, EventArgs e)
    {
        string url = this.urlTextBox.Text;
        if (string.IsNullOrWhiteSpace(url) || !this.IsValidYouTubeUrl(url))
        {
            this.statusLabel.Text = "Enter a valid YouTube URL.";
        }
        else
        {
            this.statusLabel.Text = "Ready to download.";
        }
    }

    private async void downloadButton_Click(object sender, EventArgs e)
    {
        string url = this.urlTextBox.Text;

        if (string.IsNullOrWhiteSpace(url) || !this.IsValidYouTubeUrl(url))
        {
            MessageBox.Show("Please enter a valid YouTube URL.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            this.downloadButton.Enabled = false;
            _logger.LogInformation("Starting download for URL: {Url}", url);
            await this.DownloadYouTubeVideoAsync(url);
            _logger.LogInformation("Download completed successfully for URL: {Url}", url);
            MessageBox.Show("Download complete!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (VideoUnavailableException ex)
        {
            _logger.LogError(ex, "Video is unavailable: {Url}", url);
            MessageBox.Show("This video is unavailable or has been removed.", "Video Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (RequestLimitExceededException ex)
        {
            _logger.LogError(ex, "Request limit exceeded for: {Url}", url);
            MessageBox.Show("YouTube rate limit exceeded. Please try again later.", "Rate Limit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during download: {Url}. Error type: {ExceptionType}", url, ex.GetType().Name);
            MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            this.downloadButton.Enabled = true;
            this.statusLabel.Text = "Ready";
        }
    }

    private void browseButton_Click(object sender, EventArgs e)
    {
        if (this.folderBrowserDialog.ShowDialog() == DialogResult.OK)
        {
            this.outputDirectoryTextBox.Text = this.folderBrowserDialog.SelectedPath;
        }
    }

    private void OpenOutputDirectory()
    {
        string directoryPath = this.outputDirectoryTextBox.Text;

        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            MessageBox.Show("Please select a valid output directory first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Process.Start(
            new ProcessStartInfo()
            {
                FileName = directoryPath,
                UseShellExecute = true,
                Verb = "open"
            });
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_'); // Replace invalid characters with an underscore
        }

        return fileName;
    }

    private async Task ConvertToMp3(string inputFilePath, string outputFilePath)
    {
        try
        {
            string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            _logger.LogInformation("Converting to MP3: {InputPath} -> {OutputPath}", inputFilePath, outputFilePath);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-i \"{inputFilePath}\" -f mp3 -ab 192000 -vn \"{outputFilePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                string errorOutput = await process.StandardError.ReadToEndAsync();
                _logger.LogError("FFmpeg conversion failed with exit code {ExitCode}: {Error}", process.ExitCode, errorOutput);
                throw new Exception("FFmpeg conversion failed: " + errorOutput);
            }
            
            _logger.LogInformation("Successfully converted to MP3: {OutputPath}", outputFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MP3 conversion: {InputPath}", inputFilePath);
            throw;
        }
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
    }


    private async Task DownloadYouTubeVideoAsync(string url)
    {
        try
        {
            this.UpdateStatus("Getting video info...", 10);

            var video = await _youtubeService.GetVideoAsync(url);
            
            this.UpdateStatus("Getting stream manifest...", 20);
            var streamManifest = await _youtubeService.GetStreamManifestAsync(video.Id);

            var videoStreamInfo = StreamSelector.SelectVideoStream(streamManifest, _logger);
            var audioStreamInfo = StreamSelector.SelectAudioStream(streamManifest, _logger);

            if (audioStreamInfo == null)
            {
                throw new InvalidOperationException("No suitable audio stream found for this video.");
            }

            string sanitizedTitle = YoutubeDownloader.SanitizeFileName(video.Title);
            string outputDirectory = string.IsNullOrWhiteSpace(this.outputDirectoryTextBox.Text) 
                ? Environment.CurrentDirectory 
                : this.outputDirectoryTextBox.Text;

            string outputFilePath = Path.Combine(outputDirectory, $"{sanitizedTitle}.mp4");

            if (videoStreamInfo != null)
            {
                double totalVideoSize = videoStreamInfo.Size.Bytes / 1_000_000.0;
                double totalAudioSize = audioStreamInfo.Size.Bytes / 1_000_000.0;
                double totalSize = totalAudioSize + totalVideoSize;

                var streamInfos = new IStreamInfo[] { audioStreamInfo, videoStreamInfo };
                int progBarCurrent = this.progressBar1.Value;

                await _youtubeService.DownloadAsync(
                    streamInfos, 
                    outputFilePath, 
                    new Progress<double>(progress =>
                    {
                        double progressPercentage = progress * 79;
                        int progressValue = progBarCurrent + (int)progressPercentage;
                        this.UpdateProgress(progressValue);
                        this.UpdateStatus(
                            $"Downloading video and audio streams and converting... {progressValue}%, {progress * totalSize:F2} MB of {totalSize:F2} MB downloaded",
                            progressValue);
                    }));
            }
            else
            {
                _logger.LogInformation("No video stream available, downloading audio only");
                
                double totalAudioSize = audioStreamInfo.Size.Bytes / 1_000_000.0;
                var streamInfos = new IStreamInfo[] { audioStreamInfo };
                int progBarCurrent = this.progressBar1.Value;

                await _youtubeService.DownloadAsync(
                    streamInfos, 
                    outputFilePath, 
                    new Progress<double>(progress =>
                    {
                        double progressPercentage = progress * 79;
                        int progressValue = progBarCurrent + (int)progressPercentage;
                        this.UpdateProgress(progressValue);
                        this.UpdateStatus(
                            $"Downloading audio stream and converting... {progressValue}%, {progress * totalAudioSize:F2} MB of {totalAudioSize:F2} MB downloaded",
                            progressValue);
                    }));
            }

            this.UpdateStatus("Download complete!", 100);
        }
        catch (Exception ex)
        {
            this.UpdateStatus($"Error: {ex.Message}", 0);
            throw;
        }
    }

    private void UpdateStatus(string message, int progressValue)
    {
        this.statusLabel.Text = message;
        this.UpdateProgress(progressValue);
    }

    private void UpdateProgress(int value)
    {
        this.progressBar1.Value = Math.Min(value, 100);
        this.progressBar1.Refresh();
    }

    private bool IsValidYouTubeUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
               (uriResult.Host.Contains("youtube.com") || uriResult.Host.Contains("youtu.be"));
    }

    private void openExpl_Click(object sender, EventArgs e)
    {
        this.OpenOutputDirectory();
    }

    private void progressBar1_Click(object sender, EventArgs e)
    {

    }
}
