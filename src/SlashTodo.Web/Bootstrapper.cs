﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using Microsoft.WindowsAzure.Storage;
using Nancy;
using Nancy.Authentication.Forms;
using Nancy.Authentication.Stateless;
using Nancy.Bootstrapper;
using Nancy.Cryptography;
using Nancy.Session;
using Nancy.TinyIoc;
using Refit;
using SlashTodo.Infrastructure;
using SlashTodo.Infrastructure.Configuration;
using SlashTodo.Infrastructure.Slack;

namespace SlashTodo.Web
{
    public class Bootstrapper : DefaultNancyBootstrapper
    {
        private readonly IAppSettings _appSettings;
        private readonly CryptographyConfiguration _cryptographyConfiguration;

        protected override CryptographyConfiguration CryptographyConfiguration
        {
            get { return _cryptographyConfiguration; }
        }

        public Bootstrapper(IAppSettings appSettings)
        {
            _appSettings = appSettings;
            
            // Configure the cryptography helpers.
            var keyGenerator = new PassphraseKeyGenerator(
                _appSettings.Get("nancy:CryptographyPassphrase"),
                Encoding.UTF8.GetBytes(_appSettings.Get("nancy:CryptographySalt")));
            _cryptographyConfiguration = new CryptographyConfiguration(
                new RijndaelEncryptionProvider(keyGenerator),
                new DefaultHmacProvider(keyGenerator));
        }

        protected override void ConfigureApplicationContainer(TinyIoCContainer container)
        {
            base.ConfigureApplicationContainer(container);
            container.Register<IAppSettings>(_appSettings);
            container.Register<CryptographyConfiguration>(_cryptographyConfiguration);
            container.Register<IHmacProvider>(_cryptographyConfiguration.HmacProvider);
            container.Register<IEncryptionProvider>(_cryptographyConfiguration.EncryptionProvider);

            var slackSettings = container.Resolve<ISlackSettings>();
            container.Register<ISlackApi>(RestService.For<ISlackApi>(slackSettings.ApiBaseUrl));

            // Register a single CloudStorageAccount instance per application. Also turn off
            // the Nagle algorithm to improve performance.
            // http://blogs.msdn.com/b/windowsazurestorage/archive/2010/06/25/nagle-s-algorithm-is-not-friendly-towards-small-requests.aspx
            var azureSettings = container.Resolve<IAzureSettings>();
            var cloudStorageAccount = CloudStorageAccount.Parse(azureSettings.StorageConnectionString);
            ServicePointManager.FindServicePoint(cloudStorageAccount.TableEndpoint).UseNagleAlgorithm = false;
            container.Register<CloudStorageAccount>(cloudStorageAccount);
        }

        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            base.ApplicationStartup(container, pipelines);

            // Look for views under the current module folder (if any) primarily.
            Conventions.ViewLocationConventions.Insert(0, (viewName, model, context) =>
            {
                return string.Concat(context.ModuleName, "/Views/", viewName);
            });
        }

        protected override void ConfigureRequestContainer(TinyIoCContainer container, NancyContext context)
        {
            base.ConfigureRequestContainer(container, context);
            container.Register<ISession>(context.Request.Session);
            container.Register<IOAuthState, SessionBasedOAuthState>();
        }

        protected override void RequestStartup(TinyIoCContainer container, IPipelines pipelines, NancyContext context)
        {
            base.RequestStartup(container, pipelines, context);
            if (context.Request.Url.Path.StartsWith("/api"))
            {
                // TODO 
                //StatelessAuthentication.Enable(pipelines, new StatelessAuthenticationConfiguration());
            }
            else
            {
                CookieBasedSessions.Enable(pipelines, CryptographyConfiguration);
                FormsAuthentication.Enable(pipelines, new FormsAuthenticationConfiguration
                {
                    CryptographyConfiguration = CryptographyConfiguration,
                    RequiresSSL = true,
                    RedirectUrl = "/login",
                    // TODO UserMapper = ...
                });
            }
        }
    }
}