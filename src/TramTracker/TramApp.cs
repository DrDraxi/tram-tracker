using TaskbarWidget;

namespace TramTracker;

/// <summary>
/// Taskbar layout:  [ 17 ]  4 min +2
///                          ●━━●━━◉━○
/// Line badge, live countdown (recomputed every few seconds from the departure time, not just on
/// fetch), delay, and a mini route track whose vehicle dot approaches your stop.
/// </summary>
public sealed class TramApp
{
    const string AppName = "TramTracker";
    static readonly Color BadgeYellow = Color.Hex("#FFD233");
    static readonly Color BadgeText = Color.Hex("#1A1A1A");
    static readonly TextStyle BadgeStyle = new(12, 700);
    static readonly TextStyle TimeStyle = new(13, 600);
    static readonly TextStyle DelayStyle = new(11, 600);

    readonly TramConfig _config = new();
    Widget _widget = null!;
    TramState _state = null!;
    string? _lastError;   // last fetch error while still showing older (valid) data
    int _fetching;
    int _refreshTimer, _refreshSeconds;

    public void Start()
    {
        AppKit.SetStartWithWindows(AppName, _config.StartWithWindows);
        _state = TramState.Loading(_config.Active(DateTime.Now));

        _widget = new Widget("Tram", Build, new WidgetOptions { Menu = BuildMenu });
        _widget.Show();

        _refreshSeconds = _config.RefreshSeconds;
        _refreshTimer = _widget.SetInterval(TimeSpan.FromSeconds(_refreshSeconds), Refresh);
        _widget.SetInterval(TimeSpan.FromSeconds(5), Tick);
        _widget.Resumed += Refresh;
        Refresh();
    }

    // ── Data ────────────────────────────────────────────────────────────

