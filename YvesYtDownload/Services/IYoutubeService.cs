using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YvesYtDownload.Services;

public interface IYoutubeService
{
    Task<Video> GetVideoAsync(string url);
    Task<StreamManifest> GetStreamManifestAsync(string videoId);
    Task DownloadAsync(IStreamInfo[] streamInfos, string outputPath, IProgress<double> progress);
}
