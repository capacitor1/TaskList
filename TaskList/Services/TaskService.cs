using TaskList.Data;
using TaskList.Models;

namespace TaskList.Services;

/// <summary>
/// All task rules live here: trigger windows, visibility, ordering, completion and archiving.
/// Kept free of UI code so the behaviour is easy to reason about and test.
/// </summary>
public sealed class TaskService
{
    const int OccurrenceSearchDays = 400;

    readonly TaskDatabase _db;
    public AppSettings Settings { get; }
    public List<TaskItem> All { get; private set; } = [];

    public TaskService(TaskDatabase db, AppSettings settings)
    {
        _db = db;
        Settings = settings;
        Reload();
    }

    public void Reload() => All = _db.GetAll();

    public void Insert(TaskItem task)
    {
        _db.Insert(task);
        All.Add(task);
    }

    public void Update(TaskItem task)
    {
        task.UpdatedAt = UtcTime.Now;
        _db.Update(task);
        var index = All.FindIndex(t => t.Id == task.Id);
        if (index >= 0) All[index] = task;
        else All.Add(task);
    }

    public void Delete(TaskItem task)
    {
        _db.Delete(task.Id);
        All.RemoveAll(t => t.Id == task.Id);
    }

    /// <summary>Visible window of the current occurrence (or of the whole task for one-off tasks).</summary>
    public (DateTime Start, DateTime End)? CurrentWindow(TaskItem t, DateTime now)
    {
        if (!t.IsRepeating)
        {
            var start = t.StartAt ?? t.CreatedAt;
            return (start, t.DueAt ?? DateTime.MaxValue);
        }

        var localNow = UtcTime.ToLocal(now);
        var startLocal = LatestOccurrenceAtOrBefore(t, localNow);
        if (!startLocal.HasValue) return null;

        var startUtc = UtcTime.FromLocal(startLocal.Value);
        var endUtc = NextOccurrence(t, startUtc);
        return BuildWindow(t, startUtc, endUtc);
    }

    /// <summary>First occurrence window that starts at or after <paramref name="from"/>.</summary>
    public (DateTime Start, DateTime End)? NextWindow(TaskItem t, DateTime from)
    {
        if (!t.IsRepeating)
        {
            var start = t.StartAt ?? t.CreatedAt;
            return start >= from ? BuildWindow(t, start, null) : null;
        }

        var startLocal = FirstOccurrenceAtOrAfter(t, UtcTime.ToLocal(from));
        if (!startLocal.HasValue) return null;

        var startUtc = UtcTime.FromLocal(startLocal.Value);
        return BuildWindow(t, startUtc, NextOccurrence(t, startUtc));
    }

    static (DateTime Start, DateTime End)? BuildWindow(TaskItem t, DateTime startUtc, DateTime? endUtc)
    {
        if (t.DueAt.HasValue && startUtc >= t.DueAt.Value) return null;
        var end = endUtc ?? DateTime.MaxValue;
        if (t.DueAt.HasValue && t.DueAt.Value < end) end = t.DueAt.Value;
        return end <= startUtc ? null : (startUtc, end);
    }

    /// <summary>Stable key for the current occurrence, used for change detection and notifications.</summary>
    public long OccurrenceKey(TaskItem t, DateTime now) =>
        t.IsRepeating ? CurrentWindow(t, now)?.Start.Ticks ?? long.MinValue : 0;

    public DateTime? EffectiveDeadline(TaskItem t, DateTime now)
    {
        if (t.IsRepeating) return CurrentWindow(t, now)?.End;
        return t.DueAt;
    }

    public bool IsExpired(TaskItem t, DateTime now) =>
        !t.Archived && t.DueAt.HasValue && now > t.DueAt.Value;

    public bool IsOnHome(TaskItem t, DateTime now)
    {
        if (t.Archived) return false;
        if (t.DueAt.HasValue && now > t.DueAt.Value) return false;

        if (t.IsRepeating)
        {
            if (t.StartAt.HasValue && now < t.StartAt.Value) return false;
            var window = CurrentWindow(t, now);
            if (!window.HasValue) return false;
            return !t.LastCompletedAt.HasValue || window.Value.Start > t.LastCompletedAt.Value;
        }

        return !t.StartAt.HasValue || now >= t.StartAt.Value;
    }

    public bool IsCritical(TaskItem t, DateTime now)
    {
        var deadline = EffectiveDeadline(t, now);
        if (!deadline.HasValue || deadline.Value <= now) return false;

        DateTime periodStart;
        if (t.IsRepeating)
        {
            var window = CurrentWindow(t, now);
            if (!window.HasValue) return false;
            periodStart = window.Value.Start;
        }
        else
        {
            periodStart = t.StartAt ?? t.CreatedAt;
            if (periodStart > now) return false;
        }

        var total = deadline.Value - periodStart;
        if (total <= TimeSpan.Zero) return true;
        var ratio = (deadline.Value - now).TotalSeconds / total.TotalSeconds;
        return ratio < Settings.CriticalPercent / 100.0;
    }

