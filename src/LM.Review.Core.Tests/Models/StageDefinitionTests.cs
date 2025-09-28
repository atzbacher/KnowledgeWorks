using System;
using System.Collections.Generic;
using LM.Review.Core.Models;
using Xunit;

namespace LM.Review.Core.Tests.Models;

public sealed class StageDefinitionTests
{
    [Fact]
    public void Create_WithDataExtractionStage_PreservesStageType()
    {
        var requirement = ReviewerRequirement.Create(new[]
        {
            new KeyValuePair<ReviewerRole, int>(ReviewerRole.Primary, 1)
        });
        var consensus = StageConsensusPolicy.Disabled();

        var definition = StageDefinition.Create(
            id: "extract-1",
            name: "Data Extraction",
            stageType: ReviewStageType.DataExtraction,
            reviewerRequirement: requirement,
            consensusPolicy: consensus,
            displayProfile: StageDisplayProfile.Create(new[]
            {
                StageContentArea.BibliographySummary,
                StageContentArea.DataExtractionWorkspace
            }));

        Assert.Equal(ReviewStageType.DataExtraction, definition.StageType);
    }
}
