using System.Globalization;
using System.Text;

namespace DataFlow.ReportingService.Services;

public class ReportGenerator
{
    public async Task<string> GenerateFinalReportAsync(
        double p95DurationSeconds,
        Dictionary<string, double> statusRatesPerSecond,
        double activeRequests,
        Dictionary<string, string> grafanaLinks,
        string? outputDir = null,
        bool isSample = false)
    {
        var ts = DateTimeOffset.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var baseDir = string.IsNullOrWhiteSpace(outputDir) ? Path.Combine("docs", "reports") : outputDir;
        Directory.CreateDirectory(baseDir);
        var fileName = isSample ? $"RELATORIO-FINAL-SAMPLE-{ts}.md" : $"RELATORIO-FINAL-{ts}.md";
        var path = Path.Combine(baseDir, fileName);

        var sb = new StringBuilder();
        sb.AppendLine("# Relatório Final — DataFlow");
        sb.AppendLine();
        sb.AppendLine("## 1) Sumário Executivo");
        sb.AppendLine("- Objetivo: consolidar métricas de observabilidade e estado do sistema.");
        sb.AppendLine("- Resultado: relatório gerado automaticamente com dados do Prometheus.");
        sb.AppendLine();
        sb.AppendLine("## 2) Evidências");
        sb.AppendLine("- Grafana (dashboard): " + (grafanaLinks.TryGetValue("dashboard", out var d) ? d : "(configurar link)"));
        sb.AppendLine("- Painel p95: " + (grafanaLinks.TryGetValue("panel_p95", out var p95Link) ? p95Link : "(configurar link)"));
        sb.AppendLine("- Painel status rate: " + (grafanaLinks.TryGetValue("panel_status_rate", out var rateLink) ? rateLink : "(configurar link)"));
        sb.AppendLine("- Painel active requests: " + (grafanaLinks.TryGetValue("panel_active_requests", out var actLink) ? actLink : "(configurar link)"));
        sb.AppendLine();
        sb.AppendLine("## 3) Métricas-Chave");
        sb.AppendLine($"- p95 duração (s): {p95DurationSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"- requests ativos: {activeRequests.ToString("F0", CultureInfo.InvariantCulture)}");
        sb.AppendLine("- taxa por status (req/s):");
        foreach (var kv in statusRatesPerSecond.OrderBy(k => k.Key))
        {
            sb.AppendLine($"  - {kv.Key}: {kv.Value.ToString("F3", CultureInfo.InvariantCulture)}");
        }
        sb.AppendLine();
        sb.AppendLine("## 4) Conclusão");
        sb.AppendLine("- Preencha com observações sobre estabilidade, performance e erros.");
        sb.AppendLine();
        sb.AppendLine("## 5) Próximos passos");
        sb.AppendLine("- Liste ações de melhoria, monitoramento e operação.");

        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
        return path;
    }
}

