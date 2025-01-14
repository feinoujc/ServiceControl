﻿namespace ServiceControl.AcceptanceTesting
{
    using EndpointTemplates;

    public interface ITransportIntegration : IConfigureEndpointTestExecution
    {
        string Name { get; }
        string TypeName { get; }
        string ConnectionString { get; set; }
        string ScrubPlatformConnection(string input);
    }
}