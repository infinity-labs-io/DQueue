using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace InfinityLabs.DQueue.Core
{
    public class DirectoryQueue<TObject>
        where TObject : class
    {
        private const string ITEM_EXTENSION = ".dqi";
        private const string QUEUE_KEY = "DQ_2E07BE5FD50C42B0AE53F2489D7FD9B4";
        private readonly string _queuePath;
        private readonly int _timeToWait;
        private readonly bool _lazyInitialize;
        private readonly DirectoryInfo _queueDirectory;

        public int Count
        {
            get
            {
                using(lockQueue())
                {
                    return _queueDirectory
                        .EnumerateFiles($"*{ITEM_EXTENSION}", SearchOption.TopDirectoryOnly)
                        .Count();
                }
            }
        }
        
        public DirectoryQueue(string key, int timeToWait = 1000, bool lazyInitialize = true)
        {
            _queuePath = getQueuePath(key);
            _timeToWait = timeToWait;
            _lazyInitialize = lazyInitialize;

            _queueDirectory = new DirectoryInfo(_queuePath);

            if (!lazyInitialize)
            {
                _queueDirectory.Create();
            }
        }

        public static async Task CleanUpAsync(string key) =>
            await Task.Run(() => Directory.Delete(getQueuePath(key), true));

        public async Task Enqueue(TObject data)
        {
            if (_lazyInitialize)
            {
                await initializeDirectoryAsync();
            }
            await writeAsync(data);
        }

        public async Task<TObject> DequeueAsync()
        {
            if (_lazyInitialize)
            {
                await initializeDirectoryAsync();
            }

            using(lockQueue())
            {
                var path = await getNextFilePathAsync();
                var data = await readAsync(path);
                
                await Task.Run(() => File.Delete(path));
                
                return data;
            }
        }

        private async Task writeAsync(TObject data)
        {
            var path = getRandomFilePath();
            using(var fileStream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using(var writer = new StreamWriter(fileStream))
            {
                var serializedData = JsonConvert.SerializeObject(data);
                await writer.WriteAsync(serializedData);
            }
        }

        private async Task<TObject> readAsync(string path)
        {
            using(var fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            using(var reader = new StreamReader(fileStream))
            {
                var serializedData = await reader.ReadToEndAsync();
                return JsonConvert.DeserializeObject<TObject>(serializedData);
            }
        }

        private async Task initializeDirectoryAsync() =>
            await Task.Run(() => _queueDirectory.Create());

        private async Task<string> getNextFilePathAsync()
        {
            var paths = await getFilePathsAsync();
            var path = paths.FirstOrDefault();
            if (path == null)
            {
                throw new IndexOutOfRangeException("Queue is empty");
            }
            return path;
        }

        private async Task<List<string>> getFilePathsAsync()
        {
            return await Task.Run(() => _queueDirectory
                .EnumerateFiles($"*{ITEM_EXTENSION}", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f.CreationTime)
                .Select(f => f.FullName)
                .ToList());
        }

        private string getRandomFilePath() =>
            Path.Combine(_queuePath, getRandomKey() + ITEM_EXTENSION);

        private string getRandomKey() =>
            Guid.NewGuid().ToString("N").ToUpper();

        private string getMutexKey() =>
            _queuePath.Replace(Path.DirectorySeparatorChar, '_');

        private static string getQueuePath(string key) =>
            Path.Combine(getQueueBasePath(), key);

        private static string getQueueBasePath() =>
            Path.Combine(Path.GetTempPath(), QUEUE_KEY);
        
        private IDisposable lockQueue()
        {
            var timer = new Timer(_timeToWait);
            timer.Elapsed += (_, __) => timer.Stop();
            timer.Start();
            while(timer.Enabled)
            {
                try
                {
                    var path = Path.Combine(_queuePath, "queue.lock");
                    return File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                }
                catch (IOException) {} // Ignore
            }
            throw new TimeoutException("Failed to obtain lock");
        }
    }
}
