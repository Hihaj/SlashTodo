﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SlashTodo.Infrastructure.Configuration
{
    public class AzureSettings : SettingsBase, IAzureSettings
    {
        public string StorageConnectionString
        {
            get { return AppSettings.Get("azure:StorageConnectionString"); }
        }

        public AzureSettings(IAppSettings appSettings)
            : base(appSettings)
        {
        }
    }
}