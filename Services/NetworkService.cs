using System.Net.NetworkInformation;

namespace IndoTweaks.Services;

public sealed record PingSample(double Ms, bool TimedOut);

/// <summary>
/// Samples ping to a small set of well-known low-latency endpoints to approximate
/// "distance to nearby servers" without needing Epic's actual matchmaking endpoints
/// (which aren't public). Also computes jitter (stddev of recent samples) which is
/// often a better predictor of in-game rubberbanding than raw ping.
/// </summary>
public sealed class NetworkService
{
    // Cloudflare/Google anycast addresses resolve to the nearest edge node,
    // which is a reasonable proxy for "your general internet path quality."
    private static readonly string[] ProbeHosts = { "1.1.1.1", "8.8.8.8" };

    private readonly Queue<double> _recentPings = new();
    private const int JitterWindow = 20;

    public async Task<(double avgPingMs, double jitterMs, double lossPercent)> SampleAsync(CancellationToken ct = default)
    {
        var samples = new List<PingSample>();

        foreach (var host in ProbeHosts)
        {
            using var ping = new Ping();
            try
            {
                var reply = await ping.SendPingAsync(host, 1000);
                samples.Add(reply.Status == IPStatus.Success
                    ? new PingSample(reply.RoundtripTime, false)
                    : new PingSample(0, true));
            }
            catch
            {
                samples.Add(new PingSample(0, true));
            }
        }

        var successful = samples.Where(s => !s.TimedOut).Select(s => s.Ms).ToList();
        double avg = successful.Count > 0 ? successful.Average() : -1;
        double lossPercent = 100.0 * samples.Count(s => s.TimedOut) / samples.Count;

        if (avg >= 0)
        {
            _recentPings.Enqueue(avg);
            while (_recentPings.Count > JitterWindow) _recentPings.Dequeue();
        }

        double jitter = ComputeJitter();
        return (avg, jitter, lossPercent);
    }

    private double ComputeJitter()
    {
        if (_recentPings.Count < 2) return 0;
        var arr = _recentPings.ToArray();
        double mean = arr.Average();
        double variance = arr.Select(x => Math.Pow(x - mean, 2)).Average();
        return Math.Sqrt(variance);
    }
}
