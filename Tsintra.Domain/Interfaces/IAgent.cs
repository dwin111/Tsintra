namespace Tsintra.Domain.Interfaces
{
    public interface IAgent
    {
        Task<string> GenerateResponseAsync(string prompt);
    }
} 