    void Refresh()
    {
        if (Interlocked.Exchange(ref _fetching, 1) == 1) return;

        _config.Reload(); // hot-reload: edits to config.json apply on the next fetch
        if (_config.RefreshSeconds != _refreshSeconds)
        {
            _widget.ClearTimer(_refreshTimer);
            _refreshSeconds = _config.RefreshSeconds;
            _refreshTimer = _widget.SetInterval(TimeSpan.FromSeconds(_refreshSeconds), Refresh);
        }

        var tracking = _config.Active(DateTime.Now);
        var key = _config.ApiKey;
        int limit = _config.DepartureLimit;
        var configError = _config.LoadError;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            var result = configError != null
                ? new TramState(tracking, Array.Empty<Departure>(), null, configError)
                : Golemio.Fetch(key, tracking, limit);

            _widget.Post(() =>
            {
                _fetching = 0;
                bool transientFailure = result.Error != null && result.Updated == null;
                bool haveGoodData = _state.Error == null && _state.Tracking == result.Tracking
                    && _state.Remaining(DateTimeOffset.Now).Any();
                if (transientFailure && haveGoodData)
                {
                    _lastError = result.Error; // keep counting down on the data we have
                }
                else
                {
                    _state = result;
                    _lastError = null;
                }
                _widget.Invalidate();
            });
        });
    }

    void Tick()
    {
        // The tracked tram left and we have nothing queued behind it: fetch now instead of waiting.
        if (_state.Upcoming.Count > 0 && !_state.Remaining(DateTimeOffset.Now).Any()) Refresh();
        _widget.Invalidate();
    }

    // ── UI ──────────────────────────────────────────────────────────────

    void Build(Ui ui)
    {
        var theme = ui.Theme;
        var now = DateTimeOffset.Now;
        var remaining = _state.Remaining(now).ToList();
        var dep = remaining.FirstOrDefault();
        bool hasData = dep != null;
        var text = _config.TextColor switch { "white" => Color.White, "black" => Color.Black, _ => theme.Text };

        double minutes = hasData ? Math.Max(0, (dep!.Time - now).TotalMinutes) : 0;
        string timeText = hasData
            ? (minutes < 1 ? "now" : $"{(int)minutes} min")
            : _state.Updated == null && _state.Error == null ? "···" : "—";
        Color status = !hasData ? theme.TextTertiary
            : dep!.DelayMinutes is null or <= 1 ? theme.Success
            : dep.DelayMinutes <= 3 ? theme.Caution : theme.Critical;
        float progress = hasData ? Progress(minutes) : 0;

        // Line badge
        string line = dep?.Line ?? _state.Tracking.Line ?? "–";
        var (bw, bh) = ui.MeasureText(line, BadgeStyle);
        float badgeW = MathF.Max(22, MathF.Ceiling(bw) + 10), badgeH = 20;
        ui.Custom(badgeW, badgeH, (g, _) =>
        {
            g.FillRoundRect(0, 0, badgeW, badgeH, 5, hasData ? BadgeYellow : theme.TextTertiary.WithOpacity(0.55f));
            g.Text(line, (badgeW - bw) / 2, (badgeH - bh) / 2, BadgeStyle, hasData ? BadgeText : text);
        });

        // Countdown + delay, with the route track underneath
        string? delayText = hasData && dep!.DelayMinutes > 0 ? $"+{dep.DelayMinutes}" : null;
        var (tw, th) = ui.MeasureText(timeText, TimeStyle);
        var (dw, dh) = delayText != null ? ui.MeasureText(delayText, DelayStyle) : (0f, 0f);
        // Minimum width fits "10 min +1", so the widget (and its neighbours) don't shift every minute.
        float colW = MathF.Ceiling(MathF.Max(58, tw + (delayText != null ? dw + 4 : 0)));
        float trackY = MathF.Round(th + 2);
        ui.Custom(colW, trackY + 8, (g, _) =>
        {
            g.Text(timeText, 0, 0, TimeStyle, hasData ? text : theme.TextSecondary);
            if (delayText != null) g.Text(delayText, tw + 4, (th - dh) * 0.78f, DelayStyle, status);
            DrawTrack(g, trackY, colW, progress, status, theme, hasData);
        });

        var (title, body) = Tooltip(dep, remaining.Skip(1).FirstOrDefault(), minutes, now);
        ui.Tooltip(title, body);
        ui.OnClick(Refresh);
    }

    /// <summary>0 = far away (≥ 6 min), 0.5 = 3 min, 1 = at your stop. Continuous, so the dot glides.</summary>
    static float Progress(double m) => (float)(m <= 1 ? 1 : m <= 3 ? 1 - (m - 1) * 0.25 : m <= 6 ? 0.5 - (m - 3) / 6 : 0);

    static void DrawTrack(Graphics g, float y, float w, float pos, Color status, Theme t, bool active)
    {
        float cy = y + 4, x0 = 3.5f, x1 = w - 4;
        var rail = t.TextTertiary.WithOpacity(0.75f);
        g.Line(x0, cy, x1, cy, rail, 2);

        float vx = x0 + (x1 - x0) * pos;
        if (active && vx > x0) g.Line(x0, cy, vx, cy, status, 2);

        // Intermediate stops
        for (int i = 0; i < 2; i++)
        {
            float sx = x0 + (x1 - x0) * i * 0.5f;
            bool passed = active && pos >= i * 0.5f + 0.01f;
            g.FillCircle(sx, cy, 2.1f, passed ? status : Color.Lerp(rail, t.Text, 0.25f));
        }

        // Your stop: a ring that fills in when the tram arrives
        g.StrokeCircle(x1, cy, 2.9f, active ? t.Text : rail, 1.4f);
        if (active && pos >= 0.999f) g.FillCircle(x1, cy, 2.2f, status);

        // The vehicle, with a soft glow
        if (active)
        {
            g.FillCircle(vx, cy, 5.6f, status.WithOpacity(0.22f));
            g.FillCircle(vx, cy, 3.4f, status);
        }
    }

    (string Title, string Body) Tooltip(Departure? dep, Departure? next, double minutes, DateTimeOffset now)
    {
        var t = _state.Tracking;
        var lines = new List<string>();

        if (dep == null)
        {
            lines.Add($"Stop: {t.Station}" + (t.Line != null ? $" · line {t.Line}" : "") + (t.Direction != null ? $" → {t.Direction}" : ""));
            lines.Add(_state.Error ?? (_state.Updated == null ? "Loading departures…" : "No upcoming departures"));
        }
        else
        {
            lines.Add($"From {t.Station}" + (dep.Platform != null ? $" · platform {dep.Platform}" : ""));
            var at = dep.Time.ToLocalTime();
            lines.Add(minutes < 1 ? $"Departing now ({at:HH:mm})" : $"Departs in {(int)minutes} min, at {at:HH:mm}");
            lines.Add(dep.DelayMinutes switch
            {
                null => "No live position (scheduled time)",
                <= 0 => "On time",
                1 => "Delayed 1 minute",
                var d => $"Delayed {d} minutes",
            });
            if (next != null)
            {
                var nm = Math.Max(0, (int)(next.Time - now).TotalMinutes);
                lines.Add($"Then in {nm} min, at {next.Time.ToLocalTime():HH:mm}");
            }
        }

        if (_lastError != null) lines.Add($"Last update failed: {_lastError}");
        if (_state.Updated is { } u) lines.Add($"Updated {u:HH:mm:ss}");
        lines.Add("Click to refresh · right-click for options");

        string title = dep != null ? $"{dep.Line} → {dep.Headsign}" : "Tram Tracker";
        return (title, string.Join("\n", lines));
    }

    void BuildMenu(MenuBuilder m)
    {
        m.Item("Refresh now", Refresh)
         .Item("Edit settings…", () => AppKit.Open(_config.FilePath))
         .Separator()
         .Check("Start with Windows", _config.StartWithWindows, () =>
         {
             bool enabled = !_config.StartWithWindows;
             _config.SetStartWithWindows(enabled);
             AppKit.SetStartWithWindows(AppName, enabled);
         });
    }
}
