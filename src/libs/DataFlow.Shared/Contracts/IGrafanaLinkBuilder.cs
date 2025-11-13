using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataFlow.Shared.Contracts
{
    public interface IGrafanaLinkBuilder
    {
        Dictionary<string, string> BuildSuggestedLinks(string job);
    }
}
