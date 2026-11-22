using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

sealed class LyricLine
{
    public readonly double Seconds;
    public readonly string Text;
    public readonly string Icon;
    public readonly int Rings;
    public readonly double VocalEnd;
    int cachedCopies;
    string cachedText;

    public LyricLine(double seconds, string text, double vocalEnd = Double.NaN)
    {
        Seconds = seconds; Text = text; VocalEnd = vocalEnd;
        Icon = LyricIcons.Match(text);
        Rings = LyricIcons.CountRings(text);
    }

    // Common English endings where repeating the written final e would suggest a different sound.
    static readonly Regex SilentE = new Regex(@"^(?:love|give|live|life|wife|time|mine|shine|like|drive|ride|side|smile|while|alive|alone|one|home|come|some|hope|close|move|prove|lose|take|make|made|same|name|save|wave|change|dance|face|place|please|blue|true)s?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public string HeldText(double position, double end)
    {
        bool exact = !Double.IsNaN(VocalEnd);
        if (exact) end = Math.Min(end, VocalEnd);
        double normal = (exact ? .58 : .82) + .045 * Math.Min(10, Text.Length);
        if (Double.IsInfinity(end) || end - Seconds < normal + .20) return Text;
        double age = Math.Min(position, end) - Seconds;
        if (age < normal) return Text;
        int copies = 1 + (int)Math.Min(11, (age - normal) / .135);
        if (copies != cachedCopies)
        {
            cachedText = RepeatVowel(copies);
            cachedCopies = copies;
        }
        return cachedText;
    }

    string RepeatVowel(int copies)
    {
        string word;
        try { word = Text.Normalize(); } catch (ArgumentException) { return Text; }
        int last = word.Length - 1;
        while (last >= 0 && !Char.IsLetter(word[last])) last--;
        if (last < 0) return Text;
        int first = 0;
        while (first < last && !Char.IsLetter(word[first])) first++;
        if (SilentE.IsMatch(word.Substring(first, last - first + 1))) last = Math.Max(word.LastIndexOf('e', last), word.LastIndexOf('E', last)) - 1;
        for (int i = last; i >= first; i--)
        {
            if ("aeiouáéíóúüAEIOUÁÉÍÓÚÜ".IndexOf(word[i]) < 0 && !(i == last && (word[i] == 'y' || word[i] == 'Y'))) continue;
            return word.Insert(i + 1, new String(word[i], copies));
        }
        return Text;
    }
}

sealed class Lyrics
{
    static readonly Regex Timestamp = new Regex(@"\[(\d{1,4}):([0-5]\d)(?:[.:](\d{1,3}))?\]", RegexOptions.CultureInvariant);
    static readonly Regex Offset = new Regex(@"(?im)^\s*\[offset\s*:\s*([+-]?\d{1,6})\]", RegexOptions.CultureInvariant);
    readonly HttpClient http = new HttpClient { BaseAddress = new Uri("https://lrclib.net/"), Timeout = TimeSpan.FromSeconds(14) };
    readonly Dictionary<string, LyricLine[]> cache = new Dictionary<string, LyricLine[]>();
    readonly SemaphoreSlim requests = new SemaphoreSlim(1);
    DateTimeOffset retryAfter;

    public Lyrics()
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd("NeonLyricsWindows/2.0");
        http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<LyricLine[]> FindAsync(Track track, CancellationToken cancellation)
    {
        await requests.WaitAsync(cancellation);
        try
        {
            if (String.IsNullOrWhiteSpace(track.Artist)) return new LyricLine[0];
            LyricLine[] lines;
            if (cache.TryGetValue(track.Key, out lines)) return lines;

            string query = "track_name=" + Uri.EscapeDataString(track.Title) + "&artist_name=" + Uri.EscapeDataString(track.Artist);
            var json = new JavaScriptSerializer();
            lines = new LyricLine[0];
            if (track.Duration >= 1 && track.Duration <= 3600 && track.Album.Length != 0)
            {
                string exact = query + "&album_name=" + Uri.EscapeDataString(track.Album) + "&duration=" +
                    Math.Round(track.Duration).ToString(CultureInfo.InvariantCulture);
                string body = await GetAsync("api/get?" + exact, cancellation);
                if (body != null)
                {
                    var match = json.Deserialize<Record>(body);
                    lines = WordTimes.FromRecord(match);
                }
                if (lines.Length == 0) await Task.Delay(300, cancellation);
            }

            if (lines.Length == 0)
            {
                string body = await GetAsync("api/search?" + query, cancellation);
                if (body != null) lines = MatchTrack(json.Deserialize<Record[]>(body), track);
            }

            if (cache.Count >= 50) cache.Clear();
            cache[track.Key] = lines;
            return lines;
        }
        finally { requests.Release(); }
    }

