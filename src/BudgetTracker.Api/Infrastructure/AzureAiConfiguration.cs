namespace BudgetTracker.Api.Infrastructure;

public class AzureAiConfiguration
{
    public const string SectionName = "AzureAI";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    // Legacy property for backwards compatibility
    public string DeploymentName { get; set; } = string.Empty;

    // Specific deployment names for different model types
    public string ChatDeploymentName { get; set; } = string.Empty;
    public string EmbeddingDeploymentName { get; set; } = string.Empty;

    public Dictionary<string, string> Headers { get; set; } = new();
}