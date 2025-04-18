using System.Threading.Tasks;


namespace Tsintra.MarketplaceAgent.Interfaces;
/// <summary>
/// Represents a tool that can be executed with input TInput and returns output TOutput.
/// </summary>
/// <typeparam name="TInput">The type of the input for the tool.</typeparam>
/// <typeparam name="TOutput">The type of the output from the tool.</typeparam>
public interface ITool<in TInput, TOutput>
{
    string Name { get; }
    string Description { get; }
    Task<TOutput> RunAsync(TInput input, CancellationToken ct = default);
} 