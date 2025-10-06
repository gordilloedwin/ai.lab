using Polly;

namespace ai.lab.service.Services.Common;

public interface IOllamaClient
{
    IAsyncPolicy<HttpResponseMessage> GetRetryPolicy();
}
