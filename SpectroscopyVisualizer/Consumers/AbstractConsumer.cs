﻿using System.Collections.Concurrent;
using System.Threading.Tasks;
using JetBrains.Annotations;
using PhaseSonar.Utils;
using SpectroscopyVisualizer.Producers;

namespace SpectroscopyVisualizer.Consumers
{
    /// <summary>
    ///     A base implementation of <see cref="IConsumer" />
    /// </summary>
    /// <typeparam name="T">The type of elements consumed</typeparam>
    public abstract class AbstractConsumer<T> : IConsumer
    {
        /// <summary>
        ///     The event handler for <see cref="AbstractConsumer{T}.FailEvent" />
        /// </summary>
        /// <param name="sender"></param>
        public delegate void ConsumerFailEventHandler(object sender);

        /// <summary>
        ///     The event handler for <see cref="AbstractConsumer{T}.ConsumeEvent" />
        /// </summary>
        /// <param name="sender"></param>
        public delegate void ElementConsumedEventHandler(object sender);

        /// <summary>
        ///     Create a Consumer.
        /// </summary>
        /// <param name="blockingQueue">The queue which containing elements to be consumed</param>
        protected AbstractConsumer([NotNull] BlockingCollection<T> blockingQueue)
        {
            Toolbox.RequireNonNull(blockingQueue, "blockingQueue");
            BlockingQueue = blockingQueue;
        }

        /// <summary>
        ///     The present number of failures that occurs continuously.
        /// </summary>
        public int ContinuousFailCnt { get; protected set; }

        /// <summary>
        ///     The time to block when the <see cref="BlockingQueue" /> is empty.
        /// </summary>
        public int MillisecondsTimeout { get; set; } = 10000;

        /// <summary>
        ///     The queue containing all items to be consumed.
        /// </summary>
        protected BlockingCollection<T> BlockingQueue { get; }

        /// <summary>
        ///     The state of the consumer, on or off.
        /// </summary>
        protected bool IsOn { get; set; }

        /// <summary>
        ///     Stop consuming.
        /// </summary>
        public void Stop()
        {
            IsOn = false;
            OnStop();
        }

        protected virtual void OnStop()
        {
            
        }

        /// <summary>
        ///     Start consuming.
        /// </summary>
        public abstract void Consume();

        /// <summary>
        ///     The number of elements have been consumed.
        /// </summary>
        public int ConsumedCnt { get; protected set; }

        /// <summary>
        ///     Reset the state of the consumer.
        /// </summary>
        public virtual void Reset()
        {
            ConsumedCnt = 0;
            ContinuousFailCnt = 0;
        }

       

        /// <summary>
        ///     An event fired when consumer failed to consume continuously.
        /// </summary>
        public event ConsumerFailEventHandler FailEvent;

        /// <summary>
        ///     An event fired invoked when an element is consumed successfully.
        /// </summary>
        public event ElementConsumedEventHandler ConsumeEvent;

        /// <summary>
        ///     Fire an <see cref="FailEvent" />
        /// </summary>
        protected void FireFailEvent()
        {
            FailEvent?.Invoke(this);
        }

        /// <summary>
        ///     Fire an <see cref="ConsumeEvent" />
        /// </summary>
        protected void FireConsumeEvent()
        {
            ConsumeEvent?.Invoke(this);
        }

      
    }
}