﻿namespace ServiceControl.Transports.LegacyAzureServiceBus
{
    using NServiceBus;
    using NServiceBus.Settings;
    using NServiceBus.Transport;

    public class ForwardingTopologyAzureServiceBusTransport : AzureServiceBusTransport
    {
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            //If the custom part stays in the connection string and is at the end, the sdk will treat is as part of the SharedAccessKey
            connectionString = ConnectionStringPartRemover.Remove(connectionString, QueueLengthProvider.QueueLengthQueryIntervalPartName);

            var extensions = new TransportExtensions<AzureServiceBusTransport>(settings);
            extensions.UseForwardingTopology();

            return base.Initialize(settings, connectionString);
        }
    }
}