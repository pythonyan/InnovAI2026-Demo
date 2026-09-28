using Microsoft.Extensions.Options;
using Microsoft.ML.Tokenizers;
using InnovAI.Demo.Api.Settings;

namespace InnovAI.Demo.Api.Services;

public class TokenizerService(IOptions<AzureOpenAISettings> settingsOptions)
{
    private readonly TiktokenTokenizer _chatTokenizer =
        TiktokenTokenizer.CreateForModel(settingsOptions.Value.ChatCompletion.ModelId);

    private readonly TiktokenTokenizer _embeddingTokenizer =
        TiktokenTokenizer.CreateForModel(settingsOptions.Value.Embedding.ModelId);

    public int CountChatCompletionTokens(string input) => _chatTokenizer.CountTokens(input);
    public int CountEmbeddingTokens(string input) => _embeddingTokenizer.CountTokens(input);
}
