using FW.Application.Common;
using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Domain.Enums;
using FW.Application.Features.Assistant;
using FW.Application.Features.Attrition;

namespace FW.UnitTests.Assistant;

public class RelativeDateResolverTests
{
    // Thứ Tư 18/11/2026 → tuần này bắt đầu Thứ Hai 16/11
    private static readonly DateOnly Wed = new(2026, 11, 18);

    [Theory]
    [InlineData(DayOfWeek.Monday, 0, "2026-11-16")]
    [InlineData(DayOfWeek.Thursday, 0, "2026-11-19")]
    [InlineData(DayOfWeek.Sunday, 0, "2026-11-22")]
    [InlineData(DayOfWeek.Thursday, 1, "2026-11-26")]
    [InlineData(DayOfWeek.Monday, -1, "2026-11-09")]
    public void Resolve_uses_monday_based_weeks(DayOfWeek day, int weekOffset, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), RelativeDateResolver.Resolve(Wed, day, weekOffset));

    [Fact]
    public void On_a_sunday_this_week_still_means_the_week_that_started_on_monday()
    {
        var sunday = new DateOnly(2026, 11, 22);
        Assert.Equal(new DateOnly(2026, 11, 16), RelativeDateResolver.Resolve(sunday, DayOfWeek.Monday, 0));
        Assert.Equal(new DateOnly(2026, 11, 23), RelativeDateResolver.Resolve(sunday, DayOfWeek.Monday, 1));
    }

    [Theory]
    [InlineData("thursday", DayOfWeek.Thursday)]
    [InlineData("Thu_5", DayOfWeek.Thursday)]
    [InlineData("thứ 5", DayOfWeek.Thursday)]
    [InlineData("Thứ Năm", DayOfWeek.Thursday)]
    [InlineData("T5", DayOfWeek.Thursday)]
    [InlineData("5", DayOfWeek.Thursday)]
    [InlineData("chủ nhật", DayOfWeek.Sunday)]
    [InlineData("CN", DayOfWeek.Sunday)]
    [InlineData("monday", DayOfWeek.Monday)]
    [InlineData("thứ hai", DayOfWeek.Monday)]
    public void Weekday_parsing_is_tolerant(string text, DayOfWeek expected)
    {
        Assert.True(RelativeDateResolver.TryParseWeekday(text, out var day));
        Assert.Equal(expected, day);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tomorrow")]
    [InlineData("thứ 9")]
    public void Unknown_weekday_is_rejected(string text) =>
        Assert.False(RelativeDateResolver.TryParseWeekday(text, out _));

    [Theory]
    [InlineData(null, 0)]
    [InlineData("this_week", 0)]
    [InlineData("next_week", 1)]
    [InlineData("tuần sau", 1)]
    [InlineData("tuần này", 0)]
    [InlineData("last_week", -1)]
    public void Week_parsing(string? text, int expected)
    {
        Assert.True(RelativeDateResolver.TryParseWeek(text, out var offset));
        Assert.Equal(expected, offset);
    }

    [Fact]
    public void Unknown_week_is_rejected() => Assert.False(RelativeDateResolver.TryParseWeek("someday", out _));
}

public class ShiftPartClassifierTests
{
    [Theory]
    [InlineData(5, 0, ShiftPart.Morning)]
    [InlineData(8, 0, ShiftPart.Morning)]
    [InlineData(11, 59, ShiftPart.Morning)]
    [InlineData(12, 0, ShiftPart.Afternoon)]
    [InlineData(13, 0, ShiftPart.Afternoon)]
    [InlineData(17, 0, ShiftPart.Evening)]
    [InlineData(18, 0, ShiftPart.Evening)]
    [InlineData(22, 0, ShiftPart.Night)]
    [InlineData(2, 0, ShiftPart.Night)]
    [InlineData(4, 59, ShiftPart.Night)]
    public void Classifies_by_start_time(int h, int m, ShiftPart expected) =>
        Assert.Equal(expected, ShiftPartClassifier.Classify(new TimeSpan(h, m, 0)));

