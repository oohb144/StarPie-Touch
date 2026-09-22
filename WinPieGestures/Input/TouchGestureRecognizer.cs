using System.Windows;

namespace WinPieGestures.Input;

internal sealed class TouchGestureConfig
{
    public int HoldMilliseconds { get; init; } = 150;
    public double MinimumFingerSeparation { get; init; } = 30;
    public double MaximumFingerSeparation { get; init; } = 250;
    public double MinimumSlideDistance { get; init; } = 40;
    public int PenCooldownMilliseconds { get; init; } = 500;
    public bool EnablePenGuard { get; init; } = true;
}

internal enum TouchGestureDirection
{
    Right, DownRight, Down, DownLeft, Left, UpLeft, Up, UpRight
}

internal sealed record TouchGestureEventArgs(TouchGestureDirection Direction, Point StartPoint,
    Point CurrentPoint, double Distance);

/// <summary>
/// Pure touch-contact recognizer. An input adapter must supply physical screen
/// pixels and a monotonic timestamp; this class never opens the wheel itself.
/// </summary>
internal sealed class TouchGestureRecognizer
{
    private readonly TouchGestureConfig _config;
    private readonly Dictionary<int, Point> _current = new();
    private readonly Dictionary<int, Point> _start = new();
    private readonly PenGuard _penGuard;
    private long _pairStartedAt;
    private bool _blockedUntilClear;
    private bool _triggered;

    public event EventHandler<TouchGestureEventArgs>? GestureDetected;

    public TouchGestureRecognizer(TouchGestureConfig? config = null)
    {
        _config = config ?? new TouchGestureConfig();
        _penGuard = new PenGuard(_config.PenCooldownMilliseconds);
    }

    public void PenDown(long now)
    {
        _penGuard.Down(now);
        ClearTouches();
    }

    public void PenUp(long now) => _penGuard.Up(now);

    public void TouchDown(int id, Point position, long now)
    {
        if (_config.EnablePenGuard && _penGuard.IsBlocking(now)) return;
        _current[id] = position;
        if (_current.Count > 2) _blockedUntilClear = true;
        if (_current.Count == 2)
        {
            _start.Clear();
            foreach (var pair in _current) _start[pair.Key] = pair.Value;
            _pairStartedAt = now;
        }
    }

    public void TouchMove(int id, Point position, long now)
    {
        if (!_current.ContainsKey(id)) return;
        if (_config.EnablePenGuard && _penGuard.IsBlocking(now))
        {
            ClearTouches();
            return;
        }
        _current[id] = position;
        if (_blockedUntilClear || _triggered || _current.Count != 2 ||
            now - _pairStartedAt < _config.HoldMilliseconds) return;

        using var ids = _current.Keys.GetEnumerator();
        ids.MoveNext(); int first = ids.Current;
        ids.MoveNext(); int second = ids.Current;
        Point a = _current[first], b = _current[second];
        Point startA = _start[first], startB = _start[second];
        double separation = (a - b).Length;
        double startSeparation = (startA - startB).Length;
        if (!WithinSeparation(separation) || !WithinSeparation(startSeparation)) return;

        Vector moveA = a - startA, moveB = b - startB;
        double minSlide = _config.MinimumSlideDistance;
        if (moveA.Length < minSlide || moveB.Length < minSlide) return;
        double cosine = Vector.Multiply(moveA, moveB) / (moveA.Length * moveB.Length);
        if (cosine < Math.Cos(Math.PI / 6)) return; // Within 30 degrees.

        Point startCenter = Midpoint(startA, startB);
        Point currentCenter = Midpoint(a, b);
        Vector slide = currentCenter - startCenter;
        _triggered = true;
        GestureDetected?.Invoke(this, new TouchGestureEventArgs(DirectionOf(slide),
            startCenter, currentCenter, slide.Length));
    }

    public void TouchUp(int id)
    {
        _current.Remove(id);
        _start.Remove(id);
        // A gesture is one attempt per continuous contact sequence. A third
        // contact or a completed gesture cannot be retriggered by the remainder.
        if (_current.Count == 0) ClearTouches();
    }

    private bool WithinSeparation(double distance) =>
        distance >= _config.MinimumFingerSeparation && distance <= _config.MaximumFingerSeparation;

    private void ClearTouches()
    {
        _current.Clear();
        _start.Clear();
        _blockedUntilClear = false;
        _triggered = false;
    }

    private static Point Midpoint(Point a, Point b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    private static TouchGestureDirection DirectionOf(Vector motion)
    {
        double sector = Math.Atan2(motion.Y, motion.X) / (Math.PI / 4);
        int index = ((int)Math.Round(sector) + 8) % 8;
        return (TouchGestureDirection)index;
    }
}

internal sealed class PenGuard
{
    private readonly int _cooldownMilliseconds;
    private bool _penDown;
    private long _blockedUntil;

    public PenGuard(int cooldownMilliseconds) => _cooldownMilliseconds = cooldownMilliseconds;

    public void Down(long now)
    {
        _penDown = true;
        _blockedUntil = now + _cooldownMilliseconds;
    }

    public void Up(long now)
    {
        _penDown = false;
        _blockedUntil = now + _cooldownMilliseconds;
    }

    public bool IsBlocking(long now) => _penDown || now < _blockedUntil;
}
