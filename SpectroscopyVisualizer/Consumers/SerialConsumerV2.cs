﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace SpectroscopyVisualizer.Consumers {
    public class SerialConsumerV2<TProduct> :IConsumerV2{
        private readonly BlockingCollection<TProduct> _queue;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly int _waitProducerTimeoutMs;
        private int _continuousFailCnt=0;
        private readonly Func<TProduct, bool> _processFunc;

        /// <summary>Initializes a new instance of the <see cref="T:System.Object" /> class.</summary>
        public SerialConsumerV2(Func<TProduct, bool> processFunc, BlockingCollection<TProduct> queue, int? targetCnt, int waitProducerTimeoutMs) {
            _processFunc = processFunc;
            _waitProducerTimeoutMs = waitProducerTimeoutMs;
            _queue = queue;
            TargetCnt = targetCnt;
        }

        /// <summary>
        ///     The number of elements have been consumed.
        /// </summary>
        public int ConsumedCnt { get; private set; } = 0;
        public int? TargetCnt { get; }

        /// <summary>
        ///     Stop consuming.
        /// </summary>
        public void Stop() {
            _cancellationTokenSource.Cancel();
        }


        /// <summary>
        ///     Start consuming.
        /// </summary>
        public void Start() {
            Task.Run(() => {
                while (!_cancellationTokenSource.IsCancellationRequested) {
                    Thread.Sleep(100);
                    TProduct raw;
                    if (!_queue.TryTake(out raw, _waitProducerTimeoutMs)) {
                        ProducerEmpty?.Invoke();
                        break;
                    }
                    if (_cancellationTokenSource.IsCancellationRequested) return;
                    if (ConsumeElement(raw)) {
                        _continuousFailCnt = 0;
                        ElementConsumedSuccessfully?.Invoke();
                    } else {
                        _continuousFailCnt++;
                        if (_continuousFailCnt >= 10) {
                            SourceInvalid?.Invoke();
                            break;
                        }
                    }
                    ConsumedCnt++;
                    if (ConsumedCnt == TargetCnt) {
                        TargetAmountReached?.Invoke();
                    }
                }
            }, _cancellationTokenSource.Token);
        }

        private bool ConsumeElement(TProduct raw) {
            return _processFunc(raw);
        }

        public event Action SourceInvalid;
        public event Action ElementConsumedSuccessfully;
        public event Action ProducerEmpty;
        public event Action TargetAmountReached;
    }
}
