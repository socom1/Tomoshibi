using Xunit;

namespace Tomoshibi.Tests;

/// <summary>Marks the tests that install their own <c>GradeScale</c> bands.
///
/// <para>Those bands are process-wide. A test that swaps them out and puts
/// them back is well behaved on its own, but xUnit runs test classes in
/// parallel, so anything else reading the custom scale at the same moment
/// would see the wrong table. Sharing a collection is what stops that
/// overlapping.</para>
///
/// <para>This originally existed to hold <c>SubjectsViewModelTests</c> apart
/// too, because building a <c>SubjectsViewModel</c> reached through to
/// <c>GradeScaleViewModel</c>'s constructor and reassigned the static as a
/// side effect. That write is gone — the bands are installed once now, where
/// app state is assembled — so the class no longer touches the static and no
/// longer needs to be held back. What's left is a marker for anything that
/// deliberately mutates the engine.</para></summary>
[CollectionDefinition(Name)]
public class GradeScaleStaticCollection
{
    public const string Name = "grade-scale static bands";
}
