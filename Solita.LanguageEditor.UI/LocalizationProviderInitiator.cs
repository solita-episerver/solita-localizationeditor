﻿using System;
using System.IO;
using System.Linq;
using System.Web;
using EPiServer.Events.Clients;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.Framework.Localization;
using EPiServer.Framework.Localization.XmlResources;
using EPiServer.Web.Hosting;
using Solita.LanguageEditor.UI.Common;
using log4net;

namespace Solita.LanguageEditor.UI
{
    [InitializableModule]
    //A dependency to EPiServer CMS initialization is needed to be able to use a VPP
    [ModuleDependency(typeof(EPiServer.Web.InitializationModule))] 
    public class LocalizationProviderInitiator : IInitializableModule
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LocalizationProviderInitiator));
        public const string ProviderName = "SolitaVirtualPathLocalizationProvider";

        public void Initialize(InitializationEngine context)
        {
            //Casts the current LocalizationService to a ProviderBasedLocalizationService to get access to the current list of providers
            var localizationService = context.Locate.Advanced.GetInstance<LocalizationService>() as ProviderBasedLocalizationService;
            if (localizationService == null)
            {
                return;
            }
            
            AddProvider(localizationService);
            Event.Get(LocalizationsUpdatedEventHandler.EventId).Raised += LocalizationsUpdatedEventHandler.HandleUpdate;
        }

        private static void AddProvider(ProviderBasedLocalizationService service)
        {
            var langFolderVirtualPath = Settings.AutoPopulated.LangFolderVirtualPath;
            if (string.IsNullOrEmpty(langFolderVirtualPath))
            {
                return;
            }

            var localizationProviderInitializer = new VirtualPathXmlLocalizationProviderInitializer(GenericHostingEnvironment.VirtualPathProvider);

            // Due to what is likely bug in Episerver, there needs to be a physical path with the same
            // name as the virtual path, or else GetInitializedProvider fails since it can't listen to changes in a network folder.
            var physicalDirectory = HttpContext.Current.Server.MapPath(langFolderVirtualPath);
            if (!Directory.Exists(physicalDirectory))
            {
                Directory.CreateDirectory(physicalDirectory);
            }

            try
            {
                //a VPP with the path below must be registered in the sites configuration.
                var localizationProvider = localizationProviderInitializer.GetInitializedProvider(langFolderVirtualPath, ProviderName);
                //Inserts the provider first in the provider list so that it is prioritized over default providers.
                service.Providers.Insert(0, localizationProvider);
            }
            catch(Exception e)
            {
                Log.Error("Failed to add custom localization provider." , e);
            }
        }

        public void Uninitialize(InitializationEngine context)
        {
            //Casts the current LocalizationService to a ProviderBasedLocalizationService to get access to the current list of providers
            var localizationService = context.Locate.Advanced.GetInstance<LocalizationService>() as ProviderBasedLocalizationService;
            if (localizationService == null)
            {
                return;
            }

            RemoveProvider(localizationService);
            Event.Get(LocalizationsUpdatedEventHandler.EventId).Raised -= LocalizationsUpdatedEventHandler.HandleUpdate;
        }

        private static void RemoveProvider(ProviderBasedLocalizationService service)
        {
            //Gets any provider that has the same name as the one initialized.
            var localizationProvider = service.Providers.FirstOrDefault(p => p.Name.Equals(ProviderName, StringComparison.Ordinal));
            if (localizationProvider != null)
            {
                //If found, remove it.
                service.Providers.Remove(localizationProvider);
            }
        }


        public static void ReInitProvider()
        {
            var service = EPiServer.ServiceLocation.ServiceLocator.Current.GetInstance<LocalizationService>() as ProviderBasedLocalizationService;
            RemoveProvider(service);
            AddProvider(service);
        }

        public void Preload(string[] parameters) { }
    }
}