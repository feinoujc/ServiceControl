namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using BodyStorage;
    using EndpointPlugin.Messages.SagaState;
    using Infrastructure;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using ServiceControl.SagaAudit;
    using JsonSerializer = Newtonsoft.Json.JsonSerializer;

    class AuditContextMetadataEnricher
    {
        static ILog Logger = LogManager.GetLogger<AuditPersister>();
        private readonly BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
        private readonly IEnrichImportedAuditMessages[] enrichers;
        readonly JsonSerializer sagaAuditSerializer = new JsonSerializer();
        private IMessageSession messageSession;
    
        public AuditContextMetadataEnricher(BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher, IEnrichImportedAuditMessages[] enrichers)
        {
            this.bodyStorageEnricher = bodyStorageEnricher;
            this.enrichers = enrichers;
        }

        public void Initialize(IMessageSession messageSession)
        {
            this.messageSession = messageSession;
        }

        public async Task ProcessMessage(MessageContext context)
        {
            if (context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageType)
                && messageType == typeof(SagaUpdatedMessage).FullName)
            {
                ProcessSagaAuditMessage(context);
            }
            else
            {
                await ProcessAuditMessage(context)
                    .ConfigureAwait(false);
            }
        }

        void ProcessSagaAuditMessage(MessageContext context)
        {
            try
            {
                SagaUpdatedMessage message;
                using (var memoryStream = Memory.Manager.GetStream(context.MessageId, context.Body, 0, context.Body.Length))
                using (var streamReader = new StreamReader(memoryStream))
                using (var reader = new JsonTextReader(streamReader))
                {
                    message = sagaAuditSerializer.Deserialize<SagaUpdatedMessage>(reader);
                }

                var sagaSnapshot = SagaSnapshotFactory.Create(message);

                context.Extensions.Set(sagaSnapshot);
                context.Extensions.Set("AuditType", "SagaSnapshot");
            }
            catch (Exception e)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Processing of saga audit message '{context.MessageId}' failed.", e);
                }

                context.GetTaskCompletionSource().TrySetException(e);
            }
        }

        async Task ProcessAuditMessage(MessageContext context)
        {
            if (!context.Headers.TryGetValue(Headers.MessageId, out var messageId))
            {
                messageId = DeterministicGuid.MakeId(context.MessageId).ToString();
            }

            var messageData = new ProcessedMessageData
            {
                MessageId = messageId,
                MessageIntent = context.Headers.MessageIntent()
            };

            var commandsToEmit = new List<ICommand>();
            var enricherContext = new AuditEnricherContext(context.Headers, commandsToEmit, messageData);

            foreach (var enricher in enrichers)
            {
                enricher.Enrich(enricherContext);
            }

            var processingStartedTicks =
                context.Headers.TryGetValue(Headers.ProcessingStarted, out var processingStartedValue)
                    ? DateTimeExtensions.ToUtcDateTime(processingStartedValue).Ticks
                    : DateTime.UtcNow.Ticks;

            var documentId = $"{processingStartedTicks}-{context.Headers.ProcessingId()}";

            bodyStorageEnricher.StoreAuditMessageBody(documentId, context.Body, context.Headers, messageData);

            var auditMessage = new ProcessedMessage(context.Headers, messageData)
            {
                Id = $"ProcessedMessages/{documentId}"
            };

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Emitting {commandsToEmit.Count} commands");
            }
            foreach (var commandToEmit in commandsToEmit)
            {
                await messageSession.Send(commandToEmit)
                    .ConfigureAwait(false);
            }
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"{commandsToEmit.Count} commands emitted.");
            }

            context.Extensions.Set(auditMessage);
            if (messageData.SendingEndpoint != null)
            {
                context.Extensions.Set("SendingEndpoint", messageData.SendingEndpoint);
            }

            if (messageData.ReceivingEndpoint != null)
            {
                context.Extensions.Set("ReceivingEndpoint", messageData.ReceivingEndpoint);
            }

            context.Extensions.Set("AuditType", "ProcessedMessage");
        }
    }
}