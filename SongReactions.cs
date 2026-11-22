using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;

sealed class Expression
{
    public readonly string Face;
    public readonly Color Color;

    public Expression(string face, int rgb)
    {
        Face = face;
        Color = Color.FromArgb(255, Color.FromArgb(rgb));
    }
}

sealed class SongReactions
{
    internal const double VisibleSeconds = 3.8;
    const double Spacing = 13;
    static readonly Expression Tender = new Expression("🥰", 0xFFABBC);
    static readonly Expression Heartache = new Expression("🥺", 0xB6BDFF);
    static readonly Expression Dance = new Expression("😎", 0xFFD970);
    static readonly Expression Memory = new Expression("🥲", 0xD1B0FF);
    static readonly Expression Brave = new Expression("😏", 0xFFBD85);
    static readonly Expression Calm = new Expression("😌", 0xA8E6CB);
    static readonly Expression Gloom = new Expression("😤", 0xFFA398);
    static readonly Expression Listening = new Expression("🎧", 0xAEDFF5);
    static readonly Dictionary<string,Expression> patterns = new Dictionary<string,Expression>();
    static readonly Regex punctuation = new Regex(@"[^\p{L}\p{Nd}]+");
    static readonly Regex marks = new Regex(@"\p{M}+");
    static readonly Regex negation = new Regex(@"(?:^| )(?:no|not|never|nunca|dont|isnt|wasnt|cant|cannot|sin)(?: |$)");

    static SongReactions()
    {
        Add(Tender, "te amo|te quiero|me enamoré|mi amor|quédate conmigo|bésame|enamorado|enamorada|besarte|amor|love you|in love|hold me close|kiss me|darling|sweetheart|love");
        Add(Heartache, "me duele|me rompiste|corazón roto|ya no me quieres|no te amo|ya no te quiero|sin ti|te perdí|me dejaste|llorando|desamor|triste|soledad|broken heart|broke my heart|love hurts|dont love you|without you|left me|heartbroken|crying|lonely|tears");
        Add(Dance, "a bailar|toda la pista|baila conmigo|bailando|perreo|fiesta|celebrar|dance with me|dance floor|lets dance|party|dancing|celebrate|turn it up");
        Add(Memory, "te extraño|te echo de menos|me acuerdo|aquellos días|ya te fuiste|nostalgia|recuerdos|i miss you|miss you|remember when|used to be|those days|memories|nostalgic");
        Add(Brave, "yo puedo|me levanté|sigo de pie|no me rindo|lo logré|im unstoppable|wont give up|still standing|my own way|unstoppable|fearless|invencible");
        Add(Calm, "en calma|paz|despacio|respira|tranquilidad|dulces sueños|close your eyes|slow down|breathe|peaceful|lullaby|take it slow");
        Add(Gloom, "estoy harto|estoy harta|ya basta|me cansé|rabia|furia|fed up|had enough|im angry|rage|furious");
    }

    static string Fold(string text)
    {
        text = (text ?? "").ToLowerInvariant().Replace("’","").Replace("'","");
        try { text = marks.Replace(text.Normalize(NormalizationForm.FormD), ""); } catch (ArgumentException) { return ""; }
        return punctuation.Replace(text, " ").Trim();
    }

    static void Add(Expression mood, string phrases)
    {
        foreach (var phrase in phrases.Split('|')) patterns.Add(Fold(phrase), mood);
    }

    // Match the longest phrase ending at this word. A bare keyword after a negation is ambiguous.
    static Expression ReadWord(List<string> recent, string word, out int strength)
    {
        if (recent.Count == 5) recent.RemoveAt(0);
        recent.Add(word);
        for (strength = recent.Count; strength > 0; strength--)
        {
            Expression mood;
            string phrase = String.Join(" ", recent.GetRange(recent.Count-strength, strength));
            if (!patterns.TryGetValue(phrase, out mood)) continue;
            string before = String.Join(" ", recent.GetRange(0, recent.Count-strength));
            if (negation.IsMatch(before)) return null;
            return mood;
        }
        return null;
    }

    sealed class Cue { public double At; public Expression Mood; public int Strength; }
    readonly List<Cue> cues = new List<Cue>();
    readonly Expression titleMood;
    int lastSlot = -1;
    Expression soundMood;

    public SongReactions(Track track, LyricLine[] words)
    {
        var recent = new List<string>(5);
        int strength, best = 0;
        foreach (var token in Fold(track.Title).Split(' '))
        {
            var mood = ReadWord(recent,token,out strength);
            if (mood != null && strength >= best) { best = strength; titleMood = mood; }
        }
        if (words == null) return;
        recent.Clear();
        double previous = -10;
        foreach (var word in words)
        {
            if (word.Text.Length == 0 || word.Seconds - previous > 3) recent.Clear();
            previous = word.Seconds;
            string normalized = Fold(word.Text);
            if (normalized.Length == 0) continue;
            foreach (var token in normalized.Split(' '))
            {
                var mood = ReadWord(recent, token, out strength);
                if (mood == null) continue;
                double at = word.Seconds + .25;
                var last = cues.Count == 0 ? null : cues[cues.Count-1];
                if (last != null && at - last.At < Spacing)
                {
                    if (at - last.At > 1.8 || strength <= last.Strength) continue;
                    cues.RemoveAt(cues.Count-1);
                }
                cues.Add(new Cue { At = at, Mood = mood, Strength = strength });
            }
            if (".!?;,".IndexOf(word.Text[word.Text.Length-1]) >= 0) recent.Clear();
        }
    }

    public void Apply(Frame frame)
    {
        if (!frame.Playing) return;
        int i = cues.Count - 1;
        while (i >= 0 && cues[i].At > frame.Time) i--;
        double start;
        Expression mood;
        if (i >= 0 && frame.Time - cues[i].At < VisibleSeconds)
        {
            start = cues[i].At;
            mood = cues[i].Mood;
        }
        else
        {
            if (frame.Time < 2) return;
            int slot = (int)((frame.Time-2) / 24);
            start = 2 + slot * 24;
            if (frame.Time-start >= VisibleSeconds || i >= 0 && Math.Abs(start-cues[i].At) < Spacing ||
                i+1 < cues.Count && Math.Abs(cues[i+1].At-start) < Spacing) return;
            if (slot != lastSlot)
            {
                if (frame.Level < .009f && titleMood == null) return;
                soundMood = frame.Level > .67f ? Dance : frame.Level < .24f ? Calm : Listening;
                lastSlot = slot;
            }
            mood = titleMood ?? soundMood;
        }
        frame.Mood = mood;
        frame.ReactionAge = frame.Time-start;
    }
}
