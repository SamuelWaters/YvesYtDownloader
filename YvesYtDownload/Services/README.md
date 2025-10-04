# Services Directory

This directory contains the service layer abstractions and implementations for the YouTube downloader application.

## Files Overview

### IYoutubeService.cs
**Purpose**: Interface defining YouTube operations

**Methods**:
- `GetVideoAsync(string url)`: Retrieves video metadata
- `GetStreamManifestAsync(string videoId)`: Gets available streams
- `DownloadAsync(IStreamInfo[], string, IProgress<double>)`: Downloads video/audio

**Benefits**:
- Testability via mocking
- Dependency isolation
- Easy provider swapping

### YoutubeExplodeService.cs
**Purpose**: Production implementation of IYoutubeService

**Features**:
- ✅ Retry logic with exponential backoff
- ✅ Metadata caching (1-hour TTL)
- ✅ Comprehensive logging
- ✅ Specific exception handling
- ✅ Configurable HTTP timeout (5 minutes)

**Retry Strategy**:
```
Attempt 1: Immediate
Attempt 2: Wait 1 second
Attempt 3: Wait 2 seconds
Attempt 4: Wait 4 seconds (final)
```

**Exception Handling**:
- `VideoUnavailableException`: No retry, immediate failure
- `RequestLimitExceededException`: Retry with backoff
- `YoutubeExplodeException`: Retry with backoff
- `HttpRequestException`: Retry with backoff

### SimpleLogger.cs
**Purpose**: File-based logging implementation

**Configuration**:
- Log Path: `%LocalAppData%/YvesYtDownload/Logs/`
- Log Format: `app-{date}.log`
- Minimum Level: Information

**Log Format**:
```
[2024-01-15 14:30:45] [Information] [YoutubeExplodeService] Message here
```

**Thread Safety**: Uses lock for concurrent write protection

### StreamSelector.cs
**Purpose**: Smart stream selection with fallback logic

**Methods**:

#### `SelectVideoStream(StreamManifest, ILogger)`
**Selection Priority**:
1. HD (1080p+) MP4 video streams
2. Non-HD MP4 video streams (fallback)
3. Any quality with any container (fallback)
4. Returns null if no streams available

#### `SelectAudioStream(StreamManifest, ILogger)`
**Selection Priority**:
1. MP4 audio with highest bitrate
2. Any container audio with highest bitrate (fallback)
3. Returns null if no streams available

**Benefits**:
- Maximizes video quality
- Ensures download success
- Logs selection decisions

## Usage Example

```csharp
// Initialize service
var logger = new SimpleLogger<YoutubeExplodeService>();
var service = new YoutubeExplodeService(logger, ffmpegPath);

// Get video (with automatic retry & caching)
var video = await service.GetVideoAsync(url);

// Get streams
var manifest = await service.GetStreamManifestAsync(video.Id);

// Select best streams with fallbacks
var videoStream = StreamSelector.SelectVideoStream(manifest, logger);
var audioStream = StreamSelector.SelectAudioStream(manifest, logger);

// Download with progress tracking
if (videoStream != null && audioStream != null)
{
    var streams = new IStreamInfo[] { audioStream, videoStream };
    await service.DownloadAsync(
        streams, 
        outputPath, 
        new Progress<double>(p => Console.WriteLine($"{p:P0}")));
}
```

## Design Patterns Used

### 1. Repository Pattern (IYoutubeService)
- Abstracts data access (YouTube API)
- Enables testing with mocks
- Centralizes data operations

### 2. Decorator Pattern (Retry Logic)
- Enhances base operations
- Transparent to consumers
- Composable functionality

### 3. Cache-Aside Pattern
- Cache on read
- Expire after TTL
- Transparent to consumers

### 4. Fallback Pattern (StreamSelector)
- Graceful degradation
- Multiple quality options
- Maximizes success rate

## Testing

### Unit Test Example
```csharp
[Fact]
public async Task GetVideoAsync_CachesResult()
{
    // Arrange
    var logger = new Mock<ILogger<YoutubeExplodeService>>();
    var service = new YoutubeExplodeService(logger.Object, "ffmpeg.exe");
    
    // Act
    var video1 = await service.GetVideoAsync("test-url");
    var video2 = await service.GetVideoAsync("test-url"); // From cache
    
    // Assert
    Assert.Same(video1, video2);
}
```

### Integration Test Example
```csharp
[Fact]
public async Task DownloadAsync_SucceedsWithRealVideo()
{
    // Use a stable test video
    var service = new YoutubeExplodeService(logger, ffmpegPath);
    var video = await service.GetVideoAsync("dQw4w9WgXcQ");
    
    Assert.NotNull(video);
    Assert.Equal("Rick Astley - Never Gonna Give You Up", video.Title);
}
```

## Performance Considerations

### Caching Impact
- **First Request**: Full API call (~500ms)
- **Cached Request**: Memory lookup (~1ms)
- **Cache Size**: Minimal (metadata only)

### Retry Impact
- **Best Case**: 0 retries, normal latency
- **Worst Case**: 3 retries, 7 seconds additional delay
- **Success Rate**: Significantly improved

## Thread Safety

### YoutubeExplodeService
- ⚠️ Cache is NOT thread-safe
- 📝 Use single instance per UI thread
- 📝 For multi-threaded apps, use `ConcurrentDictionary`

### SimpleLogger
- ✅ Thread-safe via lock
- ✅ Safe for concurrent logging

## Error Handling Best Practices

```csharp
try
{
    var video = await service.GetVideoAsync(url);
}
catch (VideoUnavailableException)
{
    // Handle specific case - video removed/private
    ShowError("Video is unavailable");
}
catch (RequestLimitExceededException)
{
    // Handle rate limiting
    ShowError("Please try again later");
}
catch (Exception ex)
{
    // Handle unexpected errors
    LogError(ex);
    ShowError("An error occurred");
}
```

## Configuration Options

Future enhancement: Support configuration via `appsettings.json`:

```json
{
  "YoutubeService": {
    "MaxRetries": 3,
    "TimeoutMinutes": 5,
    "CacheExpirationHours": 1,
    "EnableLogging": true,
    "LogPath": "%LocalAppData%/YvesYtDownload/Logs"
  }
}
```

## Monitoring

Check logs for patterns:

```bash
# Find retry patterns
grep "Attempting to get video (attempt" app-*.log

# Find errors
grep "ERROR" app-*.log

# Find rate limiting
grep "RequestLimitExceededException" app-*.log
```

## Future Enhancements

1. **Async Caching**: Use `MemoryCache` with async locking
2. **Distributed Cache**: Redis for multi-instance apps
3. **Circuit Breaker**: Stop calling failing APIs temporarily
4. **Metrics**: Track success rates, latencies, cache hits
5. **Health Checks**: Periodic YouTube connectivity tests
