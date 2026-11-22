namespace ramii.dev.Core.Entities;

public class LabProject
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TechTags { get; set; } = string.Empty;

    public string? GitHubUrl { get; set; }
    public string? DemoUrl { get; set; }

    public int SortOrder { get; set; } = 0;
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
