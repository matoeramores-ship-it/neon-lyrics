using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

static class HDCover
{
    static readonly HttpClient client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(9),
        MaxResponseContentBufferSize = 8*1024*1024
    };

    sealed class Artist { public string name { get; set; } }
    sealed class Album
    {
        public string title { get; set; }
        public Artist artist { get; set; }
        public string cover_xl { get; set; }
    }
    sealed class Search { public Album[] data { get; set; } }

    public static async Task<Bitmap> Find(Track track, Bitmap local, CancellationToken token)
    {
        if (local == null || Math.Min(local.Width,local.Height) >= 1000 ||
            String.IsNullOrWhiteSpace(track.Artist) || String.IsNullOrWhiteSpace(track.Album)) return null;
        var before = local.Size;
        using (var reference = ArtDetail.Sample(local,new Size(24,24)))
        {
            string query = "https://api.deezer.com/search/album?limit=8&q="+Uri.EscapeDataString(track.Artist+" "+track.Album);
            using (var response = await client.GetAsync(query,token).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var result = new JavaScriptSerializer().Deserialize<Search>(json);

                if (result == null || result.data == null) return null;
                foreach (var item in result.data.Where(a => a != null && a.artist != null &&
                    String.Equals(a.title,track.Album,StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(a.artist.name,track.Artist,StringComparison.OrdinalIgnoreCase)).Take(3))
                {
                    Uri location;
                    if (!Uri.TryCreate(item.cover_xl,UriKind.Absolute,out location) || location.Scheme != "https" ||
                        !(location.Host.EndsWith(".dzcdn.net",StringComparison.OrdinalIgnoreCase) ||
                          location.Host.EndsWith(".deezer.com",StringComparison.OrdinalIgnoreCase))) continue;
                    using (var picture = await client.GetAsync(location,token).ConfigureAwait(false))
                    {

                        if (!picture.IsSuccessStatusCode) continue;
                        byte[] bytes = await picture.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        using (var memory = new MemoryStream(bytes))
                        using (var candidate = new Bitmap(memory))
                        {
                            if (candidate.Width<=before.Width || candidate.Height<=before.Height || candidate.Width>4096 || candidate.Height>4096) continue;

                            if (Matches(reference,candidate)) return new Bitmap(candidate);
                        }
                    }
                }
            }
        }
        return null;
    }

    internal static bool Matches(Bitmap reference,Bitmap candidate)
    {
        using (var small = ArtDetail.Sample(candidate,reference.Size))
        {
            long delta=0;
            for(int y=0;y<small.Height;y++)
            for(int x=0;x<small.Width;x++)
            {
                Color a=reference.GetPixel(x,y), b=small.GetPixel(x,y);
                delta+=Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B);
            }

            return delta < 3L*small.Width*small.Height*18;
        }
    }
}



