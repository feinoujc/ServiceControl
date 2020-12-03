namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure;
    using Monitoring;
    using NServiceBus.Transport;
    using Raven.Client;
    using Raven.Client.Documents;
    using Raven.Client.Documents.BulkInsert;
    using Raven.Client.Json;
    using ServiceControl.SagaAudit;

    class SingleAuditPerister : IDisposable
    {
        private readonly TimeSpan auditRetentionPeriod;
        private BulkInsertOperation bulkInsert;
        readonly Dictionary<string, DateTime> endpointInfoPersistTimes = new Dictionary<string, DateTime>();

        public SingleAuditPerister(IDocumentStore store, TimeSpan auditRetentionPeriod)
        {
            this.auditRetentionPeriod = auditRetentionPeriod;
            bulkInsert = store.BulkInsert();
        }

        public async Task Persist(MessageContext context)
        {
            var knownEndpoints = new Dictionary<string, KnownEndpoint>();

            if (context.Extensions.TryGet(out ProcessedMessage processedMessage)) //Message was an audit message
            {
                if (context.Extensions.TryGet("SendingEndpoint", out EndpointDetails sendingEndpoint))
                {
                    RecordKnownEndpoints(sendingEndpoint, knownEndpoints, processedMessage);
                }

                if (context.Extensions.TryGet("ReceivingEndpoint", out EndpointDetails receivingEndpoint))
                {
                    RecordKnownEndpoints(receivingEndpoint, knownEndpoints, processedMessage);
                }

                await bulkInsert.StoreAsync(processedMessage, GetExpirationMetadata(DateTime.UtcNow))
                    .ConfigureAwait(false);

                using (var stream = Memory.Manager.GetStream(Guid.NewGuid(), processedMessage.Id, context.Body, 0,
                    context.Body.Length))
                {
                    if (processedMessage.MessageMetadata.ContentType != null)
                    {
                        await bulkInsert.AttachmentsFor(processedMessage.Id)
                            .StoreAsync("body", stream, (string)processedMessage.MessageMetadata.ContentType)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await bulkInsert.AttachmentsFor(processedMessage.Id).StoreAsync("body", stream)
                            .ConfigureAwait(false);
                    }
                }
            }
            else if (context.Extensions.TryGet(out SagaSnapshot sagaSnapshot))
            {
                await bulkInsert.StoreAsync(sagaSnapshot, GetExpirationMetadata(DateTime.UtcNow)).ConfigureAwait(false);
            }

            await StoreKnownEndpoints(knownEndpoints).ConfigureAwait(false);
        }

        static void RecordKnownEndpoints(EndpointDetails observedEndpoint, Dictionary<string, KnownEndpoint> observedEndpoints, ProcessedMessage processedMessage)
        {
            var uniqueEndpointId = $"{observedEndpoint.Name}{observedEndpoint.HostId}";
            if (!observedEndpoints.TryGetValue(uniqueEndpointId, out var knownEndpoint))
            {
                knownEndpoint = new KnownEndpoint
                {
                    Host = observedEndpoint.Host,
                    HostId = observedEndpoint.HostId,
                    LastSeen = processedMessage.ProcessedAt,
                    Name = observedEndpoint.Name,
                    Id = KnownEndpoint.MakeDocumentId(observedEndpoint.Name, observedEndpoint.HostId),
                };
                observedEndpoints.Add(uniqueEndpointId, knownEndpoint);
            }

            knownEndpoint.LastSeen = processedMessage.ProcessedAt > knownEndpoint.LastSeen ? processedMessage.ProcessedAt : knownEndpoint.LastSeen;
        }

        async Task StoreKnownEndpoints(Dictionary<string, KnownEndpoint> knownEndpoints)
        {
            foreach (var endpoint in knownEndpoints)
            {
                if (endpointInfoPersistTimes.TryGetValue(endpoint.Key, out var timePersisted)
                    && timePersisted.AddSeconds(30) > DateTime.UtcNow)
                {
                    //Only store endpoint if it has not been stored in last 30 seconds
                    continue;
                }

                await bulkInsert.StoreAsync(
                    endpoint.Value,
                    GetExpirationMetadata(endpoint.Value.LastSeen)
                ).ConfigureAwait(false);

                endpointInfoPersistTimes[endpoint.Key] = DateTime.UtcNow;
            }
        }

        MetadataAsDictionary GetExpirationMetadata(DateTime start)
        {
            return new MetadataAsDictionary
            {
                [Constants.Documents.Metadata.Expires] = start.Add(auditRetentionPeriod)
            };
        }

        public void Dispose()
        {
            bulkInsert?.Dispose();
        }
    }
}