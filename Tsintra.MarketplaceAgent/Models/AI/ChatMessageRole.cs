namespace Tsintra.MarketplaceAgent.Models.AI // Using root namespace for now
{
    /// <summary>
    /// Defines the possible roles in a chat conversation for AI models.
    /// </summary>
    public enum ChatMessageRole
    {
        System, // Instructions for the model
        User,   // Input from the user
        Assistant // Response from the model
    }
} 