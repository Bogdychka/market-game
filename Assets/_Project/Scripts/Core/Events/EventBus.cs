using System;
using System.Collections.Generic;
using UnityEngine;

namespace Market.Core.Events
{
    /// <summary>
    /// Type-safe event bus.
    /// Usage:
    ///   EventBus.Subscribe&lt;MoneyChangedEvent&gt;(OnMoneyChanged);
    ///   EventBus.Publish(new MoneyChangedEvent(100));
    ///   EventBus.Unsubscribe&lt;MoneyChangedEvent&gt;(OnMoneyChanged);
    /// </summary>
    public class EventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new();

        public void Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var existing))
                _handlers[type] = Delegate.Combine(existing, handler);
            else
                _handlers[type] = handler;
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var existing)) return;

            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null) _handlers.Remove(type);
            else _handlers[type] = remaining;
        }

        public void Publish<T>(T @event) where T : IGameEvent
        {
            if (!_handlers.TryGetValue(typeof(T), out var handler)) return;

            // Invoke each subscriber in isolation: one throwing handler must not prevent the
            // remaining subscribers from receiving the event.
            var invocationList = handler.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<T>)invocationList[i]).Invoke(@event);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public void Clear() => _handlers.Clear();
    }
}
