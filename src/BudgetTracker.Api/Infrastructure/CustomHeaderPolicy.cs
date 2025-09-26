using System.ClientModel.Primitives;

namespace BudgetTracker.Api.Infrastructure;

public class CustomHeaderPolicy : PipelinePolicy
{
    private readonly string _headerName;
    private readonly string _headerValue;

    public CustomHeaderPolicy(string headerName, string headerValue)
    {
        _headerName = headerName;
        _headerValue = headerValue;
    }

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Headers.Add(_headerName, _headerValue);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Headers.Add(_headerName, _headerValue);
        return ProcessNextAsync(message, pipeline, currentIndex);
    }
}