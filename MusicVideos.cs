using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

static class MusicVideos
{
    const long ByteLimit = 160L * 1024 * 1024;
    static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(22), MaxResponseContentBufferSize = 4 * 1024 * 1024 };
    static MusicVideos() { http.DefaultRequestHeaders.UserAgent.ParseAdd("NeonLyricsWindows/2.8"); }
    internal sealed class Item
    {
        public string identifier { get; set; }
        public string title { get; set; }
        public object creator { get; set; }
    }
    sealed class Search { public Results response { get; set; } }
    sealed class Results { public Item[] docs { get; set; } }
    sealed class Metadata { public Asset[] files { get; set; } }
    sealed class Asset
    {
        public string name { get; set; }
        public string size { get; set; }
        public string height { get; set; }
        public string length { get; set; }
        public string @private { get; set; }
    }

    static string Words(string text)
    {
        string folded = (text ?? "").Normalize(NormalizationForm.FormD).ToLowerInvariant();
        folded = Regex.Replace(folded, @"\p{M}", "");
        return Regex.Replace(folded, @"[^\p{L}\p{N}]+", " ").Trim();
    }

    internal static int Rank(Item item, Track track)
    {
        if (item == null || !Regex.IsMatch(item.identifier ?? "", @"^[a-zA-Z0-9_-][a-zA-Z0-9_.-]{0,180}$")) return 0;
        string artist = Words(track.Artist), song = Words(track.Title), title = Words(item.title);
        if (artist.Length == 0 || song.Length == 0) return 0;
        bool official = title.Contains("official");
        string bare = Regex.Replace(title, @"(?: (?:official music video|official video|music video|video oficial|videoclip oficial|official hd video|hd|hq|4k remaster|4k|1080p|720p))+$", "");
        var authors = item.creator as IEnumerable;
        bool sameArtist = item.creator is string ? Words((string)item.creator) == artist :
            authors != null && authors.Cast<object>().Any(a => Words(a as string) == artist);
        if (bare != artist + " " + song && bare != song + " " + artist && !(sameArtist && bare == song)) return 0;
        return 1 + (sameArtist ? 4 : 0) + (official ? 2 : 0);
    }

    static async Task<T> Read<T>(string url, CancellationToken token)
    {
        using (var res = await http.GetAsync(url, token).ConfigureAwait(false))
        {
            res.EnsureSuccessStatusCode();
            var json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
            return json.Deserialize<T>(await res.Content.ReadAsStringAsync().ConfigureAwait(false));
        }
    }

    static double Number(string s)
    {
        double n;
        return Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out n) ? n : 0;
    }

    public static async Task<string> Find(Track track, CancellationToken token)
    {
        string title = Words(track.Title), artist = Words(track.Artist);
        if (title.Length == 0 || artist.Length == 0) return null;
        string query = "mediatype:movies AND title:\"" + title + "\" AND (creator:\"" + artist + "\" OR title:\"" + artist + "\")";
        var hits = await Read<Search>("https://archive.org/advancedsearch.php?output=json&rows=32&fl[]=identifier&fl[]=title&fl[]=creator&q=" + Uri.EscapeDataString(query), token).ConfigureAwait(false);
        if (hits == null || hits.response == null || hits.response.docs == null) throw new InvalidDataException("Respuesta de búsqueda inválida.");
        foreach (var candidate in hits.response.docs.Select(i => new { Item = i, Score = Rank(i, track) }).Where(c => c.Score > 0).OrderByDescending(c => c.Score).Take(4))
        {
            string id = candidate.Item.identifier;
            var meta = await Read<Metadata>("https://archive.org/metadata/" + id, token).ConfigureAwait(false);
            if (meta == null || meta.files == null) continue;
            var files = meta.files.Where(f => f != null && !String.IsNullOrEmpty(f.name) && f.name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                && f.name.IndexOfAny(new[] {'/', '\\'}) < 0 && !String.Equals(f.@private, "true", StringComparison.OrdinalIgnoreCase)
                && Number(f.size) > 0 && Number(f.size) <= ByteLimit && (track.Duration <= 0 || Number(f.length) == 0 ||
                    Math.Abs(Number(f.length) - track.Duration) <= Math.Max(20, track.Duration * .13))).ToArray();
            foreach (var file in files.Where(f => files.Length == 1 || Words(f.name).Replace(" ", "").Contains(title.Replace(" ", "")))
                .OrderByDescending(f => Math.Min(1080, Number(f.height))).ThenBy(f => Number(f.size)).Take(2))
            {
                var path = await Download("https://archive.org/download/" + id + "/" + Uri.EscapeDataString(file.name), token).ConfigureAwait(false);
                if (path != null) return path;
            }
        }
        return null;
    }

    static async Task<string> Download(string url, CancellationToken token)
    {
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            deadline.CancelAfter(TimeSpan.FromMinutes(3));
            using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > ByteLimit) return null;
                string path = Path.Combine(Path.GetTempPath(), "NeonLyrics-" + Guid.NewGuid().ToString("N") + ".mp4");
                bool complete = false;
                try
                {
                    using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    using (deadline.Token.Register(input.Dispose))
                    {
                        byte[] buffer = new byte[81920];
                        int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, deadline.Token).ConfigureAwait(false)) > 0)
                        {
                            if (output.Length + read > ByteLimit) return null;
                            await output.WriteAsync(buffer, 0, read, deadline.Token).ConfigureAwait(false);
                        }
                        if (output.Length == 0 || (response.Content.Headers.ContentLength.HasValue && output.Length != response.Content.Headers.ContentLength.Value)) return null;
                    }
                    deadline.Token.ThrowIfCancellationRequested();
                    complete = true;
                    return path;
                }
                finally { if (!complete) Discard(path); }
            }
        }
    }

    public static void Discard(string path)
    {
        if (path == null) return;
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
