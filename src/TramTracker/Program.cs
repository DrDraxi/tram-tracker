using TaskbarWidget;

namespace TramTracker;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        new TramApp().Start();
        Widget.Run();
    }
}