    /// <summary>Combined importance + urgency score; larger means closer to the top.</summary>
    public double Score(TaskItem t, DateTime now)
    {
        var importance = (10 - Math.Clamp(t.Importance, 0, 10)) / 10.0;
        var urgency = 0.0;
        var deadline = EffectiveDeadline(t, now);
        if (deadline.HasValue)
        {
            // Urgency decays over roughly six hours: near deadlines dominate,
            // while long-horizon tasks are separated mostly by importance.
            var hours = Math.Max(0, (deadline.Value - now).TotalHours);
            urgency = 1.0 / (1.0 + hours / 6.0);
        }
        var criticalBoost = IsCritical(t, now) ? 0.5 : 0.0;
        return importance * 0.9 + urgency * 1.6 + criticalBoost;
    }

    public List<TaskItem> HomeTasks(DateTime now) =>
        All.Where(t => IsOnHome(t, now))
            .OrderByDescending(t => Score(t, now))
            .ThenBy(t => EffectiveDeadline(t, now) ?? DateTime.MaxValue)
            .ThenBy(t => t.Importance)
            .ThenBy(t => t.Id)
            .ToList();

    /// <summary>Marks the current occurrence (or the whole one-off task) complete.</summary>
    public bool Complete(TaskItem t, DateTime now)
    {
        if (t.Archived) return false;

        if (t.IsRepeating)
        {
            if (t.StartAt.HasValue && now < t.StartAt.Value) return false;
            var window = CurrentWindow(t, now);
            if (!window.HasValue) return false;
            if (t.LastCompletedAt.HasValue && window.Value.Start <= t.LastCompletedAt.Value)
                return false;

            t.LastCompletedAt = window.Value.Start;
            t.CompletedCount++;
            t.CompletedAt = now;
            t.UpdatedAt = now;
            if (t.DueAt.HasValue && now > t.DueAt.Value)
                Archive(t, now, "expired");
            _db.Update(t);
            return true;
        }

        t.CompletedCount = Math.Max(1, t.CompletedCount + 1);
        t.CompletedAt = now;
        Archive(t, now, "done");
        _db.Update(t);
        return true;
    }

    /// <summary>Archives every task whose overall deadline has passed. Returns how many changed.</summary>
    public int ArchiveExpired(DateTime now)
    {
        var count = 0;
        foreach (var t in All)
        {
            if (!IsExpired(t, now)) continue;
            Archive(t, now, "expired");
            _db.Update(t);
            count++;
        }
        return count;
    }

    static void Archive(TaskItem t, DateTime now, string reason)
    {
        t.Archived = true;
        t.ArchiveReason = reason;
        t.ArchivedAt = now;
        t.UpdatedAt = now;
    }

    public string StatusText(TaskItem t, DateTime now)
    {
        if (t.Archived)
            return t.ArchiveReason == "done" ? "已完成" : "已过期";

        if (t.IsRepeating)
        {
            if (t.StartAt.HasValue && now < t.StartAt.Value) return "未开始";
            var window = CurrentWindow(t, now);
            if (!window.HasValue) return "未开始";
            return t.LastCompletedAt.HasValue && window.Value.Start <= t.LastCompletedAt.Value
                ? "本次已完成"
                : "进行中";
        }

        return t.StartAt.HasValue && now < t.StartAt.Value ? "未开始" : "进行中";
    }

    public static string FormatDuration(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        if (span.TotalDays >= 1)
            return $"{(int)span.TotalDays}天 {span.Hours:00}:{span.Minutes:00}:{span.Seconds:00}";
        return $"{span.Hours:00}:{span.Minutes:00}:{span.Seconds:00}";
    }

    // ---- Trigger occurrence math -------------------------------------------------

    static bool IsDaySelected(TaskItem t, DateTime localDate) => t.Repeat switch
    {
        RepeatKind.Daily => true,
        RepeatKind.Weekly => (t.WeekDays & (1 << ((int)localDate.DayOfWeek + 6) % 7)) != 0,
        RepeatKind.Monthly => (t.MonthDays & (1 << localDate.Day)) != 0,
        _ => false,
    };

    static DateTime? LatestOccurrenceAtOrBefore(TaskItem t, DateTime localNow)
    {
        var date = localNow.Date;
        var triggers = t.EffectiveTriggers;
        for (var i = 0; i < OccurrenceSearchDays; i++, date = date.AddDays(-1))
        {
            if (!IsDaySelected(t, date)) continue;
            var maxSeconds = date == localNow.Date
                ? localNow.TimeOfDay.TotalSeconds
                : double.MaxValue;
            for (var j = triggers.Count - 1; j >= 0; j--)
                if (triggers[j] <= maxSeconds)
                    return date.AddSeconds(triggers[j]);
        }
        return null;
    }

    static DateTime? FirstOccurrenceAtOrAfter(TaskItem t, DateTime localFrom)
    {
        var date = localFrom.Date;
        var triggers = t.EffectiveTriggers;
        for (var i = 0; i < OccurrenceSearchDays; i++, date = date.AddDays(1))
        {
            if (!IsDaySelected(t, date)) continue;
            foreach (var seconds in triggers)
            {
                var candidate = date.AddSeconds(seconds);
                if (candidate >= localFrom) return candidate;
            }
        }
        return null;
    }

    static DateTime? NextOccurrence(TaskItem t, DateTime startUtc)
    {
        var nextLocal = FirstOccurrenceAtOrAfter(t, UtcTime.ToLocal(startUtc).AddSeconds(1));
        return nextLocal.HasValue ? UtcTime.FromLocal(nextLocal.Value) : null;
    }
}