    [Theory]
    [InlineData("morning", ShiftPart.Morning)]
    [InlineData("sáng", ShiftPart.Morning)]
    [InlineData("Ca chiều", ShiftPart.Afternoon)]
    [InlineData("tối", ShiftPart.Evening)]
    [InlineData("đêm", ShiftPart.Night)]
    public void Parses_part_names(string text, ShiftPart expected)
    {
        Assert.True(ShiftPartClassifier.TryParse(text, out var part));
        Assert.Equal(expected, part);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("full_day")]
    [InlineData("cả ngày")]
    public void Full_day_means_no_part(string? text)
    {
        Assert.True(ShiftPartClassifier.TryParse(text, out var part));
        Assert.Null(part);
    }

    [Fact]
    public void Garbage_is_rejected() => Assert.False(ShiftPartClassifier.TryParse("xyz", out _));
}

public class WeeklyJobScheduleTests
{
    [Fact]
    public void Monday_before_run_hour_runs_the_same_day()
    {
        var next = FW.Infrastructure.Services.Background.AttritionWeeklyJob.NextRun(new DateTime(2026, 11, 16, 1, 0, 0), DayOfWeek.Monday, 2);
        Assert.Equal(new DateTime(2026, 11, 16, 2, 0, 0), next);
    }

    [Theory]
    [InlineData("2026-11-16 02:00")] // đúng giờ chạy → tuần sau (nghiêm ngặt sau now)
    [InlineData("2026-11-16 09:30")]
    [InlineData("2026-11-18 10:00")]
    [InlineData("2026-11-22 23:59")]
    public void Otherwise_runs_next_monday(string now)
    {
        var next = FW.Infrastructure.Services.Background.AttritionWeeklyJob.NextRun(DateTime.Parse(now), DayOfWeek.Monday, 2);
        Assert.Equal(DayOfWeek.Monday, next.DayOfWeek);
        Assert.True(next > DateTime.Parse(now));
        Assert.True(next - DateTime.Parse(now) <= TimeSpan.FromDays(7));
        Assert.Equal(2, next.Hour);
    }
}

public class DateGroundingTests
{
    private static readonly DateOnly Wed = new(2026, 11, 18);

    private static DateOnly D(int m, int d) => new(2026, m, d);

    [Theory]
    [InlineData("Cho em nghỉ ngày mai vì có việc gia đình", "2026-11-19")]
    [InlineData("em xin nghỉ sáng mai", "2026-11-19")]
    [InlineData("Hôm nay em bị sốt", "2026-11-18")]
    [InlineData("nghỉ ngày kia nhé", "2026-11-20")]
    [InlineData("nghỉ hôm qua", "2026-11-17")]
    [InlineData("Cho em xin nghỉ ca sáng thứ 5 tuần này vì bận thi", "2026-11-19")]
    [InlineData("xin nghỉ thứ năm tuần sau", "2026-11-26")]
    [InlineData("Xin nghỉ thứ 2 tuần sau vì chuyển nhà", "2026-11-23")]
    [InlineData("nghỉ chủ nhật tuần này", "2026-11-22")]
    [InlineData("nghỉ ngày 25/11 nhé", "2026-11-25")]
    [InlineData("nghỉ ngày 25 tháng 11", "2026-11-25")]
    [InlineData("nghỉ 3/12/2026", "2026-12-03")]
    public void Extracts_the_dates_a_person_would_mean(string text, string expected) =>
        Assert.Contains(DateOnly.Parse(expected), DateMentionExtractor.Extract(text, Wed));

    [Fact]
    public void Unqualified_weekday_means_either_of_the_next_two_occurrences()
    {
        var m = DateMentionExtractor.Extract("xin nghỉ thứ 6", Wed);
        Assert.Equal(new[] { D(11, 20), D(11, 27) }, m.OrderBy(x => x));
    }

    [Fact]
    public void A_weekday_that_already_passed_this_week_rolls_forward_when_unqualified()
    {
        var m = DateMentionExtractor.Extract("nghỉ thứ 2", Wed); // thứ 2 tuần này đã qua
        Assert.Equal(new[] { D(11, 23), D(11, 30) }, m.OrderBy(x => x));
    }

