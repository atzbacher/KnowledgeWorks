#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using LM.Review.Core.Models;

namespace LM.App.Wpf.ViewModels.Review;

public sealed class StageDisplayOptionViewModel : ObservableObject
{
    private bool _isSelected;

    public StageDisplayOptionViewModel(StageContentArea area, bool isSelected)
    {
        Area = area;
        _isSelected = isSelected;
    }

    public StageContentArea Area { get; }

    public string Label => Area switch
    {
        StageContentArea.BibliographySummary => "Title & abstract",
        StageContentArea.InclusionExclusionChecklist => "Inclusion/exclusion checklist",
        StageContentArea.FullTextViewer => "Full-text PDF viewer",
        StageContentArea.ReviewerDecisionPanel => "Reviewer decision panel",
        StageContentArea.DataExtractionWorkspace => "Data extraction workspace",
        StageContentArea.NotesPane => "Notes & annotations",
        _ => Area.ToString()
    };

    public string Description => Area switch
    {
        StageContentArea.BibliographySummary => "Show the publication title, authors, and abstract on the left pane.",
        StageContentArea.InclusionExclusionChecklist => "Enable quick inclusion or exclusion toggles aligned with PICO(S).",
        StageContentArea.FullTextViewer => "Embed a PDF viewer with drag-and-drop support when files are attached.",
        StageContentArea.ReviewerDecisionPanel => "Expose keyboard-driven decision shortcuts and reviewer controls.",
        StageContentArea.DataExtractionWorkspace => "Open structured extraction tables configured for the stage.",
        StageContentArea.NotesPane => "Offer free-form notes and templated feedback fields for reviewers.",
        _ => string.Empty
    };

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
