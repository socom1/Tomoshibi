using System.Linq;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>Image-occlusion mask visibility must follow Anki's two reveal
/// modes exactly, and one card must be generated per mask.</summary>
public class OcclusionTests
{
    private static Note Occluded(int masks, bool hideAll)
    {
        var note = new Note { Type = NoteType.ImageOcclusion, HideAll = hideAll, Fields = { "img.png", "", "" } };
        for (var i = 0; i < masks; i++)
            note.Occlusions.Add(new OcclusionRect { X = 0.1 * i, Y = 0.1, W = 0.2, H = 0.1 });
        CardGenerator.Sync(note);
        return note;
    }

    [Fact]
    public void One_card_is_generated_per_mask()
    {
        var note = Occluded(3, hideAll: true);
        Assert.Equal(new[] { 0, 1, 2 }, note.Cards.Select(c => c.Ord).OrderBy(o => o));
    }

    [Fact]
    public void Hide_all_covers_every_mask_on_the_front_and_flags_the_target()
    {
        var note = Occluded(3, hideAll: true);
        var front = OcclusionLayout.Compute(note, ord: 1, CardSide.Front);

        Assert.All(front, m => Assert.True(m.Masked));       // everything covered
        Assert.True(front[1].Highlight);                     // target flagged
        Assert.False(front[0].Highlight);
    }

    [Fact]
    public void Hide_all_reveals_only_the_target_on_the_back()
    {
        var note = Occluded(3, hideAll: true);
        var back = OcclusionLayout.Compute(note, ord: 1, CardSide.Back);

        Assert.False(back[1].Masked);  // target revealed
        Assert.True(back[1].Highlight);
        Assert.True(back[0].Masked);   // others stay covered
        Assert.True(back[2].Masked);
    }

    [Fact]
    public void Hide_one_covers_only_the_target_on_the_front()
    {
        var note = Occluded(3, hideAll: false);
        var front = OcclusionLayout.Compute(note, ord: 2, CardSide.Front);

        Assert.True(front[2].Masked);   // only the target covered
        Assert.False(front[0].Masked);
        Assert.False(front[1].Masked);
    }

    [Fact]
    public void Hide_one_reveals_everything_on_the_back()
    {
        var note = Occluded(3, hideAll: false);
        var back = OcclusionLayout.Compute(note, ord: 2, CardSide.Back);

        Assert.All(back, m => Assert.False(m.Masked));
        Assert.True(back[2].Highlight);
    }

    [Fact]
    public void Deleting_a_mask_drops_its_card_but_keeps_the_others()
    {
        var note = Occluded(3, hideAll: true);
        note.Cards.Single(c => c.Ord == 1).Stability = 99; // give a survivor some history

        note.Occlusions.RemoveAt(2); // remove the last mask
        CardGenerator.Sync(note);

        Assert.Equal(2, note.Cards.Count);
        Assert.Equal(99, note.Cards.Single(c => c.Ord == 1).Stability);
    }
}
