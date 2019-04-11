using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace InfinityLabs.DQueue.Core
{
    public class DirectoryQueue<TObject> : IDisposable
    {
        private const string QUEUE_KEY = "2E07BE5FD50C42B0AE53F2489D7FD9B4";
        private readonly string _queuePath;
        private readonly string _lockPath;
        private readonly int _waitTime;

        private IDisposable _queueLock;

        private bool _initialized;

        private DirectoryInfo _directoryInfo;

        private Queue<DirectoryQueueItem<TObject>> _readQueue;

        public int Count => _readQueue.Count;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="waitTime"></param>
        /// <exception cref="TimeoutException">Occurs when a another process is working with the queue.</exception>
        public DirectoryQueue(string key, int waitTime = 10000)
        {
            _waitTime = waitTime;

            _readQueue = new Queue<DirectoryQueueItem<TObject>>();

            _queuePath = Path.Combine(Path.GetTempPath(), $"DQ_{QUEUE_KEY}", key);
            _lockPath = Path.Combine(_queuePath, "queue.lock");

            _directoryInfo = new DirectoryInfo(_queuePath);
            createDirectoryIfNotExists();

            _queueLock = requestAccess();
        }

        public async Task EnqueueAsync(TObject data)
        {
            var queueItem = new DirectoryQueueItem<TObject>(data);

            await writeAsync(queueItem);
        }

        public async Task<TObject> DequeueAsync()
        {
            if (!_initialized)
            {
                var items = await loadBufferedItemsAsync();
                _readQueue = new Queue<DirectoryQueueItem<TObject>>(items);
                _initialized = true;
            }

            var item = _readQueue.Dequeue();

            await Task.Run(() => File.Delete(item.PathToFile(_queuePath)));

            return item.Data;
        }

        private async Task<DirectoryQueueItem<TObject>[]> loadBufferedItemsAsync()
        {
            var paths = _directoryInfo
                .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                .OrderByDescending(d => d.LastWriteTime)
                .Select(f => f.FullName)
                .ToList();

            var tasks = paths.Select(readAsync).ToList();
            return await Task.WhenAll(tasks);
        }

        private void createDirectoryIfNotExists()
        {
            if (!_directoryInfo.Exists)
            {
                _directoryInfo.Create();
            }
        }

        private async Task writeAsync(DirectoryQueueItem<TObject> data)
        {
            var serialized = JsonConvert.SerializeObject(data);
            var path = data.PathToFile(_queuePath);
            using (var fileStream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fileStream))
            {
                await writer.WriteAsync(serialized);
            }
        }

        private async Task<DirectoryQueueItem<TObject>> readAsync(string path)
        {
            using (var fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var reader = new StreamReader(fileStream))
            {
                var data = await reader.ReadToEndAsync();
                return JsonConvert.DeserializeObject<DirectoryQueueItem<TObject>>(data);
            }
        }

        private IDisposable requestAccess()
        {
            var requestingAccess = true;
            var timer = new Timer(_waitTime);
            timer.Elapsed += (_, __) => timer.Stop();
            timer.Start();

            do
            {
                try
                {
                    return obtainLock();
                }
                catch (IOException) { } // Couldn't get lock on queue

                if (!timer.Enabled)
                {
                    throw new TimeoutException("Couldn't obtain lock on queue");
                }
            } while (requestingAccess);

            throw new InvalidOperationException();
        }

        private Stream obtainLock()
        {
            return File.Open(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        public void Dispose()
        {
            _queueLock.Dispose();
        }
    }
}
