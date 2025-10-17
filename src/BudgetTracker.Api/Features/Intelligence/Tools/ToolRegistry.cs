using OpenAI.Chat;

namespace BudgetTracker.Api.Features.Intelligence.Tools;

public interface IToolRegistry
{
    IReadOnlyList<IAgentTool> GetAllTools();
    IAgentTool? GetTool(string toolName);
    List<ChatTool> ToChatTools();
}

public class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, IAgentTool> _tools;

    public ToolRegistry(IEnumerable<IAgentTool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, t => t);
    }

    public IReadOnlyList<IAgentTool> GetAllTools()
    {
        return _tools.Values.ToList();
    }

    public IAgentTool? GetTool(string toolName)
    {
        return _tools.TryGetValue(toolName, out var tool) ? tool : null;
    }

    public List<ChatTool> ToChatTools()
    {
        return _tools.Values.Select(tool =>
            ChatTool.CreateFunctionTool(
                functionName: tool.Name,
                functionDescription: tool.Description,
                functionParameters: tool.ParametersSchema
            )
        ).ToList();
    }
}
