﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using NUnit.Framework;
using SlashTodo.Core.Domain;
using SlashTodo.Infrastructure.Configuration;
using SlashTodo.Infrastructure.Messaging;
using SlashTodo.Infrastructure.AzureTables;
using SlashTodo.Infrastructure.AzureTables.Lookups;
using TinyMessenger;

namespace SlashTodo.Infrastructure.Tests.Storage.AzureTables.Lookups
{
    [TestFixture]
    public class AzureTableTodoLookupTests
    {
        private readonly AzureSettings _azureSettings = new AzureSettings(new AppSettings());
        private AzureTableTodoLookup _todoLookup;
        private IMessageBus _bus;

        [SetUp]
        public void BeforeEachTest()
        {
            _bus = new TinyMessageBus(new TinyMessengerHub());
            // Reference a different table for each test to ensure isolation.
            _todoLookup = new AzureTableTodoLookup(
                CloudStorageAccount.Parse(_azureSettings.StorageConnectionString),
                string.Format("test{0}", Guid.NewGuid().ToString("N")));
            _todoLookup.RegisterSubscriptions((ISubscriptionRegistry)_bus);
            var table = GetTable();
            table.CreateIfNotExists();
        }

        [TearDown]
        public void AfterEachTest()
        {
            // Delete the table used by the test that just finished running.
            // Note that according to MSDN, deleting a table can take minutes,
            // so we won't hang around waiting for the result.
            var table = GetTable();
            table.DeleteIfExists();
            _todoLookup.Dispose();
        }
        private CloudTable GetTable()
        {
            var storageAccount = CloudStorageAccount.Parse(_azureSettings.StorageConnectionString);
            var cloudTableClient = storageAccount.CreateCloudTableClient();
            var table = cloudTableClient.GetTableReference(_todoLookup.TableName);
            return table;
        }

        [Test]
        public async Task InsertsNewRowOnTodoAdded()
        {
            // Arrange
            var todoAdded = new TodoAdded
            {
                Id = Guid.NewGuid(),
                SlackConversationId = "slackConversationId",
                ShortCode = "shortCode"
            };

            // Act
            await _bus.Publish(todoAdded);

            // Assert
            var table = GetTable();
            var retrieveOp = TableOperation.Retrieve<LookupAggregateIdByStringTableEntity>(todoAdded.SlackConversationId, todoAdded.ShortCode);
            var row = table.Execute(retrieveOp).Result as LookupAggregateIdByStringTableEntity;
            Assert.That(row, Is.Not.Null);
            Assert.That(row.AggregateId, Is.EqualTo(todoAdded.Id));
        }

        [Test]
        public async Task DeletesRowOnTodoRemoved()
        {
            // Arrange
            var todoId = Guid.NewGuid();
            var slackConversationId = "slackConversationId";
            var shortCode = "shortCode";
            var table = GetTable();
            var insertOp = TableOperation.Insert(new LookupAggregateIdByStringTableEntity(slackConversationId, shortCode, todoId));
            table.Execute(insertOp);
            var todoRemoved = new TodoRemoved
            {
                Id = Guid.NewGuid(),
                SlackConversationId = "slackConversationId",
                ShortCode = "shortCode"
            };

            // Act
            await _bus.Publish(todoRemoved);

            // Assert
            table = GetTable();
            var retrieveOp = TableOperation.Retrieve<LookupAggregateIdByStringTableEntity>(todoRemoved.SlackConversationId, todoRemoved.ShortCode);
            var row = table.Execute(retrieveOp).Result as LookupAggregateIdByStringTableEntity;
            Assert.That(row, Is.Null);
        }

        [Test]
        public async Task ReturnsNullWhenLookupFails()
        {
            // Arrange

            // Act
            var accountId = await _todoLookup.BySlackConversationIdAndShortCode("whatever", "whatever");

            // Assert
            Assert.That(accountId, Is.Null);
        }

        [Test]
        public async Task ReturnsTodoIdWhenLookupIsSuccessful()
        {
            // Arrange
            var expectedTodoId = Guid.NewGuid();
            var slackConversationId = "slackConversationId";
            var shortCode = "shortCode";
            var table = GetTable();
            var insertOp = TableOperation.Insert(new LookupAggregateIdByStringTableEntity(slackConversationId, shortCode, expectedTodoId));
            table.Execute(insertOp);

            // Act
            var todoId = await _todoLookup.BySlackConversationIdAndShortCode(slackConversationId, shortCode);

            // Assert
            Assert.That(todoId, Is.EqualTo(expectedTodoId));
        }
    }
}
