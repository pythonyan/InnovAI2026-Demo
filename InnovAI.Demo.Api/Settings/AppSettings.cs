namespace InnovAI.Demo.Api.Settings;

public class AppSettings
{
    public int MaxTokensPerLine { get; init; } = 300;
    public int MaxTokensPerParagraph { get; init; } = 1000;
    public int OverlapTokens { get; init; } = 100;
    public int MaxRelevantChunks { get; init; } = 5;
    public int MaxInputTokens { get; init; } = 16385;
    public int MaxOutputTokens { get; init; } = 800;
    /// <summary>
    /// Abilita EnsureCreated al primo avvio. Usare solo in sviluppo locale;
    /// lasciare false quando si punta al DB Azure del cliente.
    /// </summary>
    public bool EnsureDbCreated { get; init; } = false;
}
