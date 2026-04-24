using System.ComponentModel.DataAnnotations;

namespace Budget.Api.Infrastructure.Storage;

public sealed class ProjectDocumentsOptions
{
    public const string SectionName = "ProjectDocuments";

    [Required]
    public string RootPath { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;
}
