using Tomoshibi.Models;

namespace Tomoshibi.Services;

/// <summary>How one mask should show for a given card face.</summary>
public readonly record struct MaskState(bool Masked, bool Highlight);

/// <summary>
/// Pure logic for what an image-occlusion card shows — which masks are covered
/// and which is the highlighted target — for a card's ord and face. Kept out of
/// the view so it can be tested without any UI. Two reveal modes, matching
/// Anki's Image Occlusion Enhanced:
/// <list type="bullet">
/// <item><b>Hide-all-guess-one</b> (note.HideAll = true): the front covers every
/// region and asks for the highlighted one; the back reveals only that one.</item>
/// <item><b>Hide-one-guess-one</b>: the front covers only the target; the back
/// reveals everything.</item>
/// </list>
/// </summary>
public static class OcclusionLayout
{
    /// <summary>Per-mask display for card <paramref name="ord"/> on
    /// <paramref name="side"/>. Index matches <see cref="Note.Occlusions"/>.</summary>
    public static MaskState[] Compute(Note note, int ord, CardSide side)
    {
        var count = note.Occlusions.Count;
        var states = new MaskState[count];

        for (var i = 0; i < count; i++)
        {
            var isTarget = i == ord;
            states[i] = (side, note.HideAll, isTarget) switch
            {
                // Front, hide-all: everything covered, target flagged.
                (CardSide.Front, true, true) => new MaskState(true, true),
                (CardSide.Front, true, false) => new MaskState(true, false),

                // Front, hide-one: only the target covered.
                (CardSide.Front, false, true) => new MaskState(true, true),
                (CardSide.Front, false, false) => new MaskState(false, false),

                // Back, hide-all: target revealed + flagged, others stay covered.
                (CardSide.Back, true, true) => new MaskState(false, true),
                (CardSide.Back, true, false) => new MaskState(true, false),

                // Back, hide-one: everything revealed, target flagged.
                (CardSide.Back, false, true) => new MaskState(false, true),
                _ => new MaskState(false, false)
            };
        }

        return states;
    }
}
