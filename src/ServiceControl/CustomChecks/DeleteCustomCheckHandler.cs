﻿namespace ServiceControl.CustomChecks
{
    using System.Threading.Tasks;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;

    class DeleteCustomCheckHandler : IHandleMessages<DeleteCustomCheck>
    {
        IDocumentStore store;
        IDomainEvents domainEvents;

        public DeleteCustomCheckHandler(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public void Handle(DeleteCustomCheck message)
        {
            HandleAsync(message).GetAwaiter().GetResult();
        }

        private async Task HandleAsync(DeleteCustomCheck message)
        {
            await store.AsyncDatabaseCommands.DeleteAsync(store.Conventions.DefaultFindFullDocumentKeyFromNonStringIdentifier(message.Id, typeof(CustomCheck), false), null)
                .ConfigureAwait(false);

            await domainEvents.Raise(new CustomCheckDeleted {Id = message.Id})
                .ConfigureAwait(false);
        }
    }
}