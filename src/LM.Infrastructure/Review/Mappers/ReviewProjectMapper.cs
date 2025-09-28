using System.Collections.Generic;
using System.Linq;
using LM.Infrastructure.Review.Dto;
using LM.Review.Core.Models;

namespace LM.Infrastructure.Review.Mappers;

internal static class ReviewProjectMapper
{
    public static ReviewProjectDto ToDto(ReviewProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var dto = new ReviewProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            CreatedAt = project.CreatedAt,
            StageDefinitions = project.StageDefinitions
                .Select(StageDefinitionMapper.ToDto)
                .ToList(),
            Metadata = ReviewProjectMetadataMapper.ToDto(project.Metadata),
            AuditTrail = project.AuditTrail.Entries
                .Select(ReviewAuditTrailMapper.ToDto)
                .ToList()
        };

        return ReviewDtoAuditStamp.Stamp(dto);
    }

    public static ReviewProject ToDomain(ReviewProjectDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var definitions = dto.StageDefinitions?
            .Select(StageDefinitionMapper.ToDomain)
            .ToList() ?? new List<StageDefinition>();

        var auditEntries = dto.AuditTrail?
            .Select(ReviewAuditTrailMapper.ToDomain)
            .ToList() ?? new List<ReviewAuditTrail.AuditEntry>();

        var metadataDto = dto.Metadata ?? new ReviewProjectMetadataDto();
        var metadata = ReviewProjectMetadataMapper.ToDomain(metadataDto);
        var auditTrail = ReviewAuditTrail.Create(auditEntries);

        return ReviewProject.Create(dto.Id, dto.Name, dto.CreatedAt, definitions, metadata, auditTrail);
    }
}

internal static class ReviewProjectMetadataMapper
{
    public static ReviewProjectMetadataDto ToDto(ReviewProjectMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var dto = new ReviewProjectMetadataDto
        {
            Template = metadata.Template,
            Notes = metadata.Notes
        };

        return ReviewDtoAuditStamp.Stamp(dto);
    }

    public static ReviewProjectMetadata ToDomain(ReviewProjectMetadataDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return ReviewProjectMetadata.Create(dto.Template, dto.Notes);
    }
}
