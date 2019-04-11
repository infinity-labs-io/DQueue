using System;
using System.IO;

namespace InfinityLabs.DQueue.Core
{
    internal class DirectoryQueueItem<TObject>
    {
        public Guid Id { get; set; }

        public TObject Data { get; set; }

        public DirectoryQueueItem(TObject data)
        {
            Id = Guid.NewGuid();
            Data = data;
        }

        public string PathToFile(string queueLocation)
        {
            var key = Id.ToString("N").ToUpper();
            return Path.Combine(queueLocation, key + ".qbf");
        }
    }
}