    [Theory]
    [InlineData("Em còn mấy ngày phép?")]
    [InlineData("Tháng này em được bao nhiêu công?")]
    [InlineData("Em có 11 ngày phép")]
    [InlineData("")]
    [InlineData(null)]
    public void Finds_nothing_in_sentences_without_dates(string? text) =>
        Assert.Empty(DateMentionExtractor.Extract(text, Wed));

    [Fact]
    public void Impossible_dates_are_ignored() =>
        Assert.Empty(DateMentionExtractor.Extract("nghỉ 31/2 và 45/13", Wed));

    [Fact]
    public void Matching_date_is_kept_untouched()
    {
        var g = DateGrounding.Apply(D(11, 19), D(11, 19), "nghỉ ngày mai", Wed);
        Assert.Equal(D(11, 19), g.Start);
        Assert.Null(g.Note);
    }

    [Fact]
    public void Wrong_date_is_corrected_when_the_user_named_exactly_one()
    {
        var g = DateGrounding.Apply(D(11, 25), D(11, 25), "Cho em nghỉ ngày mai vì có việc", Wed);
        Assert.Equal(D(11, 19), g.Start);
        Assert.Equal(D(11, 19), g.End);
        Assert.Contains("19/11/2026", g.Note);
    }

    [Fact]
    public void Range_with_both_dates_named_is_accepted()
    {
        var g = DateGrounding.Apply(D(11, 25), D(11, 27), "nghỉ từ 25/11 đến 27/11", Wed);
        Assert.Equal((D(11, 25), D(11, 27)), (g.Start, g.End));
    }

    [Fact]
    public void Wrong_date_with_several_named_dates_is_an_error_listing_the_valid_ones()
    {
        var ex = Assert.Throws<ToolArgumentException>(() =>
            DateGrounding.Apply(D(11, 25), D(11, 25), "nghỉ ca sáng thứ 5 và thứ 6 tuần này", Wed));
        Assert.Contains("2026-11-19", ex.Message);
        Assert.Contains("2026-11-20", ex.Message);
    }

    [Fact]
    public void No_dates_in_the_message_leaves_the_models_date_alone()
    {
        var g = DateGrounding.Apply(D(11, 25), D(11, 25), "vì bận thi ạ", Wed);   // ngày có thể đến từ lượt trước
        Assert.Equal(D(11, 25), g.Start);
        Assert.Null(g.Note);
    }
}

public class ShiftPartGroundingTests
{
    [Theory]
    [InlineData("xin nghỉ ca tối thứ 4", ShiftPart.Evening)]
    [InlineData("Đổi ca chiều thứ 5 với Lan", ShiftPart.Afternoon)]
    [InlineData("nghỉ ca sáng thứ 5 và ca sáng thứ 6", ShiftPart.Morning)]
    [InlineData("nghỉ buổi chiều mai", ShiftPart.Afternoon)]
    [InlineData("ca đêm", ShiftPart.Night)]
    public void Single_mentioned_part_is_found(string text, ShiftPart expected) =>
        Assert.Equal(expected, ShiftPartGrounding.OnlyMention(text));

    [Theory]
    [InlineData("nghỉ ca sáng và ca chiều thứ 5")]
    [InlineData("đổi ca sáng của em lấy ca chiều của Lan")]
    [InlineData("nghỉ cả ngày thứ 5")]
    [InlineData("")]
    public void Zero_or_several_parts_means_no_intervention(string text) =>
        Assert.Null(ShiftPartGrounding.OnlyMention(text));

    [Fact]
    public void Omitted_or_wrong_part_is_corrected_and_a_matching_one_is_untouched()
    {
        Assert.Equal(ShiftPart.Evening, ShiftPartGrounding.Apply(null, "xin nghỉ ca tối").Part);
        Assert.NotNull(ShiftPartGrounding.Apply(ShiftPart.Morning, "xin nghỉ ca tối").Note);
        Assert.Null(ShiftPartGrounding.Apply(ShiftPart.Evening, "xin nghỉ ca tối").Note);
        Assert.Equal(ShiftPart.Morning, ShiftPartGrounding.Apply(ShiftPart.Morning, "nghỉ cả ngày").Part);
    }
}
