﻿namespace ServiceControl.Alerting.Api
{
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.Results;
    using Infrastructure.SignalR;
    using Newtonsoft.Json;
    using Raven.Client;

    public class AlertingController : ApiController
    {
        public AlertingController(IDocumentStore store)
        {
            this.store = store;
        }

        [Route("alerting")]
        [HttpGet]
        public async Task<JsonResult<AlertingSettings>> GetAlertingSettings(HttpRequestMessage request)
        {
            using (var session = store.OpenAsyncSession())
            {
                var settings = await LoadSettings(session).ConfigureAwait(false);

                return new JsonResult<AlertingSettings>(
                    settings,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Include,
                        ContractResolver = new UnderscoreMappingResolver()
                    },
                    Encoding.Unicode,
                    request);
            }
        }

        [Route("alerting")]
        [HttpPost]
        public async Task<HttpResponseMessage> UpdateSettings(UpdateAlertingSettingsRequest request)
        {
            using (var session = store.OpenAsyncSession())
            {
                var settings = await LoadSettings(session).ConfigureAwait(false);

                settings.AlertingEnabled = request.AlertingEnabled;

                settings.SmtpServer = request.SmtpServer;
                settings.SmtpPort = request.SmtpPort;

                settings.AuthenticationAccount = request.AuthorizationAccount;
                settings.AuthenticationPassword = request.AuthorizationPassword;
                settings.EnableSSL = request.EnableSSL;

                await session.SaveChangesAsync().ConfigureAwait(false);

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }

        [Route("alerting/send-test-email")]
        [HttpPost]
        public async Task<HttpResponseMessage> SendTestEmail()
        {
            using (var session = store.OpenAsyncSession())
            {
                var settings = await session.LoadAsync<AlertingSettings>(AlertingSettings.SingleDocumentId).ConfigureAwait(false);

                //TODO: generate email

                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }
        }

        static async Task<AlertingSettings> LoadSettings(IAsyncDocumentSession session)
        {
            var settings = await session.LoadAsync<AlertingSettings>(AlertingSettings.SingleDocumentId).ConfigureAwait(false);

            if (settings == null)
            {
                settings = new AlertingSettings
                {
                    AlertingEnabled = true,
                    AuthenticationEnabled = false,
                    Id = AlertingSettings.SingleDocumentId
                };

                await session.StoreAsync(settings).ConfigureAwait(false);
            }

            return settings;
        }

        IDocumentStore store;
    }
}