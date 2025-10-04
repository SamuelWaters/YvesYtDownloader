using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using YoutubeExplode.Videos.Streams;

namespace YvesYtDownload.Services;

public static class StreamSelector
{
    public static IVideoStreamInfo? SelectVideoStream(StreamManifest manifest, ILogger logger)
    {
        try
        {
            var videoStream = manifest.GetVideoStreams()
                .Where(s => s.Container == Container.Mp4)
                .FirstOrDefault(vq => vq.VideoQuality.IsHighDefinition);
            
            if (videoStream != null)
            {
                logger.LogInformation("Selected HD video stream: {Quality}", videoStream.VideoQuality.Label);
                return videoStream;
            }

            videoStream = manifest.GetVideoStreams()
                .Where(s => s.Container == Container.Mp4)
                .OrderByDescending(s => s.VideoQuality.MaxHeight)
                .FirstOrDefault();

            if (videoStream != null)
            {
                logger.LogInformation("Fallback: Selected non-HD video stream: {Quality}", videoStream.VideoQuality.Label);
                return videoStream;
            }

            videoStream = manifest.GetVideoStreams()
                .OrderByDescending(s => s.VideoQuality.MaxHeight)
                .FirstOrDefault();

            if (videoStream != null)
            {
                logger.LogInformation("Fallback: Selected video stream with any container: {Quality}, {Container}", 
                    videoStream.VideoQuality.Label, videoStream.Container);
            }
            else
            {
                logger.LogWarning("No video stream found");
            }

            return videoStream;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error selecting video stream");
            throw;
        }
    }

    public static IAudioStreamInfo? SelectAudioStream(StreamManifest manifest, ILogger logger)
    {
        try
        {
            var audioStreams = manifest.GetAudioStreams()
                .Where(s => s.Container == Container.Mp4);
            
            var audioStream = audioStreams.Any() ? audioStreams.GetWithHighestBitrate() : null;

            if (audioStream != null)
            {
                logger.LogInformation("Selected MP4 audio stream: {Bitrate}", audioStream.Bitrate);
                return audioStream as IAudioStreamInfo;
            }

            var allAudioStreams = manifest.GetAudioStreams();
            audioStream = allAudioStreams.Any() ? allAudioStreams.GetWithHighestBitrate() : null;

            if (audioStream != null)
            {
                logger.LogInformation("Fallback: Selected audio stream with any container: {Bitrate}, {Container}", 
                    audioStream.Bitrate, audioStream.Container);
                return audioStream as IAudioStreamInfo;
            }
            
            logger.LogWarning("No audio stream found");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error selecting audio stream");
            throw;
        }
    }
}
