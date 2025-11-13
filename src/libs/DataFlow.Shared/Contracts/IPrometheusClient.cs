using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataFlow.Shared.Contracts
{
    public interface IPrometheusClient
    {
        Task<double> GetHttpApiP95Async(string job, string window);
        Task<Dictionary<string, double>> GetHttpStatusRateAsync(string job, string window);
        Task<double> GetActiveRequestsAsync(string job);
    }
}
