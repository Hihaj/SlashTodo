﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SlashTodo.Core.Domain
{
    public abstract class TodoEvent : ITodoEvent
    {
        public Guid Id { get; set; }
        public int OriginalVersion { get; set; }
        public string UserId { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class TodoAdded : TodoEvent
    {
        public string Text { get; set; }
    }

    public class TodoRemoved : TodoEvent
    {
    }

    public class TodoTicked : TodoEvent
    {
    }

    public class TodoUnticked : TodoEvent
    {
    }

    public class TodoClaimed : TodoEvent
    {
    }

    public class TodoFreed : TodoEvent
    {
    }
}
