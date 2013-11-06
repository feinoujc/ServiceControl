﻿namespace ServiceControl.CustomChecks
{
    using EndpointPlugin.Messages.CustomChecks;
    using NServiceBus;
    using Raven.Client;

    class SaveCustomCheckHandler : IHandleMessages<ReportCustomCheckResult>
    {
        public IDocumentStore Store { get; set; }

        public void Handle(ReportCustomCheckResult message)
        {
            using (var session = Store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;

                var customCheck = session.Load<CustomCheck>(message.CustomCheckId) ?? new CustomCheck();

                customCheck.Id = message.CustomCheckId;
                customCheck.Category = message.Category;
                customCheck.Status = message.Result.HasFailed ? Status.Fail : Status.Pass;
                customCheck.ReportedAt = message.ReportedAt;
                customCheck.FailureReason = message.Result.FailureReason;

                session.Store(customCheck);
                session.SaveChanges();
            }
        }
    }
}