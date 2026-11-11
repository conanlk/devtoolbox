namespace DevToolBox.App.Tools;

/// <summary>A tool that can be opened with content (Smart paste), e.g. a token or a JSON document.</summary>
public interface IAcceptsInput
{
    /// <param name="intent">Optional hint from the detector: "decode", "encode", "parse", "compare", "inspect".</param>
    void AcceptInput(string text, string? intent);
}
