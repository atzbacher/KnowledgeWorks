#nullable enable
using System.Collections.Generic;
using LM.Review.Core.Models;

namespace LM.App.Wpf.Services.Review.Design;

internal static class StageDisplayProfileFactory
{
    private static readonly IReadOnlyList<StageContentArea> TitleScreeningDefaults = new[]
    {
        StageContentArea.BibliographySummary,
        StageContentArea.InclusionExclusionChecklist,
        StageContentArea.ReviewerDecisionPanel
    };

    private static readonly IReadOnlyList<StageContentArea> FullTextDefaults = new[]
    {
        StageContentArea.BibliographySummary,
        StageContentArea.FullTextViewer,
        StageContentArea.ReviewerDecisionPanel,
        StageContentArea.NotesPane
    };

    private static readonly IReadOnlyList<StageContentArea> DataExtractionDefaults = new[]
    {
        StageContentArea.BibliographySummary,
        StageContentArea.DataExtractionWorkspace,
        StageContentArea.NotesPane
    };

    private static readonly IReadOnlyList<StageContentArea> ConsensusDefaults = new[]
    {
        StageContentArea.BibliographySummary,
        StageContentArea.ReviewerDecisionPanel,
        StageContentArea.NotesPane
    };

    public static IReadOnlyList<StageContentArea> GetAvailableAreas(ReviewStageType stageType)
    {
        return stageType switch
        {
            ReviewStageType.TitleScreening => TitleScreeningDefaults,
            ReviewStageType.FullTextReview => FullTextDefaults,
            ReviewStageType.DataExtraction => DataExtractionDefaults,
            ReviewStageType.ConsensusMeeting => ConsensusDefaults,
            ReviewStageType.QualityAssurance => ConsensusDefaults,
            _ => TitleScreeningDefaults
        };
    }

    public static StageDisplayProfile CreateDefault(ReviewStageType stageType)
    {
        return StageDisplayProfile.Create(GetAvailableAreas(stageType));
    }
}
