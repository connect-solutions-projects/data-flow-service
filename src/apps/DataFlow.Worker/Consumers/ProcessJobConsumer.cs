using DataFlow.Core.Application.Services;
using DataFlow.Shared.Messages;
using MassTransit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataFlow.Worker.Consumers
{
    public class ProcessJobConsumer : IConsumer<ProcessJobMessage>
    {
        private readonly IIngestionOrchestrator _orchestrator;
        public ProcessJobConsumer(IIngestionOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        public async Task Consume(ConsumeContext<ProcessJobMessage> context)
        {
            await _orchestrator.ProcessJobAsync(context.Message.JobId, context.CancellationToken);
        }
    }
}
