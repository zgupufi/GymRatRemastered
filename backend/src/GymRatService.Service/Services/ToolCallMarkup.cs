namespace GymRatService.Service.Services;

public static class ToolCallMarkup
{
    private static readonly string[] Markers = ["<tool_call", "<function=", "<invoke", ":tool_call>"];

    public static bool IsPresentIn(string text) =>
        Markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
