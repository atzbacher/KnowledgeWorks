namespace LM.Core.Models
{
    public sealed record PublicationRecord
    {
        public string? Title { get; init; }
        public string? JournalTitle { get; init; }
        public string? JournalIsoAbbrev { get; init; }
        public string? Volume { get; init; }
        public string? Issue { get; init; }
        public string? Pages { get; init; }

        public int? Year { get; init; }
        public DateOnly? PublishedPrint { get; init; }
        public DateOnly? PublishedEpub { get; init; }

        public string? Doi { get; init; }
        public string? Pmid { get; init; }
        public string? Pmcid { get; init; }
        public string? UrlPubMed { get; init; }

        public IReadOnlyList<AuthorName> Authors { get; init; } = Array.Empty<AuthorName>();
        public string AuthorsCsv => string.Join(", ", Authors.Select(a => a.ToCsvPart()));
        public string FirstAuthorLast => Authors.Count > 0
            ? (Authors[0].Family ?? Authors[0].LastFromLiteral() ?? "")
            : "";
        public IReadOnlyList<string> Affiliations { get; init; } = Array.Empty<string>();

        public string? AbstractPlain { get; init; }
        public IReadOnlyList<AbstractSection> AbstractSections { get; init; } = Array.Empty<AbstractSection>();

        public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> MeshHeadings { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> PublicationTypes { get; init; } = Array.Empty<string>();
        public string? Language { get; init; }
        public string? Country { get; init; }

        public IReadOnlyList<GrantInfo> Grants { get; init; } = Array.Empty<GrantInfo>();

        public IReadOnlyList<string> ReferencedPmids { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> CommentsCorrections { get; init; } = Array.Empty<string>();

        public int? CitedByCount { get; init; }
        public IReadOnlyList<string> CitedByPmids { get; init; } = Array.Empty<string>();
    }

    public sealed record AuthorName
    {
        public string? Family { get; init; }
        public string? Given { get; init; }
        public string? CollectiveName { get; init; }
        public string? Literal { get; init; }
        public string? Orcid { get; init; }
        public IReadOnlyList<string> Affiliations { get; init; } = Array.Empty<string>();

        public string ToCsvPart()
        {
            if (!string.IsNullOrWhiteSpace(Family))
                return !string.IsNullOrWhiteSpace(Given) ? $"{Family}, {Given}" : Family!;
            if (!string.IsNullOrWhiteSpace(CollectiveName)) return CollectiveName!;
            return Literal ?? "";
        }

        public string? LastFromLiteral()
        {
            if (!string.IsNullOrWhiteSpace(Family)) return Family;
            if (string.IsNullOrWhiteSpace(Literal)) return null;
            var parts = Literal!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? null : parts[^1].Trim(',', '.');
        }
    }

    public sealed record AbstractSection
    {
        public string? Label { get; init; }
        public string? Text { get; init; }
    }

    public sealed record GrantInfo
    {
        public string? GrantId { get; init; }
        public string? Agency { get; init; }
        public string? Country { get; init; }
    }
}
