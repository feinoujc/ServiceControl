namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Transport;

    class SingleAuditIngestor : IDisposable
    {
        private readonly AuditContextMetadataEnricher metadataEnricher;
        private readonly SingleAuditPerister persister;

        public SingleAuditIngestor(AuditContextMetadataEnricher metadataEnricher, SingleAuditPerister persister)
        {
            this.metadataEnricher = metadataEnricher;
            this.persister = persister;
        }

        public async Task Ingest(MessageContext context)
        {
            await metadataEnricher.ProcessMessage(context)
                .ConfigureAwait(false);

            await persister.Persist(context)
                .ConfigureAwait(false);
        }

        public void Dispose()
        {
            persister?.Dispose();
        }
    }
}