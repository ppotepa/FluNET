using FluNET.Automation;using FluNET.Context;namespace FluNET.Tests.Automation;[TestFixture]public sealed class CalendarAutomationTests{[Test]public void CompilerAcceptsDailyWeeklyAndCronTriggers(){using FluNETContext c=SurfaceCompilationExtensions.CreateSurfaceContext();AutomationCompilationResult r=c.CompileAutomations("""
EVERY DAY AT 08:00
    SAY daily
EVERY MONDAY AT 09:00 IN UTC
    SAY weekly
CRON "0 8 * * 1-5" IN UTC
    SAY cron
""");Assert.That(r.IsValid,Is.True,string.Join(" | ",r.Diagnostics.Select(d=>d.Message)));Assert.Multiple(()=>{Assert.That(r.Automations[0].Trigger,Is.TypeOf<DailyTimeTriggerDefinition>());Assert.That(r.Automations[1].Trigger,Is.TypeOf<WeeklyTimeTriggerDefinition>());Assert.That(r.Automations[2].Trigger,Is.TypeOf<CronTriggerDefinition>());});}[Test]public void CronFindsNextWeekdayAtEightUtc(){CronSchedule cron=CronSchedule.Parse("0 8 * * 1-5");DateTimeOffset next=cron.NextAfter(new DateTimeOffset(2026,8,14,8,1,0,TimeSpan.Zero),"UTC");Assert.That(next,Is.EqualTo(new DateTimeOffset(2026,8,17,8,0,0,TimeSpan.Zero)));}}
