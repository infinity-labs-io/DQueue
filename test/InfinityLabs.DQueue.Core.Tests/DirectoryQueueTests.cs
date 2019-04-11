using NUnit.Framework;
using System.Threading.Tasks;
using InfinityLabs.DQueue.Core;
using System.Threading;
using System;
using System.Diagnostics;

namespace InfinityLabs.DQueue.Core.Tests
{
    public class DirectoryQueueTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public async Task TestConcurrency_Success()
        {
            var key = "concurrency_success";
            Assert.DoesNotThrowAsync(() => runGenericTest(key, 2000));
            await DirectoryQueue<SampleObject>.CleanUpAsync(key);
        }

        [Test]
        public async Task TestConcurrency_Fail()
        {
            var key = "concurrency_fail";
            Assert.ThrowsAsync<TimeoutException>(() => runGenericTest(key, 1));
            await DirectoryQueue<SampleObject>.CleanUpAsync(key);
        }

        private async Task runGenericTest(string queueName, int timeout) {
            var tasks = new Task[2];
            var queue1 = new DirectoryQueue<SampleObject>(queueName);
            for (int i = 0; i < 10; i++)
            {
                await queue1.Enqueue(new SampleObject());
            }

            tasks[0] = Task.Run(async () => 
            {
                TestContext.Out.WriteLine("Starting Test 1");


                while (queue1.Count > 0)
                {
                    TestContext.Out.WriteLine($"Count: {queue1.Count}");
                    await queue1.DequeueAsync();
                }
            });

            tasks[1] = Task.Run(async () => {
                TestContext.Out.WriteLine("Starting Test 2");
                var queue = new DirectoryQueue<SampleObject>(queueName, timeout);

                TestContext.Out.WriteLine("Dequeue 1");
                await queue.DequeueAsync();
                TestContext.Out.WriteLine("Dequeue done");
            });

            TestContext.Out.WriteLine("Starting Test");
            await Task.WhenAll(tasks);
        }

        public class SampleObject {}
    }
}