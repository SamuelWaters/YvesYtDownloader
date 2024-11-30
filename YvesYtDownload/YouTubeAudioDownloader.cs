using System;
using System.CodeDom;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Converter;

namespace YvesYtDownload;

public partial class YoutubeDownloader : Form
{
    private double _totalSize = 0;
    private string _ffmpegpath = Environment.CurrentDirectory + "\\ffmpeg.exe";

    public YoutubeDownloader()
    {
        this.InitializeComponent();
        this.urlTextBox.TextChanged += this.UrlTextBox_TextChanged;
        this.outputDirectoryTextBox.Text = Environment.CurrentDirectory.ToString();
    }

    private void UrlTextBox_TextChanged(object sender, EventArgs e)
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
        try
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
                await this.DownloadYouTubeVideoAsync(url);
                MessageBox.Show("Download complete!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.downloadButton.Enabled = true;
                this.statusLabel.Text = "Ready";
            }
        }
        catch (Exception ex)
        {
            throw ex; // TODO handle exception
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
            string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"); // Adjust if FFmpeg is not in PATH

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
                throw new Exception("FFmpeg conversion failed: " + await process.StandardError.ReadToEndAsync());
            }
        }
        catch (Exception ex)
        {
            throw; // TODO handle exception
        }
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
    }


    private async Task DownloadYouTubeVideoAsync(string url)
    {
        try
        {
            this.UpdateStatus("Getting manifest info...", 20);

            var youtube = new YoutubeClient();
            //Gets the video based on the URL provided by Yves
            var video = await youtube.Videos.GetAsync(url);
            //Gets the stream manifest, which contains information about all available video and audio streams for the selected video
            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);

            //Gets a video stream
            var videoStreamInfo = streamManifest.GetVideoStreams()
                                                //Get video streams in the mp4 format
                                                .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.Mp4)
                                                //Where quality is HD (1080p or higher)
                                                .FirstOrDefault(vq => vq.VideoQuality.IsHighDefinition);

            //Get audio stream
            var audioStreamInfo = streamManifest.GetAudioStreams()
                                                //Get mp4-compatible audio streams
                                                .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.Mp4)
                                                //Get the highest quality audio stream that YouTube has available
                                                .GetWithHighestBitrate();

            //Sanitise the title and remove characters that are invalid for Windows filenames
            string sanitizedTitle = YoutubeDownloader.SanitizeFileName(video.Title);
            //Output directory set by the user
            string outputDirectory = string.IsNullOrWhiteSpace(this.outputDirectoryTextBox.Text) ? Environment.CurrentDirectory : this.outputDirectoryTextBox.Text;

            string outputFilePath = Path.Combine(outputDirectory, $"{sanitizedTitle}.mp4");

            if (videoStreamInfo != null)
            {
                //Set total size for progress tracking on the interface 
                double totalVideoSize = videoStreamInfo.Size.Bytes / 1_000_000.0; // Convert to MB
                double totalAudioSize = audioStreamInfo.Size.Bytes / 1_000_000.0; // Convert to MB
                double totalSize = totalAudioSize + totalVideoSize;

                var streamInfos = new IStreamInfo[] { audioStreamInfo, videoStreamInfo };


                var crb = new ConversionRequestBuilder(outputFilePath);
                //Set the output format, medium speed for higher quality 
                crb.SetContainer(YoutubeExplode.Videos.Streams.Container.Mp4).SetFFmpegPath(this._ffmpegpath)
                   .SetPreset(ConversionPreset.Medium);
                crb.Build();
                //Initial progress bar value before download
                int progBarCurrent = this.progressBar1.Value;

                //Here we can pass in a progress variable to Youtube's API which will be a value between 0 and 1 that represents the percentage completion of the download process
                await youtube.Videos.DownloadAsync(
                    streamInfos, crb.Build(), new Progress<double>(
                        progress =>
                        {
                            double progressPercentage = progress * 79;
                            int progressValue = progBarCurrent + (int)progressPercentage;
                            this.UpdateProgress(progressValue);
                            this.UpdateStatus(
                                $"Downloading video and audio streams and converting... {progressValue}%, {progress * totalSize:F2} MB of {totalSize:F2} MB downloaded",
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
