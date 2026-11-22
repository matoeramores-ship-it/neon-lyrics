using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.CoreAudioApi;

sealed class Rhythm : IDisposable
{
    readonly ManualResetEvent stopping = new ManualResetEvent(false);
    readonly Thread worker;
    public volatile bool Active;
    public volatile float Pulse, Level;

    public Rhythm()
    {
        worker = new Thread(Sample) { IsBackground = true, Name = "Pulso de Spotify" };
        worker.Start();
    }

    void Sample()
    {
        var clock = Stopwatch.StartNew();
        var meters = new List<AudioMeterInformation>();
        var envelope = new Onsets();
        double discoverAt = 0, previous = 0;
        while (!stopping.WaitOne(Active ? 12 : 200))
        {
            double now = clock.Elapsed.TotalSeconds;
            if (!Active)
            {
                Pulse = Level = 0;
                envelope.Reset();
                previous = now;
                continue;
            }
            try
            {
                if (now >= discoverAt)
                {
                    meters.Clear();
                    using (var enumerator = new MMDeviceEnumerator())
                    foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                    using (device)
                    {
                        var sessions = device.AudioSessionManager.Sessions;
                        for (int i = 0; i < sessions.Count; i++)
                        using (var session = sessions[i])
                        {
                            if (session.AudioMeterInformation != null && session.GetSessionIdentifier.IndexOf("Spotify", StringComparison.OrdinalIgnoreCase) >= 0)
                                meters.Add(session.AudioMeterInformation);
                        }
                    }
                    discoverAt = now + (meters.Count == 0 ? 2 : 9);
                }
                float peak = 0;
                foreach (var meter in meters) peak = Math.Max(peak, meter.MasterPeakValue);
                Pulse = envelope.Update(peak, now - previous);
                Level = envelope.Level;
            }
            catch (COMException) { meters.Clear(); discoverAt = now + 1; envelope.Reset(); Pulse = Level = 0; }
            catch (UnauthorizedAccessException) { meters.Clear(); discoverAt = now + 2; envelope.Reset(); Pulse = Level = 0; }
            previous = now;
        }
    }

    public void Dispose()
    {
        stopping.Set();
        if (worker.Join(800)) stopping.Dispose();
    }
}

sealed class Onsets
{
    double average, fast, impulse, cooldown;
    public float Level { get { return (float)Math.Min(1, fast); } }

    public void Reset() { average = fast = impulse = cooldown = 0; }

    public float Update(float peak, double dt)
    {
        dt = Math.Min(.1, Math.Max(.001, dt));
        var energy = Math.Sqrt(Math.Max(0, peak));
        double previous = fast;
        fast += (energy - fast) * (1 - Math.Exp(-dt / .025));
        average += (fast - average) * (1 - Math.Exp(-dt / .24));
        cooldown -= dt;
        impulse *= Math.Exp(-dt / .115);
        double attack = (fast - average) / Math.Max(.045, average);
        if (cooldown <= 0 && fast > .035 && fast > previous && attack > .075)
        {
            impulse = Math.Max(impulse, Math.Min(1, .28 + attack * 1.65));
            cooldown = .105;
        }
        return (float)Math.Min(1, impulse + Math.Min(.18, fast * .21));
    }
}
