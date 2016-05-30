﻿using System;
using System.Windows;
using PhaseSonar.Utils;
using JetBrains.Annotations;
using Microsoft.WindowsAPICodePack.Shell.Interop;
using SpectroscopyVisualizer.Consumers;
using SpectroscopyVisualizer.Producers;

namespace SpectroscopyVisualizer.Controllers
{
    public class Scheduler
    {
        public StopWatch Watch { get; } = new StopWatch();

        public Scheduler(IProducer producer, UiConsumer<double[]> consumer)
        {
            Producer = producer;
            Consumer = consumer;
        }

        [NotNull]
        public IProducer Producer { get; protected set; }

        [NotNull]
        public UiConsumer<double[]> Consumer { get; protected set; }

        public void Start()
        {
            Watch.Reset();
            Producer.Produce();
            Consumer.Consume();
        }


        public void Stop()
        {
            Producer.Stop();
            Consumer.Stop();
            /* var timeElapsed = _stopWatch.Reset();

            var historyProductCnt = Producer.HistoryProductCnt;
            var productInQueue = Producer.BlockingQueue.Count;
            var productConsumed = historyProductCnt - productInQueue;

            MessageBox.Show(
                nameof(historyProductCnt) + ": " + historyProductCnt + '\n'
                + nameof(productInQueue) + ": " + productInQueue + '\n'
                + nameof(productConsumed) + ": " + productConsumed + '\n'
                + nameof(timeElapsed) + ": " + timeElapsed
                , "sampling stopped", MessageBoxButton.OK);
*/
            Producer.Reset();
            Consumer.Reset();
        }
    }
}