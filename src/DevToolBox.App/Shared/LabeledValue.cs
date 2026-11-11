namespace DevToolBox.App.Shared;

/// <summary>A label/value row in a details list (each row gets its own copy button).</summary>
public sealed record LabeledValue(string Label, string Value);
