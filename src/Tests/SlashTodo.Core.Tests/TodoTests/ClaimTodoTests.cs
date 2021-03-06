﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using SlashTodo.Core.Domain;
using SlashTodo.Tests.Common;

namespace SlashTodo.Core.Tests.TodoTests
{
    [TestFixture]
    public class ClaimTodoTests
    {
        [Test]
        public void CanClaimTodo()
        {
            // Arrange
            var id = "id";
            var context = TodoTestHelpers.GetContext();
            var slackConversationId = "slackConversationId";
            var shortCode = "x";
            var todo = Todo.Add(id, "text", slackConversationId, shortCode, context);
            todo.ClearUncommittedEvents();
            var before = DateTime.UtcNow;
            var originalVersion = todo.Version;

            // Act
            todo.Claim();

            // Assert
            var @event = todo.GetUncommittedEvents().Single() as TodoClaimed;
            @event.AssertThatBasicDataIsCorrect(id, slackConversationId, shortCode, context, before, expectedOriginalVersion: originalVersion);
        }

        [Test]
        public void ClaimTodoIsIdempotentOperation()
        {
            // Arrange
            var id = "id";
            var context = TodoTestHelpers.GetContext();
            var todo = Todo.Add(id, "text", "slackConversationId", "x", context);
            todo.ClearUncommittedEvents();

            // Act
            todo.Claim();
            todo.Claim();
            todo.Claim();

            // Assert
            Assert.That(todo.GetUncommittedEvents().Count(), Is.EqualTo(1));
        }

        [Test]
        public void CannotClaimTodoThatIsClaimedBySomeoneElseWithoutUsingForce()
        {
            // Arrange
            var id = "id";
            var otherUserId = "otherUserId";
            var otherUserContext = TodoTestHelpers.GetContext(userId: otherUserId);
            var todo = Todo.Add(id, "text", "slackConversationId", "x", otherUserContext);
            todo.Claim();
            todo.ClearUncommittedEvents();
            var userId = "userId";
            Assert.That(userId, Is.Not.EqualTo(otherUserId));

            // Act & assert
            todo.Context = TodoTestHelpers.GetContext(userId: userId);
            CustomAssert.Throws<TodoClaimedBySomeoneElseException>(
                () => todo.Claim(), 
                ex => ex.ClaimedByUserId == otherUserContext.UserId);
        }

        [Test]
        public void CanClaimTodoThatIsClaimedBySomeoneElseWhenUsingForce()
        {
            // Arrange
            var id = "id";
            var otherUserId = "otherUserId";
            var userId = "userId";
            Assert.That(userId, Is.Not.EqualTo(otherUserId));
            var otherUserContext = TodoTestHelpers.GetContext(userId: otherUserId);
            var slackConversationId = "slackConversationId";
            var shortCode = "x";
            var todo = Todo.Add(id, "text", slackConversationId, shortCode, otherUserContext);
            todo.Claim();
            todo.ClearUncommittedEvents();
            var originalVersion = todo.Version;
            var before = DateTime.UtcNow;
            

            // Act
            var context = todo.Context = TodoTestHelpers.GetContext(userId: userId);
            todo.Claim(force: true);

            // Assert
            var @event = todo.GetUncommittedEvents().Single() as TodoClaimed;
            @event.AssertThatBasicDataIsCorrect(id, slackConversationId, shortCode, context, before, expectedOriginalVersion: originalVersion);
        }

        [Test]
        public void ClaimingRemovedTodoDoesNothing()
        {
            // Arrange
            var id = "id";
            var context = TodoTestHelpers.GetContext();
            var todo = Todo.Add(id, "text", "slackConversationId", "x", context);
            todo.Remove();
            todo.ClearUncommittedEvents();

            // Act
            todo.Claim();

            // Assert
            Assert.That(todo.GetUncommittedEvents(), Is.Empty);
        }

        [Test]
        public void ClaimingTickedTodoDoesNothing()
        {
            // Arrange
            var id = "id";
            var context = TodoTestHelpers.GetContext();
            var todo = Todo.Add(id, "text", "slackConversationId", "x", context);
            todo.Tick();
            todo.ClearUncommittedEvents();

            // Act
            todo.Claim();

            // Assert
            Assert.That(todo.GetUncommittedEvents(), Is.Empty);
        }
    }
}
