namespace ArchtApp.Api.Models;

public class Stage
{
    public int StageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}