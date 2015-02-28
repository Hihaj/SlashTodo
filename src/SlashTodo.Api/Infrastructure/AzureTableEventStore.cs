﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using SlashTodo.Api.Configuration;
using SlashTodo.Core;
using SlashTodo.Core.Domain;

namespace SlashTodo.Api.Infrastructure
{
    public class AzureTableEventStore : IEventStore
    {
        private readonly string _storageConnectionString;
        private readonly string _tableName;

        public string TableName { get { return _tableName; } }

        public AzureTableEventStore(IAzureSettings settings, string tableName)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }
            if (string.IsNullOrWhiteSpace(settings.StorageConnectionString))
            {
                throw new ArgumentException("Azure table storage connection string missing from configuration.");
            }
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentNullException("tableName");
            }
            _storageConnectionString = settings.StorageConnectionString;
            _tableName = tableName;
        }

        public async Task<IEnumerable<IDomainEvent>> GetById(Guid aggregateId)
        {
            var table = await GetTable();
            var events = GetDomainEventEntities(table, aggregateId)
                .Select(x => x.GetDomainEvent())
                .OrderBy(x => x.OriginalVersion);
            return events;
        }

        public async Task Save(Guid aggregateId, int expectedVersion, IEnumerable<IDomainEvent> events)
        {
            // We don't have to actually use the expected version, since the Azure Table infrastructure
            // will return an error if we try to insert records in the partition with an already existing
            // RowKey (domain event version).

            // Inserting events in batches results in each batch being encapsulated in its own transaction.
            // A batch may include up to 100 records, which in practice should be more than enough. This
            // means that events often (always?) are inserted as an atomic operation.
            var table = await GetTable();
            var orderedEvents = events.OrderBy(x => x.OriginalVersion).ToArray();
            var insertedRows = 0;
            while (insertedRows < orderedEvents.Length)
            {
                var batch = new TableBatchOperation();
                foreach (var @event in orderedEvents.Skip(insertedRows).Take(100))
                {
                    batch.Insert(new DomainEventEntity(@event));
                }
                var result = await table.ExecuteBatchAsync(batch);
                insertedRows += result.Count;
            }
        }

        public async Task Delete(Guid aggregateId)
        {
            var table = await GetTable();
            var entities = GetDomainEventEntities(table, aggregateId).ToArray();
            var deletedRows = 0;
            while (deletedRows < entities.Length)
            {
                var batch = new TableBatchOperation();
                foreach (var entity in entities.Skip(deletedRows).Take(100))
                {
                    batch.Delete(entity);
                }
                var result = await table.ExecuteBatchAsync(batch);
                deletedRows += result.Count;
            }
        }

        private async Task<CloudTable> GetTable()
        {
            var storageAccount = CloudStorageAccount.Parse(_storageConnectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(_tableName);
            await table.CreateIfNotExistsAsync();
            return table;
        }

        private IEnumerable<DomainEventEntity> GetDomainEventEntities(CloudTable table, Guid aggregateId)
        {
            var query = new TableQuery<DomainEventEntity>()
                .Where(TableQuery.GenerateFilterCondition(
                    "PartitionKey",
                    QueryComparisons.Equal,
                    aggregateId.ToString()));
            return table.ExecuteQuery(query);
        }

        public class DomainEventEntity : TableEntity
        {
            public Guid Id { get { return Guid.Parse(PartitionKey); } }
            public int Version { get { return int.Parse(RowKey); } }
            public string DomainEventAssemblyQualifiedName { get; set; }
            public string SerializedDomainEvent { get; set; }

            public DomainEventEntity() { } 

            public DomainEventEntity(IDomainEvent @event)
            {
                PartitionKey = @event.Id.ToString();
                RowKey = @event.OriginalVersion.ToString();
                SerializedDomainEvent = JsonConvert.SerializeObject(@event);
                DomainEventAssemblyQualifiedName = @event.GetType().AssemblyQualifiedName;
            }

            public IDomainEvent GetDomainEvent()
            {
                return (IDomainEvent)JsonConvert.DeserializeObject(SerializedDomainEvent, Type.GetType(DomainEventAssemblyQualifiedName));
            }
        }
    }
}