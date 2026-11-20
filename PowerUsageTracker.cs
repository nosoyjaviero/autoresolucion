using System.Text.Json;

namespace AutoResolution;

internal sealed class PowerUsageTracker
{
    private readonly string filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoResolution",
        "power-usage.json");
    private DateTimeOffset activeSince;
    private bool isPluggedIn;
    private int activeStartPercentage;
    private int currentPercentage;
    private DateTimeOffset lastSampleAt;
    private readonly List<PowerSession> sessions = [];

    public TimeSpan PluggedDuration { get; private set; }
    public TimeSpan BatteryDuration { get; private set; }
    public double? CurrentDrainRate => GetRate(CurrentSession);
    public PowerSession? CurrentSession { get; private set; }
    public IReadOnlyList<PowerSession> Sessions => sessions;

    public void Start(bool pluggedIn, int batteryPercentage, int processorLimit, double? batteryCapacityWh, double cpuUsage)
    {
        var state = Load();
        PluggedDuration = TimeSpan.FromTicks(state.PluggedTicks);
        BatteryDuration = TimeSpan.FromTicks(state.BatteryTicks);
        sessions.AddRange(state.Sessions ?? []);
        isPluggedIn = state.HasActiveState ? state.IsPluggedIn : pluggedIn;
        activeSince = state.HasActiveState ? state.ActiveSince : DateTimeOffset.Now;
        activeStartPercentage = state.HasActiveState ? state.ActiveStartPercentage : batteryPercentage;
        currentPercentage = state.HasActiveState ? state.CurrentPercentage : batteryPercentage;
        lastSampleAt = state.HasActiveState ? state.LastSampleAt : DateTimeOffset.Now;
        CurrentSession = state.CurrentSession ?? CreateSession(isPluggedIn, activeSince, activeStartPercentage, processorLimit, batteryCapacityWh);
        Update(pluggedIn, batteryPercentage, processorLimit, batteryCapacityWh, cpuUsage);
    }

    public void Update(bool pluggedIn, int batteryPercentage, int processorLimit, double? batteryCapacityWh, double cpuUsage)
    {
        var now = DateTimeOffset.Now;
        if (isPluggedIn == pluggedIn)
        {
            AddPercentageSample(now, batteryPercentage, cpuUsage);
            if (CurrentSession is not null)
            {
                CurrentSession.EndedAt = now;
                CurrentSession.EndPercentage = batteryPercentage;
            }
            Save();
            return;
        }

        AddPercentageSample(now, batteryPercentage, cpuUsage);
        AddElapsed(now);
        CompleteCurrentSession(now, batteryPercentage);
        isPluggedIn = pluggedIn;
        activeSince = now;
        activeStartPercentage = batteryPercentage;
        currentPercentage = batteryPercentage;
        lastSampleAt = now;
        CurrentSession = CreateSession(pluggedIn, now, batteryPercentage, processorLimit, batteryCapacityWh);
        Save();
    }

    public void SaveSnapshot(int batteryPercentage)
    {
        AddPercentageSample(DateTimeOffset.Now, batteryPercentage, CpuUsageMonitor.GetPercent());
        AddElapsed(DateTimeOffset.Now);
        activeSince = DateTimeOffset.Now;
        if (CurrentSession is not null)
        {
            CurrentSession.EndedAt = activeSince;
            CurrentSession.EndPercentage = batteryPercentage;
        }
        Save();
    }

    public PowerSession? GetGraphSession() => CurrentSession ?? sessions.FirstOrDefault();

    public (TimeSpan plugged, TimeSpan battery) GetCurrentTotals()
    {
        var plugged = PluggedDuration;
        var battery = BatteryDuration;
        var active = DateTimeOffset.Now - activeSince;
        if (isPluggedIn) plugged += active;
        else battery += active;
        return (plugged, battery);
    }

    public double? GetAverageBatteryDrainRate(int processorLimit)
    {
        var rates = sessions
            .Where(session =>
                !session.IsCharging &&
                session.ProcessorLimit == processorLimit &&
                session.Duration.TotalHours >= 0.1 &&
                session.EndPercentage < session.StartPercentage)
            .Select(session => (session.StartPercentage - session.EndPercentage) / session.Duration.TotalHours)
            .ToList();

        return rates.Count == 0 ? null : rates.Average();
    }

    private static double? GetRate(PowerSession? session)
    {
        if (session is null || session.IsCharging || session.Duration.TotalHours < 0.1 ||
            session.EndPercentage >= session.StartPercentage)
        {
            return null;
        }

        return (session.StartPercentage - session.EndPercentage) / session.Duration.TotalHours;
    }

    private PowerSession CreateSession(bool pluggedIn, DateTimeOffset startedAt, int percentage, int processorLimit, double? batteryCapacityWh) =>
        new()
        {
            IsCharging = pluggedIn,
            StartedAt = startedAt,
            StartPercentage = percentage,
            ProcessorLimit = processorLimit,
            BatteryCapacityWh = batteryCapacityWh,
            EndedAt = startedAt,
            EndPercentage = percentage
        };

    private void CompleteCurrentSession(DateTimeOffset endedAt, int percentage)
    {
        if (CurrentSession is null)
        {
            return;
        }

        CurrentSession.EndedAt = endedAt;
        CurrentSession.EndPercentage = percentage;
        sessions.Insert(0, CurrentSession);
        if (sessions.Count > 100)
        {
            sessions.RemoveAt(sessions.Count - 1);
        }
    }

    private void AddElapsed(DateTimeOffset now)
    {
        var elapsed = now - activeSince;
        if (elapsed <= TimeSpan.Zero || elapsed > TimeSpan.FromDays(30))
        {
            return;
        }

        if (isPluggedIn) PluggedDuration += elapsed;
        else BatteryDuration += elapsed;
    }

    private void AddPercentageSample(DateTimeOffset now, int batteryPercentage, double cpuUsage)
    {
        if (CurrentSession is null)
        {
            return;
        }

        var elapsed = now - lastSampleAt;
        if (elapsed > TimeSpan.Zero && elapsed <= TimeSpan.FromMinutes(10))
        {
            var key = Math.Clamp(currentPercentage, 0, 100).ToString();
            CurrentSession.PercentageTicks.TryGetValue(key, out var ticks);
            CurrentSession.PercentageTicks[key] = ticks + elapsed.Ticks;
            CurrentSession.CpuPercentSeconds += cpuUsage * elapsed.TotalSeconds;
            CurrentSession.CpuSeconds += elapsed.TotalSeconds;
        }

        currentPercentage = Math.Clamp(batteryPercentage, 0, 100);
        lastSampleAt = now;
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        JsonFileStore.Save(filePath, new StoredState
        {
            PluggedTicks = PluggedDuration.Ticks,
            BatteryTicks = BatteryDuration.Ticks,
            HasActiveState = true,
            IsPluggedIn = isPluggedIn,
            ActiveSince = activeSince,
            ActiveStartPercentage = activeStartPercentage,
            CurrentPercentage = currentPercentage,
            LastSampleAt = lastSampleAt,
            CurrentSession = CurrentSession,
            Sessions = sessions
        });
    }

    private StoredState Load()
    {
        return JsonFileStore.Load<StoredState>(filePath) ?? new StoredState();
    }

    private sealed class StoredState
    {
        public long PluggedTicks { get; set; }
        public long BatteryTicks { get; set; }
        public bool HasActiveState { get; set; }
        public bool IsPluggedIn { get; set; }
        public DateTimeOffset ActiveSince { get; set; }
        public int ActiveStartPercentage { get; set; }
        public int CurrentPercentage { get; set; }
        public DateTimeOffset LastSampleAt { get; set; }
        public PowerSession? CurrentSession { get; set; }
        public List<PowerSession>? Sessions { get; set; }
    }
}

internal sealed class PowerSession
{
    public bool IsCharging { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public int StartPercentage { get; set; }
    public int ProcessorLimit { get; set; }
    public double? BatteryCapacityWh { get; set; }
    public double CpuPercentSeconds { get; set; }
    public double CpuSeconds { get; set; }
    public double AverageCpuPercent => CpuSeconds <= 0 ? 0 : CpuPercentSeconds / CpuSeconds;
    public Dictionary<string, long> PercentageTicks { get; set; } = [];
    public DateTimeOffset EndedAt { get; set; }
    public int EndPercentage { get; set; }
    public TimeSpan Duration => EndedAt - StartedAt;
}