    async Task<string> GetAsync(string address, CancellationToken cancellation)
    {
        if (DateTimeOffset.UtcNow < retryAfter)
            throw new HttpRequestException("LRCLIB pidió esperar antes de volver a consultar.");
        using (var response = await http.GetAsync(address, cancellation))
        {
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if ((int)response.StatusCode == 429)
            {
                var limit = response.Headers.RetryAfter;
                retryAfter = limit == null ? DateTimeOffset.UtcNow.AddMinutes(1) :
                    limit.Date ?? DateTimeOffset.UtcNow.Add(limit.Delta ?? TimeSpan.FromMinutes(1));
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }

    internal static LyricLine[] MatchTrack(Record[] records, Track track)
    {
        return (records ?? new Record[0])
            .Where(record => record != null && (record.instrumental || !String.IsNullOrWhiteSpace(record.lyricsfile) || !String.IsNullOrWhiteSpace(record.syncedLyrics))
                && Equal(record.trackName, track.Title) && Equal(record.artistName, track.Artist)
                && (track.Duration <= 0 || Math.Abs(record.duration - track.Duration) <= 4))
            .OrderByDescending(record => Equal(record.albumName, track.Album))
            .ThenBy(record => track.Duration <= 0 ? 0 : Math.Abs(record.duration - track.Duration))
            .ThenByDescending(record => record.hasWordSync)
            .Select(WordTimes.FromRecord)
            .FirstOrDefault(lines => lines.Length > 0) ?? new LyricLine[0];
    }

    static bool Equal(string left, string right)
    {
        return left != null && String.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static LyricLine[] Parse(string lrc)
    {
        if (String.IsNullOrWhiteSpace(lrc)) return new LyricLine[0];
        var offset = Offset.Match(lrc);
        double adjustment = offset.Success ? Int32.Parse(offset.Groups[1].Value, CultureInfo.InvariantCulture) / 1000.0 : 0;
        var lines = new List<LyricLine>();
        foreach (string row in lrc.Split('\n'))
        {
            var times = Timestamp.Matches(row);
            if (times.Count == 0) continue;
            var last = times[times.Count - 1];
            string text = row.Substring(last.Index + last.Length).Trim();
            foreach (Match time in times)
            {
                double seconds = Int32.Parse(time.Groups[1].Value, CultureInfo.InvariantCulture) * 60 +
                    Int32.Parse(time.Groups[2].Value, CultureInfo.InvariantCulture);
                if (time.Groups[3].Success)
                    seconds += Double.Parse("0." + time.Groups[3].Value, CultureInfo.InvariantCulture);
                lines.Add(new LyricLine(Math.Max(0, seconds - adjustment), text));
            }
        }
        return lines.OrderBy(line => line.Seconds).ToArray();
    }

    public static int At(LyricLine[] lines, double position)
    {
        int first = 0, end = lines.Length;
        while (first < end)
        {
            int mid = first + (end - first) / 2;
            if (lines[mid].Seconds <= position) first = mid + 1;
            else end = mid;
        }
        return first - 1;
    }

    internal sealed class Record
    {
        public string trackName { get; set; }
        public string artistName { get; set; }
        public string albumName { get; set; }
        public double duration { get; set; }
        public bool instrumental { get; set; }
        public string syncedLyrics { get; set; }
        public bool hasWordSync { get; set; }
        public string lyricsfile { get; set; }
    }
}

