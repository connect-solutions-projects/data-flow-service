using DataFlow.Infrastructure.Integrations;
using DataFlow.Shared.Contracts;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace DataFlow.Infrastructure.Integrations;

public class PrometheusClient : IPrometheusClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public PrometheusClient(HttpClient httpClient, string baseUrl)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<double> GetHttpApiP95Async(string job, string window)
    {
        var promql = $"histogram_quantile(0.95, sum by (le, job) (rate(http_server_request_duration_seconds_bucket{{job=\"{job}\"}}[{window}])))";
        var result = await QueryAsync(promql);
        return result.SingleValue ?? 0.0;
    }

    public async Task<Dictionary<string, double>> GetHttpStatusRateAsync(string job, string window)
    {
        var promql = $"sum by (http_response_status_code) (rate(http_server_request_duration_seconds_count{{job=\"{job}\"}}[{window}]))";
        var result = await QueryAsync(promql);
        return result.Series.ToDictionary(
            s => s.Labels.TryGetValue("http_response_status_code", out var code) ? code : "unknown",
            s => s.Value ?? 0.0
        );
    }

    public async Task<double> GetActiveRequestsAsync(string job)
    {
        var promql = $"sum(http_server_active_requests{{job=\"{job}\"}})";
        var result = await QueryAsync(promql);
        return result.SingleValue ?? 0.0;
    }

    private async Task<PromResult> QueryAsync(string promql)
    {
        var url = $"{_baseUrl}/api/v1/query";
        var form = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("query", promql) });
        using var response = await _httpClient.PostAsync(url, form);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        var resultType = data.GetProperty("resultType").GetString();
        var results = data.GetProperty("result");

        var pr = new PromResult();

        if (resultType == "vector")
        {
            foreach (var item in results.EnumerateArray())
            {
                var metric = item.GetProperty("metric");
                var value = item.GetProperty("value");
                var val = value[1].GetString();
                double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var num);

                var labels = new Dictionary<string, string>();
                foreach (var prop in metric.EnumerateObject())
                {
                    labels[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }

                pr.Series.Add(new PromSeries { Labels = labels, Value = num });
            }

            if (pr.Series.Count == 1)
            {
                pr.SingleValue = pr.Series[0].Value;
            }
        }

        return pr;
    }

    private class PromResult
    {
        public List<PromSeries> Series { get; } = new();
        public double? SingleValue { get; set; }
    }

    private class PromSeries
    {
        public Dictionary<string, string> Labels { get; set; } = new();
        public double? Value { get; set; }
    }
}

