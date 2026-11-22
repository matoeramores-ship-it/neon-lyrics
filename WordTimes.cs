using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

static class WordTimes
{
    static readonly Regex InstrumentalMark = new Regex(@"^(?:(?:[♪♫♬♩\s]|🎵|🎶)+|[\[(]\s*(?:instrumental|instrumental break|interludio|interlude|guitar solo|solo de guitarra)\s*[\])])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    internal sealed class FileData { public List<Row> lines { get; set; } }
    internal sealed class Row
    {
        public string text { get; set; }
        public int? start_ms { get; set; }
        public int? end_ms { get; set; }
        public List<Row> words { get; set; }
    }

    public static void LoadReader()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            string assembly = new AssemblyName(args.Name).Name;
            if (assembly != "YamlDotNet" && assembly != "NAudio") return null;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(assembly + ".dll"))
            using (var copy = new System.IO.MemoryStream())
            {
                stream.CopyTo(copy);
                return Assembly.Load(copy.ToArray());
            }
        };
    }

    public static LyricLine[] FromRecord(Lyrics.Record record)
    {
        if (record.instrumental) return new[] { new LyricLine(0, "") };
        if (!String.IsNullOrWhiteSpace(record.lyricsfile))
        {
            try
            {
                var actual = ReadFile(record.lyricsfile, record.duration);
                if (actual.Any(c => c.Text.Length != 0) || actual.Length > 0 && String.IsNullOrWhiteSpace(record.syncedLyrics)) return actual;
            }
            catch (YamlException) { /* A line-synced copy can still be used. */ }
            catch (FormatException) { }
            catch (OverflowException) { }
        }
        return Estimate(Lyrics.Parse(record.syncedLyrics), record.duration);
    }

    static LyricLine[] ReadFile(string yaml, double duration)
    {
        var reader = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        var file = reader.Deserialize<FileData>(yaml);
        if (file == null || file.lines == null) return new LyricLine[0];
        var lines = file.lines.Where(r => r != null && r.start_ms.HasValue).OrderBy(r => r.start_ms).ToArray();
        var cues = new List<LyricLine>();
        double heldUntil = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            double start = line.start_ms.Value / 1000.0;
            double next = i + 1 < lines.Length ? lines[i + 1].start_ms.Value / 1000.0 : duration;
            double end = line.end_ms.HasValue && line.end_ms > line.start_ms ? line.end_ms.Value / 1000.0 : next;
            if (!String.IsNullOrEmpty(line.text) && InstrumentalMark.IsMatch(line.text.Trim()))
            {
                cues.Add(new LyricLine(Math.Max(start, heldUntil), ""));
                continue;
            }
            var words = line.words == null ? null : WholeWords(line);
            if (words == null || words.Length == 0)
            {
                if (end <= 0 || end > Math.Max(start, heldUntil))
                    Spread(cues, line.text ?? "", Math.Max(start, heldUntil), end, i + 1 == lines.Length || end < next);
            }
            else
            {
                for (int j = 0; j < words.Length; j++)
                {
                    var word = words[j];
                    double onset = word.start_ms.Value / 1000.0;
                    double show = Math.Max(onset, heldUntil);
                    double boundary = j + 1 < words.Length ? words[j + 1].start_ms.Value / 1000.0 : next;
                    bool timedEnd = word.end_ms.HasValue && word.end_ms > word.start_ms;
                    double stop = timedEnd ? word.end_ms.Value / 1000.0 : (j + 1 == words.Length ? end : boundary);
                    if (stop > 0 && stop <= show) continue;
                    cues.Add(new LyricLine(show, word.text.Trim(), timedEnd ? stop : Double.NaN));
                    if (timedEnd) heldUntil = stop;
                    if (stop > show && (i + 1 == lines.Length && j + 1 == words.Length || boundary - stop > .16))
                        cues.Add(new LyricLine(stop, ""));
                }
            }
        }
        return cues.OrderBy(w => w.Seconds).ToArray();
    }

    static Row[] WholeWords(Row line)
    {
        var parts = line.words.Where(w => w != null && w.start_ms.HasValue && !String.IsNullOrWhiteSpace(w.text)).OrderBy(w => w.start_ms).ToArray();
        var tokens = Regex.Matches(line.text ?? "", @"\S+");
        if (tokens.Count == 0 || tokens.Count >= parts.Length) return parts;
        var words = new List<Row>(tokens.Count);
        int at = 0;
        foreach (Match token in tokens)
        {
            int first = at;
            string joined = "";
            while (at < parts.Length && joined.Length < token.Length)
                joined += parts[at++].text.Trim();
            if (!String.Equals(joined, token.Value, StringComparison.OrdinalIgnoreCase)) return parts;
            words.Add(new Row { text = token.Value, start_ms = parts[first].start_ms, end_ms = parts[at - 1].end_ms });
        }
        return at == parts.Length ? words.ToArray() : parts;
    }

    internal static LyricLine[] Estimate(LyricLine[] lines, double duration)
    {
        var cues = new List<LyricLine>();
        for (int i = 0; i < lines.Length; i++)
        {
            double next = i + 1 < lines.Length ? lines[i + 1].Seconds : duration;
            if (i + 1 == lines.Length || next > lines[i].Seconds)
                Spread(cues, InstrumentalMark.IsMatch(lines[i].Text) ? "" : lines[i].Text, lines[i].Seconds, next, i + 1 == lines.Length);
        }
        return cues.OrderBy(c => c.Seconds).ToArray();
    }

    static void Spread(List<LyricLine> result, string text, double start, double end, bool clearAtEnd)
    {
        var words = Regex.Matches(text, @"\S+").Cast<Match>().Select(m => m.Value).ToArray();
        if (words.Length == 0) { result.Add(new LyricLine(start, "")); return; }
        double span = end > start ? end - start : Math.Max(2.2, words.Length * .77);
        var weights = words.Select(w => 1 + Math.Min(w.Length, 9) * .095).ToArray();
        double unit = span / weights.Sum();
        double cursor = start;
        for (int i = 0; i < words.Length; i++)
        {
            result.Add(new LyricLine(cursor, words[i]));
            cursor += weights[i] * unit;
        }
        if (clearAtEnd) result.Add(new LyricLine(end > start ? end : cursor, ""));
    }
}

