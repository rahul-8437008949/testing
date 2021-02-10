using System.Configuration;
using System.Web.Http;
using Microsoft.Azure.KeyVault;
using AskSAPBot.Helpers;
using Autofac;
using AskSAPBot.App_Start;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Connector;
using Microsoft.Azure.KeyVault.Models;

namespace AskSAPBOT
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected async void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
            AskSAPBOT.Models.AuthSettings.Mode = ConfigurationManager.AppSettings["ActiveDirectory.Mode"];
            AskSAPBOT.Models.AuthSettings.EndpointUrl = ConfigurationManager.AppSettings["ActiveDirectory.EndpointUrl"];
            AskSAPBOT.Models.AuthSettings.Tenant = ConfigurationManager.AppSettings["ActiveDirectory.Tenant"];
            AskSAPBOT.Models.AuthSettings.RedirectUrl = ConfigurationManager.AppSettings["ActiveDirectory.RedirectUrl"];
            AskSAPBOT.Models.AuthSettings.ClientId = ConfigurationManager.AppSettings["ActiveDirectory.AppId"];
           
            AskSAPBOT.Models.AuthSettings.Scopes = ConfigurationManager.AppSettings["ActiveDirectory.Scopes"].Split(',');

            var kv = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(KeyVaultHelper.GetToken));
            var sec1 = kv.GetSecretAsync(ConfigurationManager.AppSettings["SecretUri"], "LuisSecretKey").GetAwaiter().GetResult();
            ConfigurationManager.AppSettings["luis:SubscriptionId"] = sec1.Value;
            KeyVaultHelper.LuisSecretKey = ConfigurationManager.AppSettings["luis:SubscriptionId"];

            var sec2 = kv.GetSecretAsync(ConfigurationManager.AppSettings["SecretUri"], "AppSecret").GetAwaiter().GetResult();

            ConfigurationManager.AppSettings["MicrosoftAppPassword"] = sec2.Value;
            AskSAPBOT.Models.AuthSettings.ClientSecret = ConfigurationManager.AppSettings["MicrosoftAppPassword"];
            
            // Register bot modules
            var botDataStorageSecret = await kv.GetSecretAsync(ConfigurationManager.AppSettings["SecretUri"], "BotDataStorageConnectionString");
            var botDataStorageConnectionString = botDataStorageSecret.Value;
            Conversation.UpdateContainer(
                builder =>
                {
                    builder.RegisterModule<LuisConfig>();

                    var store = new TableBotDataStore(botDataStorageConnectionString);

                    builder.Register(c => store)
                        .Keyed<IBotDataStore<BotData>>(AzureModule.Key_DataStore)
                        .AsSelf()
                        .SingleInstance();

                    builder.Register(c => new CachingBotDataStore(store,
                        CachingBotDataStoreConsistencyPolicy
                        .ETagBasedConsistency))
                        .As<IBotDataStore<BotData>>()
                        .AsSelf()
                        .InstancePerLifetimeScope();
                });

        }
    }
}
