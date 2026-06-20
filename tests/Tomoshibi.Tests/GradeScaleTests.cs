using System.Collections.Generic;
using Tomoshibi.Models;
using Tomoshibi.Services;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The grade engine is pure percentage→label/points conversion, and
/// it drives every figure on the subjects page — so its band boundaries are
/// exactly the kind of off-by-one-prone logic worth pinning down.</summary>
public class GradeScaleTests
{
    [Theory]
    [InlineData(93, "A")]
    [InlineData(92.9, "A-")]
    [InlineData(90, "A-")]
    [InlineData(87, "B+")]
    [InlineData(83, "B")]
    [InlineData(60, "D-")]
    [InlineData(59.9, "F")]
    [InlineData(0, "F")]
    public void UsGpa_labels_on_band_boundaries(double percent, string expected)
        => Assert.Equal(expected, GradeScale.Label(GradeScaleKind.UsGpa, percent));

    [Theory]
    [InlineData(93, 4.0)]
    [InlineData(90, 3.7)]
    [InlineData(82.9, 2.7)]
    [InlineData(59.9, 0.0)]
    public void UsGpa_points_match_the_standard_table(double percent, double expected)
        => Assert.Equal(expected, GradeScale.Points(GradeScaleKind.UsGpa, percent));

    [Theory]
    [InlineData(70, "first")]
    [InlineData(69.9, "2:1")]
    [InlineData(40, "third")]
    [InlineData(39.9, "fail")]
    public void UkHonours_classifies_by_band(double percent, string expected)
        => Assert.Equal(expected, GradeScale.Label(GradeScaleKind.UkHonours, percent));

    [Fact]
    public void Percentage_scale_has_no_label()
        => Assert.Equal(string.Empty, GradeScale.Label(GradeScaleKind.Percentage, 75));

    [Fact]
    public void Custom_bands_label_and_score_from_the_user_table()
    {
        // The static bands are app-wide state; set them for this test and put
        // them back so other tests see a clean engine.
        var previous = GradeScale.CustomBands;
        try
        {
            GradeScale.CustomBands = new List<GradeBand>
            {
                new() { MinPercent = 50, Label = "Pass", Points = 1.0 },
                new() { MinPercent = 0, Label = "Fail", Points = 0.0 },
            };

            Assert.Equal("Pass", GradeScale.Label(GradeScaleKind.Custom, 50));
            Assert.Equal("Fail", GradeScale.Label(GradeScaleKind.Custom, 49.9));
            Assert.Equal(1.0, GradeScale.Points(GradeScaleKind.Custom, 80));
            Assert.True(GradeScale.UsesPoints(GradeScaleKind.Custom));
        }
        finally
        {
            GradeScale.CustomBands = previous;
        }
    }

    [Fact]
    public void Custom_scale_without_points_is_not_a_gpa_scale()
    {
        var previous = GradeScale.CustomBands;
        try
        {
            GradeScale.CustomBands = new List<GradeBand>
            {
                new() { MinPercent = 0, Label = "Done", Points = 0.0 },
            };
            Assert.False(GradeScale.UsesPoints(GradeScaleKind.Custom));
        }
        finally
        {
            GradeScale.CustomBands = previous;
        }
    }
}
