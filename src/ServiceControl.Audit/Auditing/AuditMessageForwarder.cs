namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using NServiceBus.Extensibility;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

    class AuditMessageForwarder
    {
        private readonly string forwardingAddress;

        public AuditMessageForwarder(Settings settings)
        {
            forwardingAddress = settings.AuditLogQueue;
        }

        public async Task Forward(MessageContext messageContext, IDispatchMessages dispatcher)
        {
            var transportOperations = new TransportOperation[1];
            if (messageContext.Extensions.TryGet("AuditType", out string auditType)
                && auditType != "ProcessedMessage")
            {
                return;
            }

            var outgoingMessage = new OutgoingMessage(
                messageContext.MessageId,
                messageContext.Headers,
                messageContext.Body);

            // Forwarded messages should last as long as possible
            outgoingMessage.Headers.Remove(NServiceBus.Headers.TimeToBeReceived);

            transportOperations[0] = new TransportOperation(outgoingMessage, new UnicastAddressTag(forwardingAddress));

            await dispatcher.Dispatch(
                new TransportOperations(transportOperations),
                messageContext.TransportTransaction,
                messageContext.Extensions
            ).ConfigureAwait(false);
        }

        public async Task VerifyCanReachForwardingAddress(IDispatchMessages dispatcher)
        {
            try
            {
                var transportOperations = new TransportOperations(
                    new TransportOperation(
                        new OutgoingMessage(Guid.Empty.ToString("N"),
                            new Dictionary<string, string>(),
                            new byte[0]),
                        new UnicastAddressTag(forwardingAddress)
                    )
                );

                await dispatcher.Dispatch(transportOperations, new TransportTransaction(), new ContextBag())
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to write to forwarding queue {forwardingAddress}", e);
            }
        }

    }
}