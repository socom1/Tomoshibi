using System;
using System.Linq;
using Tomoshibi.Models;
using Tomoshibi.ViewModels;
using Xunit;

namespace Tomoshibi.Tests;

/// <summary>The subjects page's destructive path and its form. Deleting a
/// subject takes a term's worth of marks with it and there's no undo, so the
/// confirmation is worth asserting rather than eyeballing.</summary>
public class SubjectsViewModelTests
{
    private static Subject Subject(string name, params double[] grades)
    {
        var s = new Subject { Name = name, Code = name[..Math.Min(4, name.Length)].ToUpperInvariant() };
        foreach (var g in grades)
            s.Assessments.Add(new Assessment { Title = "Test", Weight = 10, Grade = g });
        return s;
    }

    private static (SubjectsViewModel Vm, AppState State) Vm(params Subject[] subjects)
    {
        var state = new AppState();
        state.Subjects.AddRange(subjects);
        return (new SubjectsViewModel(state, () => { }, _ => { }), state);
    }

    // ---- deleting ----

    [Fact]
    public void The_delete_button_asks_before_it_takes_anything()
    {
        var (vm, state) = Vm(Subject("Algorithms", 80, 90));
        var row = Assert.Single(vm.Items);

        vm.RemoveCommand.Execute(row);

        Assert.True(vm.IsRemoveConfirmOpen);
        Assert.Single(state.Subjects);   // nothing gone yet
        Assert.Single(vm.Items);
    }

    [Fact]
    public void The_prompt_says_what_goes_with_it()
    {
        var (vm, _) = Vm(Subject("Algorithms", 80, 90, 75));

        vm.RemoveCommand.Execute(vm.Items[0]);

        Assert.Equal("Algorithms", vm.RemoveConfirmName);
        Assert.Contains("3 assessments", vm.RemoveConfirmDetail);
        Assert.Contains("can't be undone", vm.RemoveConfirmDetail);
    }

    [Fact]
    public void An_empty_subject_doesnt_claim_to_be_taking_marks()
    {
        var (vm, _) = Vm(Subject("Just added"));

        vm.RemoveCommand.Execute(vm.Items[0]);

        Assert.DoesNotContain("assessment", vm.RemoveConfirmDetail);
        Assert.Contains("can't be undone", vm.RemoveConfirmDetail);
    }

    [Fact]
    public void One_assessment_reads_as_one()
    {
        var (vm, _) = Vm(Subject("Algorithms", 80));

        vm.RemoveCommand.Execute(vm.Items[0]);

        Assert.Contains("1 assessment ", vm.RemoveConfirmDetail);
    }

    [Fact]
    public void Confirming_removes_it()
    {
        var (vm, state) = Vm(Subject("Algorithms", 80), Subject("Statistics", 60));

        vm.RemoveCommand.Execute(vm.Items.First(i => i.Model.Name == "Algorithms"));
        vm.ConfirmRemoveCommand.Execute(null);

        Assert.Equal("Statistics", Assert.Single(state.Subjects).Name);
        Assert.False(vm.IsRemoveConfirmOpen);
    }

    [Fact]
    public void Backing_out_keeps_it()
    {
        var (vm, state) = Vm(Subject("Algorithms", 80));

        vm.RemoveCommand.Execute(vm.Items[0]);
        vm.CancelRemoveCommand.Execute(null);

        Assert.Single(state.Subjects);
        Assert.False(vm.IsRemoveConfirmOpen);
        Assert.Single(vm.Items);
    }

    [Fact]
    public void Confirming_with_nothing_staged_does_nothing()
    {
        var (vm, state) = Vm(Subject("Algorithms", 80));

        vm.ConfirmRemoveCommand.Execute(null);

        Assert.Single(state.Subjects);
    }

    // ---- the form ----

    [Fact]
    public void A_subject_with_no_name_is_refused_and_says_why()
    {
        var (vm, state) = Vm();
        vm.OpenAddCommand.Execute(null);
        vm.FormName = "   ";

        vm.ConfirmModalCommand.Execute(null);

        Assert.Empty(state.Subjects);
        Assert.True(vm.IsFormNameInvalid);
        Assert.Contains("name", vm.FormError);
        Assert.True(vm.IsModalOpen);
    }

    [Fact]
    public void The_complaint_clears_as_soon_as_theyre_fixing_it()
    {
        var (vm, _) = Vm();
        vm.OpenAddCommand.Execute(null);
        vm.ConfirmModalCommand.Execute(null);
        Assert.True(vm.IsFormNameInvalid);

        vm.FormName = "Linear Algebra";

        Assert.False(vm.IsFormNameInvalid);
        Assert.False(vm.HasFormError);
    }

    [Fact]
    public void A_completed_form_creates_the_subject()
    {
        var (vm, state) = Vm();
        vm.OpenAddCommand.Execute(null);
        vm.FormName = "Linear Algebra";
        vm.FormCode = "MATH201";
        vm.FormCredits = 3;

        vm.ConfirmModalCommand.Execute(null);

        var made = Assert.Single(state.Subjects);
        Assert.Equal("Linear Algebra", made.Name);
        Assert.Equal("MATH201", made.Code);
        Assert.Equal(3, made.Credits);
        Assert.False(vm.IsModalOpen);
    }

    [Fact]
    public void The_grade_scale_moved_out_but_still_answers()
    {
        // The extraction that had to be verified by hand at the time.
        var (vm, _) = Vm(Subject("Algorithms", 80));

        Assert.NotNull(vm.Scale);
        Assert.NotEmpty(vm.Scale.Options);
        Assert.Contains(vm.Scale.Options, o => o.Kind == GradeScaleKind.Custom);
    }
}
