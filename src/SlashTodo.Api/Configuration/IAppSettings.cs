﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlashTodo.Api.Configuration
{
    public interface IAppSettings
    {
        string Get(string key);
    }
}
