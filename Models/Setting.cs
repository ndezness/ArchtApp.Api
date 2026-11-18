namespace ArchtApp.Api.Models;

// Business details and the default consultation fee, editable from a Settings page
public class Setting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}