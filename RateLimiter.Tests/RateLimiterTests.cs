using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Guava.RateLimiter.Tests
{
    /// <summary>
    /// Copyright (C) 2012 The Guava Authors
    /// 
    /// Licensed under the Apache License, Version 2.0 (the "License");
    /// you may not use this file except in compliance with the License.
    /// You may obtain a copy of the License at
    /// 
    /// http://www.apache.org/licenses/LICENSE-2.0
    /// 
    /// Unless required by applicable law or agreed to in writing, software
    /// distributed under the License is distributed on an "AS IS" BASIS,
    /// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    /// See the License for the specific language governing permissions and
    /// limitations under the License.
    /// </summary>
    [TestFixture]
    public class RateLimiterTests
    {
        private static readonly double Epsilon = 1e-8;

        private void AssertEvents(FakeStopwatch stopwatch, params string[] events)
        {
            Assert.AreEqual(string.Join(", ", events), stopwatch.ReadEventsAndClear());
        }

        [Test]
        public void TestSimple()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, 5.0);
            limiter.Acquire(); // R0.00, since it's the first request
            limiter.Acquire(); // R0.20
            limiter.Acquire(); // R0.20
            AssertEvents(stopwatch, "R0.00", "R0.20", "R0.20");
        }

        [Test]
        public void TestImmediateTryAcquire()
        {
            var r = RateLimiter.Create(1);
            Assert.IsTrue(r.TryAcquire(), "Unable to Acquire initial permit");
            Assert.IsFalse(r.TryAcquire(), "Capable of acquiring secondary permit");
        }

        [Test]
        public void TestDoubleMinValueCanAcquireExactlyOnce()
        {
            var stopwatch = new FakeStopwatch();
            var r = RateLimiter.Create(stopwatch, Epsilon);
            Assert.IsTrue(r.TryAcquire(), "Unable to Acquire initial permit");
            Assert.IsFalse(r.TryAcquire(), "Capable of acquiring an additional permit");
            stopwatch.SleepMillis(int.MaxValue);
            Assert.IsFalse(r.TryAcquire(), "Capable of acquiring an additional permit after sleeping");
            stopwatch.ReadEventsAndClear();
        }

        [Test]
        public void TestSimpleRateUpdate()
        {
            var limiter = RateLimiter.Create(5.0, 5);
            Assert.AreEqual(5.0, limiter.GetRate());
            limiter.SetRate(10.0);
            Assert.AreEqual(10.0, limiter.GetRate());

            Assert.Catch<ArgumentOutOfRangeException>(() => limiter.SetRate(0.0));
            Assert.Catch<ArgumentOutOfRangeException>(() => limiter.SetRate(-10.0));
        }

        [Test]
        public void TestAcquireParameterValidation()
        {
            var limiter = RateLimiter.Create(999);

            Assert.Catch<ArgumentOutOfRangeException>(() => limiter.Acquire(0));
            Assert.Catch<ArgumentOutOfRangeException>(() => limiter.Acquire(-1));
            Assert.Catch<ArgumentOutOfRangeException>(() => limiter.TryAcquire(0));
            Assert.Catch<ArgumentOutOfRangeException>(() => limiter.TryAcquire(-1));
            Assert.Catch<ArgumentOutOfRangeException>(() => limiter.TryAcquire(0, 1, TimeUnit.Seconds));
            Assert.Catch<ArgumentOutOfRangeException>(() => limiter.TryAcquire(-1, 1, TimeUnit.Seconds));
        }

        [Test]
        public void TestSimpleWithWait()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, 5.0);
            limiter.Acquire(); // R0.00
            stopwatch.SleepMillis(200); // U0.20, we are ready for the next request...
            limiter.Acquire(); // R0.00, ...which is granted immediately
            limiter.Acquire(); // R0.20
            AssertEvents(stopwatch, "R0.00", "U0.20", "R0.00", "R0.20");
        }

        [Test]
        public void TestSimpleAcquireReturnValues()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, 5.0);
            Assert.AreEqual(0.0, limiter.Acquire(), Epsilon); // R0.00
            stopwatch.SleepMillis(200); // U0.20, we are ready for the next request...
            Assert.AreEqual(0.0, limiter.Acquire(), Epsilon); // R0.00, ...which is granted immediately
            Assert.AreEqual(0.2, limiter.Acquire(), Epsilon); // R0.20
            AssertEvents(stopwatch, "R0.00", "U0.20", "R0.00", "R0.20");
        }

        [Test]
        public void TestSimpleAcquireEarliestAvailableIsInPast()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, 5.0);
            Assert.AreEqual(0.0, limiter.Acquire(), Epsilon);
            stopwatch.SleepMillis(400);
            Assert.AreEqual(0.0, limiter.Acquire(), Epsilon);
            Assert.AreEqual(0.0, limiter.Acquire(), Epsilon);
            Assert.AreEqual(0.2, limiter.Acquire(), Epsilon);
        }

        [Test]
        public void TestOneSecondBurst()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, 5.0);
            stopwatch.SleepMillis(1000); // max capacity reached
            stopwatch.SleepMillis(1000); // this makes no difference
            limiter.Acquire(1); // R0.00, since it's the first request

            limiter.Acquire(1); // R0.00, from capacity
            limiter.Acquire(3); // R0.00, from capacity
            limiter.Acquire(1); // R0.00, concluding a burst of 5 permits

            limiter.Acquire(); // R0.20, capacity exhausted
            AssertEvents(stopwatch, "U1.00", "U1.00",
                "R0.00", "R0.00", "R0.00", "R0.00", // first request and burst
                "R0.20");
        }

        [Test]
        public void TestCreateWarmupParameterValidation()
        {
            RateLimiter unused;
            unused = RateLimiter.Create(1.0, 1, TimeUnit.Nanoseconds);
            unused = RateLimiter.Create(1.0, 0, TimeUnit.Nanoseconds);

            Assert.Catch<ArgumentOutOfRangeException>(() => RateLimiter.Create(0.0, 1, TimeUnit.Nanoseconds));
            Assert.Catch<ArgumentOutOfRangeException>(() => RateLimiter.Create(1.0, -1, TimeUnit.Nanoseconds));
        }

        [Test]
        public void TestWarmUp()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, 2.0, 4000, TimeUnit.Milliseconds, 3.0);
            for (var i = 0; i < 8; i++)
            {
                limiter.Acquire(); // #1
            }
            stopwatch.SleepMillis(500); // #2: to repay for the last Acquire
            stopwatch.SleepMillis(4000); // #3: becomes cold again
            for (var i = 0; i < 8; i++)
            {
                limiter.Acquire(); // // #4
            }
            stopwatch.SleepMillis(500); // #5: to repay for the last Acquire
            stopwatch.SleepMillis(2000); // #6: didn't get cold! It would take another 2 seconds to go cold
            for (var i = 0; i < 8; i++)
            {
                limiter.Acquire(); // #7
            }
            AssertEvents(stopwatch, "R0.00, R1.38, R1.13, R0.88, R0.63, R0.50, R0.50, R0.50", // #1
                "U0.50", // #2
                "U4.00", // #3
                "R0.00, R1.38, R1.13, R0.88, R0.63, R0.50, R0.50, R0.50", // #4
                "U0.50", // #5
                "U2.00", // #6
                "R0.00, R0.50, R0.50, R0.50, R0.50, R0.50, R0.50, R0.50"); // #7
        }

        [Test]
        public void TestWarmUpWithColdFactor()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, 5.0, 4000, TimeUnit.Milliseconds, 10.0);
            for (int i = 0; i < 8; i++)
            {
                limiter.Acquire(); // #1
            }
            stopwatch.SleepMillis(200); // #2: to repay for the last Acquire
            stopwatch.SleepMillis(4000); // #3: becomes cold again
            for (int i = 0; i < 8; i++)
            {
                limiter.Acquire(); // // #4
            }
            stopwatch.SleepMillis(200); // #5: to repay for the last Acquire
            stopwatch.SleepMillis(1000); // #6: still warm! It would take another 3 seconds to go cold
            for (int i = 0; i < 8; i++)
            {
                limiter.Acquire(); // #7
            }

            AssertEvents(stopwatch, "R0.00, R1.75, R1.26, R0.76, R0.30, R0.20, R0.20, R0.20", // #1
                "U0.20", // #2
                "U4.00", // #3
                "R0.00, R1.75, R1.26, R0.76, R0.30, R0.20, R0.20, R0.20", // #4
                "U0.20", // #5
                "U1.00", // #6
                "R0.00, R0.20, R0.20, R0.20, R0.20, R0.20, R0.20, R0.20"); // #7
        }

        [Test]
        public void TestWarmUpWithColdFactor1()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, 5.0, 4000, TimeUnit.Milliseconds, 1.0);
            for (int i = 0; i < 8; i++)
            {
                limiter.Acquire(); // #1
            }
            stopwatch.SleepMillis(340); // #2
            for (int i = 0; i < 8; i++)
            {
                limiter.Acquire(); // #3
            }
            AssertEvents(stopwatch, "R0.00, R0.20, R0.20, R0.20, R0.20, R0.20, R0.20, R0.20", // #1
                "U0.34", // #2
                "R0.00, R0.20, R0.20, R0.20, R0.20, R0.20, R0.20, R0.20"); // #3
        }

        [Test]
        public void TestWarmUpAndUpdate()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, 2.0, 4000, TimeUnit.Milliseconds, 3.0);
            for (var i = 0; i < 8; i++)
            {
                limiter.Acquire(); // // #1
            }
            stopwatch.SleepMillis(4500); // #2: back to cold state (warmup period + repay last Acquire)
            for (var i = 0; i < 3; i++)
            {
                // only three steps, we're somewhere in the warmup period
                limiter.Acquire(); // #3
            }

            limiter.SetRate(4.0); // double the rate!
            limiter.Acquire(); // #4, we repay the debt of the last Acquire (imposed by the old rate)
            for (var i = 0; i < 4; i++)
            {
                limiter.Acquire(); // #5
            }
            stopwatch.SleepMillis(4250); // #6, back to cold state (warmup period + repay last Acquire)
            for (var i = 0; i < 11; i++)
            {
                limiter.Acquire(); // #7, showing off the warmup starting from totally cold
            }

            // make sure the areas (times) remain the same, while permits are different
            AssertEvents(stopwatch, "R0.00, R1.38, R1.13, R0.88, R0.63, R0.50, R0.50, R0.50", // #1
                "U4.50", // #2
                "R0.00, R1.38, R1.13", // #3, after that the rate changes
                "R0.88", // #4, this is what the throttling would be with the old rate
                "R0.34, R0.28, R0.25, R0.25", // #5
                "U4.25", // #6
                "R0.00, R0.72, R0.66, R0.59, R0.53, R0.47, R0.41", // #7
                "R0.34, R0.28, R0.25, R0.25"); // #7 (cont.), note, this matches #5
        }

        [Test]
        public void TestWarmUpAndUpdateWithColdFactor()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, 5.0, 4000, TimeUnit.Milliseconds, 10.0);
            for (var i = 0; i < 8; i++)
            {
                limiter.Acquire(); // #1
            }
            stopwatch.SleepMillis(4200); // #2: back to cold state (warmup period + repay last Acquire)
            for (var i = 0; i < 3; i++)
            {
                // only three steps, we're somewhere in the warmup period
                limiter.Acquire(); // #3
            }

            limiter.SetRate(10.0); // double the rate!
            limiter.Acquire(); // #4, we repay the debt of the last Acquire (imposed by the old rate)
            for (var i = 0; i < 4; i++)
            {
                limiter.Acquire(); // #5
            }
            stopwatch.SleepMillis(4100); // #6, back to cold state (warmup period + repay last Acquire)
            for (var i = 0; i < 11; i++)
            {
                limiter.Acquire(); // #7, showing off the warmup starting from totally cold
            }

            // make sure the areas (times) remain the same, while permits are different
            AssertEvents(stopwatch, "R0.00, R1.75, R1.26, R0.76, R0.30, R0.20, R0.20, R0.20", // #1
                "U4.20", // #2
                "R0.00, R1.75, R1.26", // #3, after that the rate changes
                "R0.76", // #4, this is what the throttling would be with the old rate
                "R0.20, R0.10, R0.10, R0.10", // #5
                "U4.10", // #6
                "R0.00, R0.94, R0.81, R0.69, R0.57, R0.44, R0.32", // #7
                "R0.20, R0.10, R0.10, R0.10"); // #7 (cont.), note, this matches #5
        }

        [Test]
        public void TestBurstyAndUpdate()
        {
            var stopwatch = new FakeStopwatch();
            var rateLimiter = RateLimiter.Create(stopwatch, 1.0);
            rateLimiter.Acquire(1); // no wait
            rateLimiter.Acquire(1); // R1.00, to repay previous

            rateLimiter.SetRate(2.0); // update the rate!

            rateLimiter.Acquire(1); // R1.00, to repay previous (the previous was under the old rate!)
            rateLimiter.Acquire(2); // R0.50, to repay previous (now the rate takes effect)
            rateLimiter.Acquire(4); // R1.00, to repay previous
            rateLimiter.Acquire(1); // R2.00, to repay previous
            AssertEvents(stopwatch, "R0.00", "R1.00", "R1.00", "R0.50", "R1.00", "R2.00");
        }

        [Test]
        public void TestTryAcquire_noWaitAllowed()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, 5.0);
            Assert.IsTrue(limiter.TryAcquire(timeout: 0, unit: TimeUnit.Seconds));
            Assert.IsFalse(limiter.TryAcquire(timeout: 0, unit: TimeUnit.Seconds));
            Assert.IsFalse(limiter.TryAcquire(timeout: 0, unit: TimeUnit.Seconds));
            stopwatch.SleepMillis(100);
            Assert.IsFalse(limiter.TryAcquire(timeout: 0, unit: TimeUnit.Seconds));
        }

        [Test]
        public void TestTryAcquire_someWaitAllowed()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, 5.0);
            Assert.IsTrue(limiter.TryAcquire(timeout: 0, unit: TimeUnit.Seconds));
            Assert.IsTrue(limiter.TryAcquire(timeout: 200, unit: TimeUnit.Milliseconds));
            Assert.IsFalse(limiter.TryAcquire(timeout: 100, unit: TimeUnit.Milliseconds));
            stopwatch.SleepMillis(100);
            Assert.IsTrue(limiter.TryAcquire(timeout: 100, unit: TimeUnit.Milliseconds));
        }

        [Test]
        public void TestTryAcquire_overflow()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, 5.0);
            Assert.IsTrue(limiter.TryAcquire(timeout: 0, unit: TimeUnit.Microseconds));
            stopwatch.SleepMillis(100);
            Assert.IsTrue(limiter.TryAcquire(timeout: long.MaxValue, unit: TimeUnit.Microseconds));
        }

        [Test]
        public void TestTryAcquire_negative()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, 5.0);
            Assert.IsTrue(limiter.TryAcquire(5, 0, TimeUnit.Seconds));
            stopwatch.SleepMillis(900);
            Assert.IsFalse(limiter.TryAcquire(1, long.MinValue, TimeUnit.Seconds));
            stopwatch.SleepMillis(100);
            Assert.IsTrue(limiter.TryAcquire(1, -1, TimeUnit.Seconds));
        }

        [Test]
        public void TestSimpleWeights()
        {
            var stopwatch = new FakeStopwatch();
            var rateLimiter = RateLimiter.Create(stopwatch, 1.0);
            rateLimiter.Acquire(1); // no wait
            rateLimiter.Acquire(1); // R1.00, to repay previous
            rateLimiter.Acquire(2); // R1.00, to repay previous
            rateLimiter.Acquire(4); // R2.00, to repay previous
            rateLimiter.Acquire(8); // R4.00, to repay previous
            rateLimiter.Acquire(1); // R8.00, to repay previous
            AssertEvents(stopwatch, "R0.00", "R1.00", "R1.00", "R2.00", "R4.00", "R8.00");
        }

        [Test]
        public void TestInfinity_Bursty()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, double.PositiveInfinity);
            limiter.Acquire(int.MaxValue / 4);
            limiter.Acquire(int.MaxValue / 2);
            limiter.Acquire(int.MaxValue);
            AssertEvents(stopwatch, "R0.00", "R0.00", "R0.00"); // no wait, infinite rate!

            limiter.SetRate(2.0);
            limiter.Acquire();
            limiter.Acquire();
            limiter.Acquire();
            limiter.Acquire();
            limiter.Acquire();
            AssertEvents(stopwatch, "R0.00", // First comes the saved-up burst, which defaults to a 1-second burst (2 requests).
                "R0.00",
                "R0.00", // Now comes the free request.
                "R0.50", // Now it's 0.5 seconds per request.
                "R0.50");

            limiter.SetRate(double.PositiveInfinity);
            limiter.Acquire();
            limiter.Acquire();
            limiter.Acquire();
            AssertEvents(stopwatch, "R0.50", "R0.00", "R0.00"); // we repay the last request (.5sec), then back to +oo
        }

        /// <summary>
        /// <see ref="https://code.google.com/p/guava-libraries/issues/detail?id=1791"/>
        /// </summary>
        [Test]
        public void TestInfinity_BustyTimeElapsed()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, double.PositiveInfinity);
            stopwatch.Instant += 1000000;
            limiter.SetRate(2.0);
            for (var i = 0; i < 5; i++)
            {
                limiter.Acquire();
            }
            AssertEvents(stopwatch, "R0.00", // First comes the saved-up burst, which defaults to a 1-second burst (2 requests).
                "R0.00",
                "R0.00", // Now comes the free request.
                "R0.50", // Now it's 0.5 seconds per request.
                "R0.50");
        }

        [Test]
        public void TestInfinity_WarmUp()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, double.PositiveInfinity, 10, TimeUnit.Seconds, 3.0);
            limiter.Acquire(int.MaxValue / 4);
            limiter.Acquire(int.MaxValue / 2);
            limiter.Acquire(int.MaxValue);
            AssertEvents(stopwatch, "R0.00", "R0.00", "R0.00");

            limiter.SetRate(1.0);
            limiter.Acquire();
            limiter.Acquire();
            limiter.Acquire();
            AssertEvents(stopwatch, "R0.00", "R1.00", "R1.00");

            limiter.SetRate(double.PositiveInfinity);
            limiter.Acquire();
            limiter.Acquire();
            limiter.Acquire();
            AssertEvents(stopwatch, "R1.00", "R0.00", "R0.00");
        }

        [Test]
        public void TestInfinity_WarmUpTimeElapsed()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, double.PositiveInfinity, 10, TimeUnit.Seconds, 3.0);
            stopwatch.Instant += 1000000;
            limiter.SetRate(1.0);
            for (var i = 0; i < 5; i++)
            {
                limiter.Acquire();
            }
            AssertEvents(stopwatch, "R0.00", "R1.00", "R1.00", "R1.00", "R1.00");
        }

        /// <summary>
        /// Make sure that bursts can never go above 1-second-worth-of-work for the current
        /// rate, even when we change the rate.
        /// </summary>
        [Test]
        public void TestWeNeverGetABurstMoreThanOneSec()
        {
            var stopwatch = new FakeStopwatch();
            var limiter = RateLimiter.Create(stopwatch, 1.0);
            int[] rates = {1000, 1, 10, 1000000, 10, 1};
            foreach (var rate in rates)
            {
                int oneSecWorthOfWork = rate;
                stopwatch.SleepMillis(rate * 1000);
                limiter.SetRate(rate);
                long burst = MeasureTotalTimeMillis(stopwatch, limiter, oneSecWorthOfWork, new Random());
                // we allow one second worth of work to go in a burst (i.e. take less than a second)
                Assert.IsTrue(burst <= 1000);
                long afterBurst = MeasureTotalTimeMillis(stopwatch, limiter, oneSecWorthOfWork, new Random());
                // but work beyond that must take at least one second
                Assert.IsTrue(afterBurst >= 1000);
            }
        }

        /// <summary>
        /// This neat test shows that no matter what weights we use in our requests, if we push X
        /// amount of permits in a cool state, where X = rate * timeToCoolDown, and we have
        /// specified a timeToWarmUp() period, it will cost as the prescribed amount of time. E.g.,
        /// calling [Acquire(5), Acquire(1)] takes exactly the same time as
        /// [Acquire(2), Acquire(3), Acquire(1)].
        /// </summary>
        [Test]
        public void TestTimeToWarmUpIsHonouredEvenWithWeights()
        {
            var stopwatch = new FakeStopwatch();
            var random = new Random();
            var warmupPermits = 10;
            var coldFactorsToTest = new[] {2.0, 3.0, 10.0};
            var qpsToTest = new[] {4.0, 2.0, 1.0, 0.5, 0.1};
            for (var trial = 0; trial < 100; trial++)
            {
                foreach (var coldFactor in coldFactorsToTest)
                {
                    foreach (var qps in qpsToTest)
                    {
                        // If warmupPermits = maxPermits - thresholdPermits then
                        // warmupPeriod = (1 + coldFactor) * warmupPermits * stableInterval / 2
                        var warmupMillis = (long) ((1 + coldFactor) * warmupPermits / (2.0 * qps) * 1000.0);
                        var rateLimiter = RateLimiter.Create(stopwatch, qps, warmupMillis, TimeUnit.Milliseconds, coldFactor);
                        Assert.AreEqual(warmupMillis, MeasureTotalTimeMillis(stopwatch, rateLimiter, warmupPermits, random));
                    }
                }
            }
        }

        [Test]
        public void TestVerySmallDoubleValues() //throws
        {
            var stopwatch = new FakeStopwatch();
            var rateLimiter = RateLimiter.Create(stopwatch, Epsilon);
            Assert.IsTrue(rateLimiter.TryAcquire(), "Should Acquire initial permit");
            Assert.IsFalse(rateLimiter.TryAcquire(), "Should not Acquire additional permit");
            stopwatch.SleepMillis(5000);
            Assert.IsFalse(rateLimiter.TryAcquire(), "Should not Acquire additional permit even after sleeping");
        }

        private long MeasureTotalTimeMillis(FakeStopwatch stopwatch, RateLimiter rateLimiter, int permits, Random random)
        {
            var startTime = stopwatch.Instant;
            while (permits > 0)
            {
                var nextPermitsToAcquire = Math.Max(1, random.Next(permits));
                permits -= nextPermitsToAcquire;
                rateLimiter.Acquire(nextPermitsToAcquire);
            }
            rateLimiter.Acquire(1); // to repay for any pending debt
            return TimeUnit.Nanoseconds.ToMillis(stopwatch.Instant - startTime);
        }


        /// <summary>
        /// The stopwatch gathers events and presents them as strings.
        /// R0.6 means a delay of 0.6 seconds caused by the(R)ateLimiter
        /// U1.0 means the(U)ser caused the stopwatch to sleep for a second.
        /// </summary>
        private class FakeStopwatch : ISleepingStopwatch
        {
            public long Instant { get; set; }
            private readonly List<string> _events = new List<string>();

            public long ReadMicros()
            {
                return TimeUnit.Nanoseconds.ToMicros(Instant);
            }

            public void SleepMillis(int millis)
            {
                SleepMicros("U", TimeUnit.Milliseconds.ToMicros(millis));
            }

            private void SleepMicros(string caption, long micros)
            {
                Instant += TimeUnit.Microseconds.ToNanos(micros);
                _events.Add($"{caption}{micros / 1000000.0:0.00}");
            }

            public void SleepMicrosUninterruptibly(long micros)
            {
                SleepMicros("R", micros);
            }

            public string ReadEventsAndClear()
            {
                try
                {
                    return ToString();
                }
                finally
                {
                    _events.Clear();
                }
            }

            public override string ToString()
            {
                return string.Join(", ", _events);
            }
        }
    }
}
