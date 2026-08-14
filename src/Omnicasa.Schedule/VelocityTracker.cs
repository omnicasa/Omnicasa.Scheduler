namespace Omnicasa.Schedule;

/// <summary>
/// Estimates the release velocity of a drag by least-squares fitting the recent samples, the way
/// Android's <c>VelocityTracker</c> does. A running average of per-sample deltas is the obvious
/// alternative, but touch samples arrive at irregular intervals, and the resulting estimate is noisy
/// enough that flings travel visibly different distances for the same gesture. A fit over a fixed
/// time horizon is insensitive to that jitter.
/// </summary>
public sealed class VelocityTracker
{
    /// <summary>Default window of gesture history the fit considers, in seconds.</summary>
    public const double DefaultHorizonSeconds = 0.1;

    /// <summary>
    /// A degree-2 fit reads acceleration, so it captures a gesture that is still speeding up as the
    /// finger lifts. It can also blow up on near-collinear samples, so it is only trusted while it
    /// stays within this multiple of the (always well-behaved) degree-1 fit.
    /// </summary>
    private const double QuadraticTrustFactor = 3.0;

    private const int Capacity = 20;

    private readonly double[] times = new double[Capacity];

    private readonly double[] positions = new double[Capacity];

    // Ring buffer: `count` valid entries ending at `head - 1`.
    private int head;

    private int count;

    /// <summary>Drops all samples; call when a new gesture starts.</summary>
    public void Clear()
    {
        head = 0;
        count = 0;
    }

    /// <summary>Records one movement sample.</summary>
    /// <param name="timeSeconds">Sample timestamp, in seconds on any monotonic clock.</param>
    /// <param name="position">Position along the tracked axis, in logical pixels.</param>
    public void Add(double timeSeconds, double position)
    {
        times[head] = timeSeconds;
        positions[head] = position;
        head = (head + 1) % Capacity;
        if (count < Capacity)
        {
            count++;
        }
    }

    /// <summary>
    /// Position at <paramref name="timeSeconds"/>, linearly interpolated between the two samples that
    /// bracket it. Touch samples and rendered frames run on unrelated clocks, so a frame that reads the
    /// newest sample consumes one sample's worth of travel on some frames and two on others — a finger
    /// moving at constant speed still lands in visibly uneven steps. Reading a fixed lag behind the
    /// newest sample instead hands every frame an equal slice, which is what makes the motion smooth.
    /// </summary>
    /// <param name="timeSeconds">Time to sample at, on the clock the samples were stamped with.</param>
    /// <returns>
    /// The interpolated position, or null when there is no history or the time asked for is newer than
    /// the newest sample — callers sample into the past precisely so they never have to extrapolate.
    /// </returns>
    public double? PositionAt(double timeSeconds)
    {
        if (count == 0)
        {
            return null;
        }

        int newest = (head - 1 + Capacity) % Capacity;
        if (timeSeconds > times[newest])
        {
            return null;
        }

        int oldest = (head - count + (Capacity * 2)) % Capacity;
        if (timeSeconds <= times[oldest])
        {
            return positions[oldest];
        }

        // Walk back from the newest sample until one sits at or before the requested time.
        for (int i = 0; i < count - 1; i++)
        {
            int later = (head - 1 - i + (Capacity * 2)) % Capacity;
            int earlier = (head - 2 - i + (Capacity * 2)) % Capacity;
            if (times[earlier] > timeSeconds)
            {
                continue;
            }

            double span = times[later] - times[earlier];
            if (span <= 0)
            {
                return positions[later];
            }

            double fraction = (timeSeconds - times[earlier]) / span;
            return positions[earlier] + ((positions[later] - positions[earlier]) * fraction);
        }

        return positions[oldest];
    }

    /// <summary>
    /// Velocity at the newest sample, in logical pixels per second. Zero when there is too little
    /// history to fit, or when the newest sample is already older than the horizon — a finger that
    /// came to rest before lifting carries no momentum, so that case must not fling.
    /// </summary>
    /// <param name="nowSeconds">Current time on the same clock the samples were stamped with.</param>
    /// <param name="horizonSeconds">Window of history to fit over.</param>
    /// <returns>The estimated velocity, or 0 when it cannot be estimated.</returns>
    public double Velocity(double nowSeconds, double horizonSeconds = DefaultHorizonSeconds)
    {
        if (count < 2 || horizonSeconds <= 0)
        {
            return 0;
        }

        double newestTime = times[(head - 1 + Capacity) % Capacity];
        if (nowSeconds - newestTime >= horizonSeconds)
        {
            return 0;
        }

        // Walk back from the newest sample, in time relative to it, until we fall off the horizon.
        Span<double> u = stackalloc double[Capacity];
        Span<double> y = stackalloc double[Capacity];
        int n = 0;
        for (int i = 0; i < count; i++)
        {
            int idx = (head - 1 - i + (Capacity * 2)) % Capacity;
            double age = newestTime - times[idx];
            if (age > horizonSeconds)
            {
                break;
            }

            u[n] = -age;
            y[n] = positions[idx];
            n++;
        }

        if (n < 2)
        {
            return 0;
        }

        double linear = FitLinear(u, y, n);
        if (n < 3)
        {
            return linear;
        }

        double quadratic = FitQuadratic(u, y, n);
        bool trusted = double.IsFinite(quadratic)
            && quadratic * linear >= 0
            && Math.Abs(quadratic) <= Math.Abs(linear) * QuadraticTrustFactor;

        return trusted ? quadratic : linear;
    }

    // Slope of the least-squares line through the samples.
    private static double FitLinear(ReadOnlySpan<double> u, ReadOnlySpan<double> y, int n)
    {
        double s0 = n, s1 = 0, s2 = 0, t0 = 0, t1 = 0;
        for (int i = 0; i < n; i++)
        {
            s1 += u[i];
            s2 += u[i] * u[i];
            t0 += y[i];
            t1 += u[i] * y[i];
        }

        double det = (s0 * s2) - (s1 * s1);
        return Math.Abs(det) < 1e-12 ? 0 : ((s0 * t1) - (s1 * t0)) / det;
    }

    // First derivative at u = 0 (the newest sample) of the least-squares parabola, i.e. its linear
    // coefficient, from the 3x3 normal equations solved by Cramer's rule.
    private static double FitQuadratic(ReadOnlySpan<double> u, ReadOnlySpan<double> y, int n)
    {
        double s0 = n, s1 = 0, s2 = 0, s3 = 0, s4 = 0, t0 = 0, t1 = 0, t2 = 0;
        for (int i = 0; i < n; i++)
        {
            double a = u[i];
            double b = a * a;
            s1 += a;
            s2 += b;
            s3 += b * a;
            s4 += b * b;
            t0 += y[i];
            t1 += a * y[i];
            t2 += b * y[i];
        }

        double det = Det3(s0, s1, s2, s1, s2, s3, s2, s3, s4);
        if (Math.Abs(det) < 1e-18)
        {
            return double.NaN;
        }

        // Replace the middle column with the right-hand side to get the linear coefficient.
        double num = Det3(s0, t0, s2, s1, t1, s3, s2, t2, s4);
        return num / det;
    }

    private static double Det3(double a, double b, double c, double d, double e, double f, double g, double h, double i)
        => (a * ((e * i) - (f * h))) - (b * ((d * i) - (f * g))) + (c * ((d * h) - (e * g)));
}
