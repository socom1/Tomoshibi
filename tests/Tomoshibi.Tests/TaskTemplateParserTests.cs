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
    public void Parse_stamps_each_block_with_its_source_line()
    {
        var blocks = TaskTemplateParser.Parse("// a\nstudy: 25\n\n\n// b\nstudy: 50");

        Assert.Equal(0, blocks[0].StartLine);
        Assert.Equal(4, blocks[1].StartLine);
    }

    [Fact]
    public void ToggleDone_adds_then_removes_the_flag_for_the_given_block()
    {
        const string src = "// a\nstudy: 25\n\n// b\nstudy: 50";

        var withDone = TaskTemplateParser.ToggleDone(src, TaskTemplateParser.Parse(src)[0]);
        Assert.True(TaskTemplateParser.Parse(withDone)[0].IsDone);
        Assert.False(TaskTemplateParser.Parse(withDone)[1].IsDone);

        var toggledOff = TaskTemplateParser.ToggleDone(withDone, TaskTemplateParser.Parse(withDone)[0]);
        Assert.False(TaskTemplateParser.Parse(toggledOff)[0].IsDone);
    }

    [Fact]
    public void ToggleDone_leaves_source_untouched_for_a_stale_block()
    {
        const string src = "// a\nstudy: 25";

        // A block whose anchor no longer points at anything real — e.g. parsed
        // from an older, longer version of the source.
        var stale = new Tomoshibi.Models.TaskBlock { Title = "a", StartLine = 40 };

        Assert.Equal(src, TaskTemplateParser.ToggleDone(src, stale));
        Assert.Equal(src, TaskTemplateParser.ToggleDone(src, null));
    }

    [Fact]
    public void ToggleDone_only_touches_the_given_block_not_lines_around_it()
    {
        const string src = "// warm-up\nstudy: 25\n\n// deep work\nstudy: 50\ncourse: CS210";

        var toggled = TaskTemplateParser.ToggleDone(src, TaskTemplateParser.Parse(src)[1]);

        var blocks = TaskTemplateParser.Parse(toggled);
        Assert.False(blocks[0].IsDone);
        Assert.True(blocks[1].IsDone);
        // The untouched block's text survives byte-for-byte.
        Assert.StartsWith("// warm-up\nstudy: 25\n", toggled);
    }

    [Fact]
    public void ToggleDone_with_duplicate_titles_toggles_exactly_the_clicked_block()
    {
        // Two tasks may share a title ("revise"); the block anchor keeps them
        // independent instead of always resolving to the first match.
        const string src = "// revise\nstudy: 25\n\n// revise\nstudy: 50";

        var toggled = TaskTemplateParser.ToggleDone(src, TaskTemplateParser.Parse(src)[1]);

        var blocks = TaskTemplateParser.Parse(toggled);
        Assert.False(blocks[0].IsDone);
        Assert.True(blocks[1].IsDone);
    }

    [Fact]
    public void ToggleDone_handles_windows_line_endings()
    {
        const string src = "// a\r\nstudy: 25\r\n\r\n// b\r\nstudy: 50";

        var toggled = TaskTemplateParser.ToggleDone(src, TaskTemplateParser.Parse(src)[1]);

        var blocks = TaskTemplateParser.Parse(toggled);
        Assert.False(blocks[0].IsDone);
        Assert.True(blocks[1].IsDone);
    }

    [Fact]
    public void ToggleDone_works_on_a_block_whose_title_line_is_not_its_first_line()
    {
        // The anchor is the block's first line, wherever the title sits.
        const string src = "study: 25\n// late title\n\n// other\nstudy: 50";

        var toggled = TaskTemplateParser.ToggleDone(src, TaskTemplateParser.Parse(src)[0]);

        var blocks = TaskTemplateParser.Parse(toggled);
        Assert.True(blocks[0].IsDone);
        Assert.False(blocks[1].IsDone);
    }
}
