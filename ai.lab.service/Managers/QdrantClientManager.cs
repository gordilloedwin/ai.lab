using ai.lab.service.Managers.Common;
using ai.lab.service.Options;
using ai.lab.service.Services.Common;
using Microsoft.Extensions.Options;

namespace ai.lab.service.Managers;

public class QdrantClientManager : AILabBaseClient, IQdrantClient
{
    public static new string HttpClientName => "QdrantClient";

    public QdrantClientManager(IHttpClientFactory httpClientFactory, IOptionsMonitor<AILabOptions> options, ILogger<QdrantClientManager> logger)
        : base(logger, httpClientFactory)
    {

    }
}
