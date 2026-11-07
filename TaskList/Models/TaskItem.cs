namespace TaskList.Models;

using TaskList.Data;

public enum RepeatKind
{
    Once = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
}

/// <summary>A single persistent task record. All DateTimes are UTC.</summary>
public sealed class TaskItem
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public int Importance { get; set; } = 5;

    public RepeatKind Repeat { get; set; } = RepeatKind.Once;

    /// <summary>Daily trigger checkpoints as seconds after local midnight, sorted and unique.</summary>
    public List<int> TriggerSeconds { get; set; } = [];

    /// <summary>Weekly execution days. Bit 0 is Monday, bit 6 is Sunday.</summary>
    public int WeekDays { get; set; } = 0b1111111;

    /// <summary>Monthly execution days. Bit 1 is day 1 ... bit 31 is day 31.</summary>
    public int MonthDays { get; set; } = DefaultMonthDays;

    public const int DefaultWeekDays = 0b1111111;
    public const int DefaultMonthDays = (1 << 29) - 2; // days 1-28
    public const int DefaultTrigger = 9 * 3600;
    public const int DefaultSecondTrigger = 18 * 3600;

    public DateTime? StartAt { get; set; }
    public DateTime? DueAt { get; set; }

    /// <summary>Total completed occurrences (or 1 for a finished one-off task).</summary>
    public int CompletedCount { get; set; }

    /// <summary>Start of the last completed occurrence; later occurrences become visible again.</summary>
    public DateTime? LastCompletedAt { get; set; }

    public bool Archived { get; set; }
    public string? ArchiveReason { get; set; }

    public DateTime CreatedAt { get; set; } = UtcTime.Now;
    public DateTime UpdatedAt { get; set; } = UtcTime.Now;
    public DateTime? ArchivedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public bool IsRepeating => Repeat != RepeatKind.Once;

    public string RepeatLabel => Repeat switch
    {
        RepeatKind.Daily => "每天",
        RepeatKind.Weekly => "每周",
        RepeatKind.Monthly => "每月",
        _ => "单次",
    };

    /// <summary>Sorted, de-duplicated trigger list; falls back to a single morning checkpoint.</summary>
    public IReadOnlyList<int> EffectiveTriggers
    {
        get
        {
            var values = TriggerSeconds
                .Where(s => s is >= 0 and < 86400)
                .Distinct()
                .OrderBy(s => s)
                .ToList();
            return values.Count > 0 ? values : [DefaultTrigger];
        }
    }

    public string TriggerTimesText =>
        string.Join("、", EffectiveTriggers.Select(s => TimeSpan.FromSeconds(s).ToString(@"hh\:mm\:ss")));

    public TaskItem Clone()
    {
        var copy = (TaskItem)MemberwiseClone();
        copy.TriggerSeconds = [.. TriggerSeconds];
        return copy;
    }
}
