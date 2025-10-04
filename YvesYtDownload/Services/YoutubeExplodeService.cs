using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Converter;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YvesYtDownload.Services;

public class YoutubeExplodeService : IYoutubeService
{
    private readonly YoutubeClient _client;
    private readonly ILogger<YoutubeExplodeService> _logger;
    private readonly Dictionary<string, (Video Video, DateTime CachedAt)> _videoCache;
    private readonly Dictionary<string, (StreamManifest Manifest, DateTime CachedAt)> _manifestCache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);
    private readonly int _maxRetries = 3;
    private readonly string _ffmpegPath;

    public YoutubeExplodeService(ILogger<YoutubeExplodeService> logger, string ffmpegPath)
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        _client = new YoutubeClient(httpClient);
        _logger = logger;
        _videoCache = new Dictionary<string, (Video, DateTime)>();
        _manifestCache = new Dictionary<string, (StreamManifest, DateTime)>();
        _ffmpegPath = ffmpegPath;
    }

    public async Task<Video> GetVideoAsync(string url)
    {
        if (_videoCache.TryGetValue(url, out var cached) &&
            DateTime.Now - cached.CachedAt < _cacheExpiration)
        {
            _logger.LogInformation("Retrieved video from cache: {Url}", url);
            return cached.Video;
        }

        var video = await GetVideoWithRetry(url);
        _videoCache[url] = (video, DateTime.Now);
        return video;
    }

    public async Task<StreamManifest> GetStreamManifestAsync(string videoId)
    {
        if (_manifestCache.TryGetValue(videoId, out var cached) &&
            DateTime.Now - cached.CachedAt < _cacheExpiration)
        {
            _logger.LogInformation("Retrieved stream manifest from cache: {VideoId}", videoId);
            return cached.Manifest;
        }

        var manifest = await GetStreamManifestWithRetry(videoId);
        _manifestCache[videoId] = (manifest, DateTime.Now);
        return manifest;
    }

    public async Task DownloadAsync(IStreamInfo[] streamInfos, string outputPath, IProgress<double> progress)
    {
        await DownloadWithRetry(streamInfos, outputPath, progress);
    }

    private async Task<Video> GetVideoWithRetry(string url)
    {
        for (int i = 0; i < _maxRetries; i++)
        {
            try
            {
                _logger.LogInformation("Attempting to get video (attempt {Attempt}/{MaxRetries}): {Url}", i + 1, _maxRetries, url);
                var video = await _client.Videos.GetAsync(url);
                _logger.LogInformation("Successfully retrieved video: {Title} ({VideoId})", video.Title, video.Id);
                return video;
            }
            catch (VideoUnavailableException ex)
            {
                _logger.LogError(ex, "Video is unavailable: {Url}", url);
                throw;
            }
            catch (RequestLimitExceededException ex)
            {
                _logger.LogWarning(ex, "Request limit exceeded for: {Url}. Attempt {Attempt}/{MaxRetries}", url, i + 1, _maxRetries);
                if (i == _maxRetries - 1)
                {
                    _logger.LogError("Failed to retrieve video after {MaxRetries} attempts due to rate limiting", _maxRetries);
                    throw;
                }
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
            }
            catch (YoutubeExplodeException ex)
            {
                _logger.LogWarning(ex, "YoutubeExplode error retrieving video: {Url}. Attempt {Attempt}/{MaxRetries}. Error type: {ExceptionType}", 
                    url, i + 1, _maxRetries, ex.GetType().Name);
                if (i == _maxRetries - 1)
                {
                    _logger.LogError(ex, "Failed to retrieve video after {MaxRetries} attempts", _maxRetries);
                    throw;
                }
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP request error retrieving video: {Url}. Attempt {Attempt}/{MaxRetries}", url, i + 1, _maxRetries);
                if (i == _maxRetries - 1)
                {
                    _logger.LogError(ex, "Failed to retrieve video after {MaxRetries} attempts due to network error", _maxRetries);
                    throw;
                }
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
            }
        }
        throw new InvalidOperationException("Retry loop completed without returning or throwing");
    }

    private async Task<StreamManifest> GetStreamManifestWithRetry(string videoId)
    {
        for (int i = 0; i < _maxRetries; i++)
        {
            try
            {
                _logger.LogInformation("Attempting to get stream manifest (attempt {Attempt}/{MaxRetries}): {VideoId}", i + 1, _maxRetries, videoId);
                var manifest = await _client.Videos.Streams.GetManifestAsync(videoId);
                _logger.LogInformation("Successfully retrieved stream manifest for: {VideoId}", videoId);
                return manifest;
            }
            catch (VideoUnavailableException ex)
            {
                _logger.LogError(ex, "Video streams unavailable: {VideoId}", videoId);
                throw;
            }
            catch (YoutubeExplodeException ex)
            {
                _logger.LogWarning(ex, "YoutubeExplode error retrieving manifest: {VideoId}. Attempt {Attempt}/{MaxRetries}. Error type: {ExceptionType}",
                    videoId, i + 1, _maxRetries, ex.GetType().Name);
                if (i == _maxRetries - 1)
                {
                    _logger.LogError(ex, "Failed to retrieve stream manifest after {MaxRetries} attempts", _maxRetries);
                    throw;
                }
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP request error retrieving manifest: {VideoId}. Attempt {Attempt}/{MaxRetries}", videoId, i + 1, _maxRetries);
                if (i == _maxRetries - 1)
                {
                    _logger.LogError(ex, "Failed to retrieve stream manifest after {MaxRetries} attempts due to network error", _maxRetries);
                    throw;
                }
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
            }
        }
        throw new InvalidOperationException("Retry loop completed without returning or throwing");
    }

    private async Task DownloadWithRetry(IStreamInfo[] streamInfos, string outputPath, IProgress<double> progress)
    {
        for (int i = 0; i < _maxRetries; i++)
        {
            try
            {
                _logger.LogInformation("Attempting to download video (attempt {Attempt}/{MaxRetries}): {OutputPath}", i + 1, _maxRetries, outputPath);
                
                var crb = new ConversionRequestBuilder(outputPath);
                crb.SetContainer(Container.Mp4)
                   .SetFFmpegPath(_ffmpegPath)
                   .SetPreset(ConversionPreset.Medium);

                await _client.Videos.DownloadAsync(streamInfos, crb.Build(), progress);
                
                _logger.LogInformation("Successfully downloaded video: {OutputPath}", outputPath);
                return;
            }
            catch (YoutubeExplodeException ex)
            {
                _logger.LogWarning(ex, "YoutubeExplode error downloading video: {OutputPath}. Attempt {Attempt}/{MaxRetries}. Error type: {ExceptionType}",
                    outputPath, i + 1, _maxRetries, ex.GetType().Name);
                if (i == _maxRetries - 1)
                {
                    _logger.LogError(ex, "Failed to download video after {MaxRetries} attempts", _maxRetries);
                    throw;
                }
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP request error downloading video: {OutputPath}. Attempt {Attempt}/{MaxRetries}", outputPath, i + 1, _maxRetries);
                if (i == _maxRetries - 1)
                {
                    _logger.LogError(ex, "Failed to download video after {MaxRetries} attempts due to network error", _maxRetries);
                    throw;
                }
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
            }
        }
    }
}
