using DataFlow.Shared.Contracts;

namespace DataFlow.Infrastructure.Integrations;

public class GrafanaClient : IGrafanaLinkBuilder
{
    private readonly string _baseUrl;

    public GrafanaClient(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public Dictionary<string, string> BuildSuggestedLinks(string job)
    {
        // Estes links são sugestivos e podem variar conforme UID e organização do dashboard.
        // Ajuste se necessário para apontar ao dashboard provisionado “DataFlow Overview”.
        var dashboard = $"{_baseUrl}/d/dataflow-overview/dataflow-overview";
        return new Dictionary<string, string>
        {
            { "dashboard", dashboard },
            { "panel_p95", $"{dashboard}?viewPanel=1&var-job={job}" },
            { "panel_status_rate", $"{dashboard}?viewPanel=2&var-job={job}" },
            { "panel_active_requests", $"{dashboard}?viewPanel=3&var-job={job}" }
        };
    }
}

