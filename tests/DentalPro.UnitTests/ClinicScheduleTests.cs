using DentalPro.Domain.Appointments;

namespace DentalPro.UnitTests;

public sealed class ClinicScheduleTests
{
    [Fact]
    public void Create_AllowsConfiguredHoursAndClosedOrReplacementExceptions()
    {
        var schedule = ClinicSchedule.Create("America/Argentina/Buenos_Aires", 30, 15,
        [
            new(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0)),
            new(DayOfWeek.Monday, new TimeOnly(15, 0), new TimeOnly(19, 0))
        ],
        [
            new(new DateOnly(2030, 1, 1)),
            new(new DateOnly(2030, 1, 2), [(new TimeOnly(10, 0), new TimeOnly(12, 0))])
        ]);

        Assert.Equal(2, schedule.WeeklyIntervals.Count);
        Assert.True(schedule.DateExceptions[0].IsClosed);
        Assert.Single(schedule.DateExceptions[1].Intervals);
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(30, 0)]
    public void Create_RejectsNonPositiveDurationOrIncrement(int duration, int increment) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ClinicSchedule.Create("UTC", duration, increment));

    [Fact]
    public void Create_RejectsBlankZoneAndInvalidOrOverlappingIntervals()
    {
        Assert.Throws<ArgumentException>(() => ClinicSchedule.Create(" ", 30, 15));
        Assert.Throws<ArgumentException>(() => ClinicSchedule.Create("UTC", 30, 15,
            [new(DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(10, 0))]));
        Assert.Throws<ArgumentException>(() => ClinicSchedule.Create("UTC", 30, 15,
        [
            new(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(11, 0)),
            new(DayOfWeek.Tuesday, new TimeOnly(10, 30), new TimeOnly(12, 0))
        ]));
    }

    [Fact]
    public void Create_RejectsOverlappingExceptionIntervalsAndDuplicateExceptionDates()
    {
        var date = new DateOnly(2030, 5, 1);
        Assert.Throws<ArgumentException>(() => new ClinicDateException(date,
        [
            (new TimeOnly(9, 0), new TimeOnly(11, 0)),
            (new TimeOnly(10, 0), new TimeOnly(12, 0))
        ]));
        Assert.Throws<ArgumentException>(() => ClinicSchedule.Create("UTC", 30, 15, dateExceptions:
        [new(date), new(date)]));
    }
}
