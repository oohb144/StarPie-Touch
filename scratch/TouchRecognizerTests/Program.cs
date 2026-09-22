using System.Windows;
using WinPieGestures.Input;

static void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    Console.WriteLine("PASS " + name);
}

static TouchGestureRecognizer New(out List<TouchGestureEventArgs> events)
{
    var captured = new List<TouchGestureEventArgs>();
    events = captured;
    var recognizer = new TouchGestureRecognizer();
    recognizer.GestureDetected += (_, e) => captured.Add(e);
    return recognizer;
}

{
    var r = New(out var events);
    r.TouchDown(1, new Point(100, 100), 0);
    r.TouchDown(2, new Point(160, 100), 10);
    r.TouchMove(1, new Point(150, 100), 100);
    r.TouchMove(2, new Point(210, 100), 100);
    Check(events.Count == 0, "hold threshold");
    r.TouchMove(1, new Point(151, 100), 160);
    Check(events.Count == 1 && events[0].Direction == TouchGestureDirection.Right,
        "two contacts move together after 150 ms");
    r.TouchMove(2, new Point(220, 100), 200);
    Check(events.Count == 1, "one event per contact sequence");
}
{
    var r = New(out var events);
    r.TouchDown(1, new Point(100, 100), 0);
    r.TouchDown(2, new Point(160, 100), 10);
    r.TouchMove(1, new Point(150, 100), 200);
    Check(events.Count == 0, "one moving finger is insufficient");
    r.TouchMove(2, new Point(110, 100), 210);
    Check(events.Count == 0, "opposite motion is rejected");
}
{
    var r = New(out var events);
    r.TouchDown(1, new Point(100, 100), 0);
    r.TouchDown(2, new Point(160, 100), 10);
    r.TouchDown(3, new Point(130, 150), 20);
    r.TouchUp(3);
    r.TouchMove(1, new Point(150, 100), 200);
    r.TouchMove(2, new Point(210, 100), 210);
    Check(events.Count == 0, "third contact blocks the sequence");
}
{
    var r = New(out var events);
    r.PenDown(0);
    r.TouchDown(1, new Point(100, 100), 100);
    r.TouchDown(2, new Point(160, 100), 110);
    r.PenUp(200);
    r.TouchDown(3, new Point(100, 100), 699);
    Check(events.Count == 0, "pen down and cooldown suppress touch");
    r.TouchDown(1, new Point(100, 100), 700);
    r.TouchDown(2, new Point(160, 100), 710);
    r.TouchMove(1, new Point(150, 100), 900);
    r.TouchMove(2, new Point(210, 100), 910);
    Check(events.Count == 1, "new contacts work after pen cooldown");
}
Console.WriteLine("Touch recognizer checks passed.");
