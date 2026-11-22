using System;
using System.Drawing;
using System.Drawing.Drawing2D;

static class WordMotion
{
    public static void Append(GraphicsPath word, GraphicsPath symbol, string name, double age, bool ringing)
    {
        var text = word.GetBounds();
        var icon = symbol.GetBounds();
        if (text.Height <= 0 || icon.Width <= 0 || icon.Height <= 0) return;
        bool moving = age >= 0;
        float x = 0, y = 0, angle = 0, sx = 1, sy = 1, bell = 0;
        if (moving)
        {
            float sway = (float)Math.Sin(age * 7);
            float hop = (float)Math.Abs(Math.Sin(age * 4.6));
            switch (name)
            {
                case "📞":
                    double phase = age % 1.15;
                    bell = phase < .24 || phase >= .43 && phase < .70 ? 1 : 0;
                    float shake = (float)Math.Sin(age * 69) * bell;
                    angle = shake * (ringing ? 22 : 13); x = shake * 2; y = -bell * 1.8f;
                    break;
                case "heart": case "💔": case "💋": case "🧲":
                    float beat = (float)Math.Pow(Math.Abs(Math.Sin(age * 4.2)), 5);
                    sx = 1 + beat * .23f; sy = 1 + beat * .16f; break;
                case "🦋": case "🕊": case "🍃":
                    sx = .53f + .47f * (float)Math.Abs(Math.Cos(age * 10)); y = -hop * 3; angle = sway * 9; break;
                case "drop": case "❄": case "🧊":
                    double fall = age % .85 / .85; y = (float)(8 * fall * fall - 3);
                    x = name == "drop" ? 0 : sway * 2; sy = 1 + .13f * (float)fall; break;
                case "🚗": case "🚂": case "🛣": case "👣": case "🏃": case "🧳":
                    x = sway * 4; y = -hop * 2; angle = sway * 4; break;
                case "✈": case "🪐": case "👻": case "☁": case "🌬":
                    x = (float)Math.Sin(age * 4) * 4; y = sway * -3; angle = sway * -8; break;
                case "🌲": case "flower": case "🌈": case "⚓": case "⛵": case "wave":
                    angle = sway * 12; y = -hop * 1.5f; break;
                case "🎸": case "🎹": case "🥁": case "🎤": case "🎧": case "note": case "🎉": case "🪩":
                    y = -hop * 4.5f; angle = sway * 14; sy = 1 - .12f * (1-hop); break;
                case "eye": case "🪞": case "📷":
                    sy = 1 - .69f * (float)Math.Pow(Math.Max(0,Math.Cos((age-.28)*5)), 14); break;
                case "flame": case "bolt": case "🕯": case "star": case "💎":
                    sx = 1 - .12f * sway; sy = 1 + .19f * sway; angle = sway * 6; break;
                case "💥":
                    float burst = (float)Math.Exp(-5*(age % .58));
                    sx = sy = .80f + .56f * burst; angle = sway * 11; break;
                case "🔔":
                    angle = 25*(float)Math.Sin(age*17); break;
                case "🐱": case "🐶":
                    y = -hop*3; angle = sway*9; sy = 1+.12f*hop; break;
                case "hands": case "🔑": case "🔒": case "👑": case "😈": case "🎭":
                    angle = sway * 18; break;
                case "🧭": case "clock": case "sun": case "🎲": case "⏳":
                    angle = (float)Math.Sin(age*3) * 31; break;
                case "🚪": case "📖": case "✉":
                    sx = .72f + .28f * (float)Math.Cos(age*6); x = (1-sx)*5; break;
                default:
                    angle = sway * 7; y = -hop * 2.7f; break;
            }
        }
        float side = Math.Min(78,text.Height * (moving ? .78f : .63f));
        using (var shape = (GraphicsPath)symbol.Clone())
        using (var fit = new Matrix())
        using (var pose = new Matrix())
        {
            float units = 24 / Math.Max(icon.Width,icon.Height);
            fit.Scale(units, units);
            fit.Translate(-icon.X-icon.Width/2, -icon.Y-icon.Height/2);
            shape.Transform(fit);
            if (name == "📞" && bell > 0)
            using (var lines = new GraphicsPath())
            using (var stroke = new Pen(Color.Black,1.45f) { StartCap=LineCap.Round,EndCap=LineCap.Round })
            {
                lines.AddArc(-20,-11,8,16,118,115);
                lines.StartFigure(); lines.AddArc(12,-11,8,16,-53,110);
                if (ringing)
                {
                    lines.StartFigure(); lines.AddArc(-25,-13,10,21,120,112);
                    lines.StartFigure(); lines.AddArc(15,-13,10,21,-54,112);
                }
                lines.Widen(stroke); shape.AddPath(lines,false);
            }
            pose.Translate(text.Right + text.Height*.18f + side/2, text.Y+text.Height/2);
            pose.Scale(side/24,side/24);
            pose.Translate(x,y); pose.Rotate(angle); pose.Scale(sx,sy);
            shape.Transform(pose);
            float shift = text.Right + text.Height*.11f - shape.GetBounds().Left;
            if (shift > 0)
            {
                pose.Reset(); pose.Translate(shift,0);
                shape.Transform(pose);
            }
            word.AddPath(shape,false);
        }
    }
}
