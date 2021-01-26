using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace fnfimageprocessing_AKS_Container_queues
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _config;

        // Instantiate a QueueClient which will be used to manipulate the queue      
        private static QueueClient queueClient;

        public static SemaphoreSlim semSlim = null;


        private static BlobServiceClient _blobServiceClient = null;
        private static BlobContainerClient _targetContainerClient = null;

        // Get a reference to the file
        private static ShareClient _share = null;
        private static ShareDirectoryClient _directory = null;

        public Worker(ILogger<Worker> logger, IConfiguration iconfiguration)
        {
            _logger = logger;
            _config = iconfiguration;
            queueClient = new QueueClient(_config["StorageQueueConnectionString"], _config["QueueName"]);
            semSlim = new SemaphoreSlim(int.Parse(_config["MaxDegreeParallelism"]));
            
            System.Threading.ThreadPool.SetMinThreads(300, 100);
            
            _blobServiceClient = new BlobServiceClient(_config["DestinationBlobContainerConnectionString"]);
            _targetContainerClient = _blobServiceClient.GetBlobContainerClient("imagemigration");//This is the container where we want to copy the blob

            _share = new ShareClient(_config["SourceFileShareConnectionString"], _config["SourceFileShareName"]);
            _directory = _share.GetDirectoryClient(_config["SourceFileShareDirectory"]);

        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int blockMessage = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                //_logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                //if all hreads are waiting, wait 100ms and continue witht he loop. 
                if (semSlim.CurrentCount == 0)
                {
                    if (blockMessage >= 50)
                    {
                        Console.WriteLine("block");
                        blockMessage = 0;
                    }
                    blockMessage++;
                    await Task.Delay(100);
                    continue;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await semSlim.WaitAsync();
                        QueueHelper helper = new QueueHelper();
                        //there are avaialable threads so lets queue more work. 
                        await helper.DequeueMessage(queueClient, _config, _logger, _blobServiceClient, _targetContainerClient,_directory);
                    }
                    finally { semSlim.Release(); }
                });




            }
        }
    }
}
