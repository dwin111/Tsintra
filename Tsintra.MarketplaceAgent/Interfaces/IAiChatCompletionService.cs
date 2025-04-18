using Tsintra.MarketplaceAgent.Models.AI; // Assuming models are in this namespace
using Tsintra.MarketplaceAgent.Configuration; // Assuming config is in this namespace
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace Tsintra.MarketplaceAgent.Interfaces;
/// <summary>
/// Defines a generic interface for interacting with AI chat completion services.
/// </summary>
/// </summary>
public interface IAiChatCompletionService
{
    /// <summary>
    /// Gets a completion response from the AI model based on the provided messages and options.
    /// </summary>
    /// <param name="messages">The sequence of messages representing the conversation history.</param>
    /// <param name="options">Options to control the generation process.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The generated text content from the AI, or null if an error occurred.</returns>
    Task<string?> GetCompletionAsync(List<AiChatMessage> messages, AiCompletionOptions options, CancellationToken cancellationToken = default);
}