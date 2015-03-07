﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using SlashTodo.Core;

namespace SlashTodo.Infrastructure.Storage.AzureTables.Repositories
{
    public class AccountRepository : Repository<Core.Domain.Account>
    {
        public const string DefaultTableName = "accounts";

        public AccountRepository(CloudStorageAccount storageAccount, IEventDispatcher eventDispatcher)
            : this(storageAccount, eventDispatcher, DefaultTableName)
        {
        }

        public AccountRepository(CloudStorageAccount storageAccount, IEventDispatcher eventDispatcher, string tableName)
            : base(new AzureTableEventStore(storageAccount, tableName), eventDispatcher)
        {
        }
    }
}
