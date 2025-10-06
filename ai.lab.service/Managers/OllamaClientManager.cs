using ai.lab.service.Managers.Common;
using ai.lab.service.Services.Common;

namespace ai.lab.service.Managers;

public class OllamaClientManager : AILabBaseClient, IOllamaClient
{
    protected override string HttpClientName => "OllamaClient";

    public OllamaClientManager(ILogger<OllamaClientManager> logger, IHttpClientFactory httpClientFactory) 
        : base(logger, httpClientFactory)
    {

    }
}
