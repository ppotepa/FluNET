namespace FluNET.Automation;

public interface IScheduledTrigger
{
    DateTimeOffset NextAfter(DateTimeOffset instant);
}

public sealed record DailyTimeTriggerDefinition(TimeOnly Time, string TimeZoneId) : TriggerDefinition, IScheduledTrigger
{
    public DateTimeOffset NextAfter(DateTimeOffset instant) => AutomationScheduleCalculator.NextDaily(instant, Time, TimeZoneId);
}

public sealed record WeeklyTimeTriggerDefinition(DayOfWeek DayOfWeek, TimeOnly Time, string TimeZoneId) : TriggerDefinition, IScheduledTrigger
{
    public DateTimeOffset NextAfter(DateTimeOffset instant) => AutomationScheduleCalculator.NextWeekly(instant, DayOfWeek, Time, TimeZoneId);
}

public sealed record CronTriggerDefinition(CronSchedule Schedule, string TimeZoneId) : TriggerDefinition, IScheduledTrigger
{
    public DateTimeOffset NextAfter(DateTimeOffset instant) => Schedule.NextAfter(instant, TimeZoneId);
}

public sealed class CronSchedule
{
    private readonly HashSet<int> minutes;
    private readonly HashSet<int> hours;
    private readonly HashSet<int> days;
    private readonly HashSet<int> months;
    private readonly HashSet<int> weekdays;
    private readonly bool dayAny;
    private readonly bool weekdayAny;

    private CronSchedule(HashSet<int> minutes, HashSet<int> hours, HashSet<int> days, HashSet<int> months, HashSet<int> weekdays, bool dayAny, bool weekdayAny)
    { this.minutes=minutes;this.hours=hours;this.days=days;this.months=months;this.weekdays=weekdays;this.dayAny=dayAny;this.weekdayAny=weekdayAny; }

    public static CronSchedule Parse(string expression)
    {
        string[] parts = expression.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 5) throw new FormatException("CRON requires five fields: minute hour day month weekday.");
        HashSet<int> minute = ParseField(parts[0],0,59,out _);
        HashSet<int> hour = ParseField(parts[1],0,23,out _);
        HashSet<int> day = ParseField(parts[2],1,31,out bool dayAny);
        HashSet<int> month = ParseField(parts[3],1,12,out _);
        HashSet<int> weekday = ParseField(parts[4],0,7,out bool weekdayAny).Select(value=>value==7?0:value).ToHashSet();
        return new(minute,hour,day,month,weekday,dayAny,weekdayAny);
    }

    public DateTimeOffset NextAfter(DateTimeOffset instant, string timeZoneId)
    {
        TimeZoneInfo zone = AutomationScheduleCalculator.ResolveZone(timeZoneId);
        DateTimeOffset local = TimeZoneInfo.ConvertTime(instant, zone);
        DateTime candidate = new(local.Year,local.Month,local.Day,local.Hour,local.Minute,0,DateTimeKind.Unspecified).AddMinutes(1);
        DateTime limit = candidate.AddYears(5);
        while (candidate < limit)
        {
            if (Matches(candidate))
            {
                try
                {
                    DateTime utc = TimeZoneInfo.ConvertTimeToUtc(candidate, zone);
                    DateTimeOffset result = new(utc, TimeSpan.Zero);
                    if (result > instant) return result;
                }
                catch (ArgumentException) { }
            }
            candidate = candidate.AddMinutes(1);
        }
        throw new InvalidOperationException("CRON schedule has no occurrence within five years.");
    }

    private bool Matches(DateTime value)
    {
        if (!minutes.Contains(value.Minute) || !hours.Contains(value.Hour) || !months.Contains(value.Month)) return false;
        bool dayMatch = days.Contains(value.Day);
        bool weekdayMatch = weekdays.Contains((int)value.DayOfWeek);
        bool calendarMatch = dayAny && weekdayAny ? true : dayAny ? weekdayMatch : weekdayAny ? dayMatch : dayMatch || weekdayMatch;
        return calendarMatch;
    }

    private static HashSet<int> ParseField(string source,int minimum,int maximum,out bool any)
    {
        any=source=="*";HashSet<int> result=[];
        foreach(string segment in source.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
        {
            string core=segment;int step=1;int slash=segment.IndexOf('/');if(slash>=0){core=segment[..slash];if(!int.TryParse(segment[(slash+1)..],out step)||step<=0)throw new FormatException($"Invalid CRON step '{segment}'.");}
            int start,end;if(core=="*"){start=minimum;end=maximum;}else{int dash=core.IndexOf('-');if(dash>=0){if(!int.TryParse(core[..dash],out start)||!int.TryParse(core[(dash+1)..],out end))throw new FormatException($"Invalid CRON range '{core}'.");}else{if(!int.TryParse(core,out start))throw new FormatException($"Invalid CRON field '{core}'.");end=start;}}
            if(start<minimum||end>maximum||start>end)throw new FormatException($"CRON value '{segment}' is outside {minimum}..{maximum}.");for(int value=start;value<=end;value+=step)result.Add(value);
        }
        if(result.Count==0)throw new FormatException("CRON field cannot be empty.");return result;
    }
}

public static class AutomationScheduleCalculator
{
    public static DateTimeOffset NextDaily(DateTimeOffset instant, TimeOnly time, string timeZoneId)
    {
        TimeZoneInfo zone=ResolveZone(timeZoneId);DateTimeOffset local=TimeZoneInfo.ConvertTime(instant,zone);DateOnly date=DateOnly.FromDateTime(local.DateTime);
        for(int i=0;i<370;i++,date=date.AddDays(1)){DateTime candidate=date.ToDateTime(time,DateTimeKind.Unspecified);try{DateTime utc=TimeZoneInfo.ConvertTimeToUtc(candidate,zone);DateTimeOffset result=new(utc,TimeSpan.Zero);if(result>instant)return result;}catch(ArgumentException){}}
        throw new InvalidOperationException("Could not resolve the next daily schedule occurrence.");
    }

    public static DateTimeOffset NextWeekly(DateTimeOffset instant, DayOfWeek day, TimeOnly time, string timeZoneId)
    {
        TimeZoneInfo zone=ResolveZone(timeZoneId);DateTimeOffset local=TimeZoneInfo.ConvertTime(instant,zone);DateOnly date=DateOnly.FromDateTime(local.DateTime);
        for(int i=0;i<370;i++,date=date.AddDays(1)){DateTime candidate=date.ToDateTime(time,DateTimeKind.Unspecified);if(candidate.DayOfWeek!=day)continue;try{DateTime utc=TimeZoneInfo.ConvertTimeToUtc(candidate,zone);DateTimeOffset result=new(utc,TimeSpan.Zero);if(result>instant)return result;}catch(ArgumentException){}}
        throw new InvalidOperationException("Could not resolve the next weekly schedule occurrence.");
    }

    public static TimeZoneInfo ResolveZone(string? id)
    {
        string value=string.IsNullOrWhiteSpace(id)?"UTC":id.Trim();
        try{return TimeZoneInfo.FindSystemTimeZoneById(value);}catch(TimeZoneNotFoundException e){throw new FormatException($"Unknown timezone '{value}'.",e);}catch(InvalidTimeZoneException e){throw new FormatException($"Invalid timezone '{value}'.",e);}
    }
}
