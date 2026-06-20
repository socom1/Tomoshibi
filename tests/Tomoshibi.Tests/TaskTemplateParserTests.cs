using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The task template is a tiny grammar the user types by hand, so it
/// has to be forgiving about whitespace, blank-line block boundaries and the
/// "25" vs "25m" minute forms — all easy to regress.</summary>
public class TaskTemplateParserTests
{
    [Fact]
    public void Empty_or_whitespace_source_yields_no_blocks()
    {
        Assert.Empty(TaskTemplateParser.Parse(null));
        Assert.Empty(TaskTemplateParser.Parse("   \n  \n"));
    }

    [Fact]
    public void Parses_a_full_block()
    {
        var blocks = TaskTemplateParser.Parse(
            "// finish the essay\nstudy: 50\nshort: 10\ncourse: ENG201\ndone");

        var b = Assert.Single(blocks);
        Assert.Equal("finish the essay", b.Title);
        Assert.Equal(50, b.Study);
        Assert.Equal(10, b.Short);
        Assert.Equal("ENG201", b.Course);
        Assert.True(b.IsDone);
    }

    [Fact]
    public void Blank_lines_separate_blocks_and_order_is_preserved()
    {
        var blocks = TaskTemplateParser.Parse("// first\nstudy: 25\n\n// second\nstudy: 50");

        Assert.Equal(2, blocks.Count);
        Assert.Equal("first", blocks[0].Title);
        Assert.Equal("second", blocks[1].Title);
    }

    [Theory]
    [InlineData("study: 25", 25)]
    [InlineData("study: 25m", 25)]
    [InlineData("focus: 30min", 30)]
    public void Minutes_accept_bare_and_suffixed_forms(string line, int expected)
    {
        var b = Assert.Single(TaskTemplateParser.Parse("// t\n" + line));
        Assert.Equal(expected, b.Study);
    }

    [Fact]
    public void Non_positive_or_garbage_minutes_are_ignored()
    {
        var b = Assert.Single(TaskTemplateParser.Parse("// t\nstudy: 0\nshort: abc"));
        Assert.Null(b.Study);
        Assert.Null(b.Short);
    }

    [Fact]
    public void ToggleDone_adds_then_removes_the_flag_for_the_named_block()
    {
        const string src = "// a\nstudy: 25\n\n// b\nstudy: 50";

        var withDone = TaskTemplateParser.ToggleDone(src, "a");
        Assert.True(TaskTemplateParser.Parse(withDone)[0].IsDone);
        Assert.False(TaskTemplateParser.Parse(withDone)[1].IsDone);

        var toggledOff = TaskTemplateParser.ToggleDone(withDone, "a");
        Assert.False(TaskTemplateParser.Parse(toggledOff)[0].IsDone);
    }

    [Fact]
    public void ToggleDone_leaves_source_untouched_for_an_unknown_title()
    {
        const string src = "// a\nstudy: 25";
        Assert.Equal(src, TaskTemplateParser.ToggleDone(src, "nope"));
    }
}
