using System;
using System.Linq;
using System.Drawing;
using System.IO;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Control;

sealed class Track
{
    public readonly string Title, Artist, Album;
    public readonly double Duration;

    public Track(string title, string artist, string album, double duration)
    {
        Title = title.Trim();
        Artist = artist.Trim();
        Album = album.Trim();
        Duration = duration;
    }

    public string Key { get { return Title + "\n" + Artist + "\n" + Album; } }
}

sealed class Playback
{
    GlobalSystemMediaTransportControlsSessionManager manager;

    public IRandomAccessStreamReference Thumbnail { get; private set; }
    public Track Track { get; private set; }
    public double Position { get; private set; }
    public double Rate { get; private set; }
    public bool TimelineAvailable { get; private set; }
    public bool Playing { get; private set; }

    public async Task ReadAsync()
    {
        if (manager == null)
            manager = await Complete(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());

        var session = manager.GetSessions().FirstOrDefault(s =>
            s.SourceAppUserModelId.IndexOf("spotify", StringComparison.OrdinalIgnoreCase) >= 0);
        Track = null;
        Thumbnail = null;
        if (session == null) return;

        var metadata = await Complete(session.TryGetMediaPropertiesAsync());
        if (metadata == null || String.IsNullOrWhiteSpace(metadata.Title)) return;

        Thumbnail = metadata.Thumbnail;
        var timeline = session.GetTimelineProperties();
        var info = session.GetPlaybackInfo();
        double duration = Math.Max(0, (timeline.EndTime - timeline.StartTime).TotalSeconds);
        Position = (timeline.Position - timeline.StartTime).TotalSeconds;
        Playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        Rate = Playing ? info.PlaybackRate ?? 1 : 0;
        TimelineAvailable = timeline.LastUpdatedTime.Year >= 2000;
        if (TimelineAvailable)
            Position += Math.Max(0, (DateTimeOffset.UtcNow - timeline.LastUpdatedTime).TotalSeconds) * Rate;
        Track = new Track(metadata.Title, metadata.Artist ?? "", metadata.AlbumTitle ?? "", duration);
    }

    public static async Task<Bitmap> ReadCoverAsync(IRandomAccessStreamReference thumbnail)
    {
        if (thumbnail == null) return null;
        using (var stream = await Complete(thumbnail.OpenReadAsync()))
        {
            if (stream.Size == 0 || stream.Size > 12*1024*1024) return null;
            using (var reader = new DataReader(stream.GetInputStreamAt(0)))
            {
                uint size = await Complete<uint>(reader.LoadAsync((uint)stream.Size));
                if (size == 0 || size != stream.Size) return null;
                var bytes = new byte[size];
                reader.ReadBytes(bytes);
                return await Task.Run(() =>
                {
                    using (var file = new MemoryStream(bytes))
                    using (var image = Image.FromStream(file, false, true))
                    {
                        if (image.Width > 8192 || image.Height > 8192) return null;
                        return new Bitmap(image);
                    }
                });
            }
        }
    }

    public void Reconnect() { manager = null; }

    static async Task<T> Complete<T>(IAsyncOperation<T> operation)
    {
        var promise = new TaskCompletionSource<T>();
        operation.Completed = (completed, status) =>
        {
            try { promise.TrySetResult(completed.GetResults()); }
            catch (Exception error) { promise.TrySetException(error); }
        };
        if (await Task.WhenAny(promise.Task, Task.Delay(4000)) != promise.Task)
        {
            operation.Cancel();
            throw new TimeoutException("Windows no respondió al consultar Spotify.");
        }
        return await promise.Task;
    }
}

