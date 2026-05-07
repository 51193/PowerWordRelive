using PowerWordRelive.LLMRequester.Core;

namespace PowerWordRelive.LLMRequester.Requests;

public static class RequestRegistry
{
    public static Dictionary<string, IRequest> Build(string llmToken)
    {
        return new Dictionary<string, IRequest>();
    }
}