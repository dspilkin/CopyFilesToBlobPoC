using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fnfimageprocessing_AKS_Container_queues
{
    class QueueHelper
    {

        private IConfiguration _config;
        private ILogger<Worker> _logger;


        //-------------------------------------------------
        // Process and remove a message from the queue
        //-------------------------------------------------
        public async Task DequeueMessage(QueueClient queueClient, IConfiguration config, ILogger<Worker> logger, BlobServiceClient blobServiceClient, BlobContainerClient targetContainerClient, ShareDirectoryClient directory)
        {
            // Get the connection string from app settings
            _config = config;
            _logger = logger;

            if (queueClient.Exists())
            {
                // Get the next message
                QueueMessage[] retrievedMessage = await queueClient.ReceiveMessagesAsync(10, TimeSpan.FromMinutes(2));

                _logger.LogInformation($"Receiving Messages : '{retrievedMessage.Count()}' on ThreadId '{System.Threading.Thread.CurrentThread.ManagedThreadId}'");
                int i = 1;
                foreach (var item in retrievedMessage)
                {
                    var eventItem = JsonConvert.DeserializeObject<IndexItem>(item.MessageText);
                    //await copyFiles(eventItem, blobServiceClient, targetContainerClient);
                    var returnValue = await copyFilesDownloadUpload(eventItem, directory, targetContainerClient);
                    // Process (i.e. print) the message in less than 30 seconds
                    if (returnValue)
                    {
                        Console.WriteLine($"Dequeued message #'{i}': '{item.MessageText}' on ThreadId '{System.Threading.Thread.CurrentThread.ManagedThreadId}'");
                        queueClient.DeleteMessage(item.MessageId, item.PopReceipt);
                    }
                    i++;
                };

            }
        }

        /// <summary>
        /// Copy files by downloading the file from azure Files and upload to the Blob. 
        /// </summary>
        /// <param name="eventItem"></param>
        /// <returns></returns>
        public async Task<bool> copyFilesDownloadUpload(IndexItem eventItem, ShareDirectoryClient directory, BlobContainerClient targetContainerClient)
        {
            bool returnValue;
            try
            {

                //var sourceFile = string.Format("{0}/{1}{2}", _config["sourceFileShareContainer"], eventItem.sourceFilePath, _config["sasTokenSource"]);
                //var destinationFile = string.Format("{0}/{1}{2}", _config["destinationBlobContainer"], eventItem.destinationFilePath, _config["sasTokenDestination"]);


                ShareFileClient file = directory.GetFileClient(eventItem.sourceFilePath);

                BlobClient targetBlobClient = targetContainerClient.GetBlobClient(_config["destinationBlobContainerFolder"] + eventItem.destinationFilePath);

                Stopwatch sp = new Stopwatch();
                Stopwatch sp2 = new Stopwatch();

                var guid = Guid.NewGuid();
                // Download the file
                sp.Start();
                ShareFileDownloadInfo download = file.Download();
                sp.Stop();

                InsertResultInSql(eventItem.sourceFilePath, eventItem.destinationFilePath, false, 0, guid, 0, 0);
                sp2.Start();
                var uploadInfo = await targetBlobClient.UploadAsync(download.Content);
                sp2.Stop();


                var time = sp.ElapsedMilliseconds;
                var time2 = sp2.ElapsedMilliseconds;
                InsertResultInSql(eventItem.sourceFilePath, eventItem.destinationFilePath, true, 10, guid, time, time2);

                _logger.LogInformation("Download Time: {0}ms; Upload Time: {1}ms ", time, time2);
                returnValue = true;
            }
            catch (Exception ex)
            {
                returnValue = false;
                Console.WriteLine(ex.Message);
            }

            return returnValue;
        }
        /// <summary>
        /// Copy Files with StartCopyFromUriAsync SDK API call
        /// </summary>
        /// <param name="eventItem"></param>
        /// <returns></returns>
        public async Task<bool> copyFiles(IndexItem eventItem, BlobServiceClient blobServiceClient, BlobContainerClient targetContainerClient)
        {
            var returnValue = false;
            try
            {

                var sourceFile = string.Format("{0}/{1}{2}", _config["sourceFileShareContainer"], eventItem.sourceFilePath, _config["sasTokenSource"]);
                var destinationFile = string.Format("{0}/{1}{2}", _config["destinationBlobContainer"], eventItem.destinationFilePath, _config["sasTokenDestination"]);

                var blobUri = new Uri(sourceFile);

                BlobClient targetBlobClient = targetContainerClient.GetBlobClient(_config["destinationBlobContainerFolder"] + eventItem.destinationFilePath);

                Stopwatch sp = new Stopwatch();
                Stopwatch sp2 = new Stopwatch();
                sp.Start();

                var guid = Guid.NewGuid();

                InsertResultInSql(sourceFile, destinationFile, false, 0, guid, 0, 0);
                var copyOperationResult = await targetBlobClient.StartCopyFromUriAsync(blobUri);
                sp2.Start();
                await copyOperationResult.WaitForCompletionAsync();
                sp2.Stop();
                sp.Stop();

                var time = sp.ElapsedMilliseconds;
                var time2 = sp2.ElapsedMilliseconds;
                InsertResultInSql(sourceFile, destinationFile, copyOperationResult.HasCompleted, copyOperationResult.Value, guid, time, time2);

                _logger.LogInformation("Copied File in: {0}ms with SQL; Just wait for compy completion: {1}ms ", time, time2);
                returnValue = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return returnValue;
        }


        private void InsertResultInSql(string sourceFile, string destinationFile, bool hasCompleted, long numberOfBytes, Guid operationid, long time, long waitCompletionTime)
        {
            using (var connection = new SqlConnection(_config["SqlConnectionString"]))
            {
                using (var command = new SqlCommand("sp_InsertResultsFromCopy", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add(new SqlParameter("@SourceFile", SqlDbType.VarChar)).Value = sourceFile;
                    command.Parameters.Add(new SqlParameter("@DestinationFile", SqlDbType.VarChar)).Value = destinationFile;
                    command.Parameters.Add(new SqlParameter("@HasCompleted", SqlDbType.Bit)).Value = hasCompleted;
                    command.Parameters.Add(new SqlParameter("@NumberOfBytes", SqlDbType.BigInt)).Value = numberOfBytes;
                    command.Parameters.Add(new SqlParameter("@OperationId", SqlDbType.VarChar)).Value = operationid.ToString();
                    command.Parameters.Add(new SqlParameter("@ElapsedTime", SqlDbType.Int)).Value = time;
                    command.Parameters.Add(new SqlParameter("@ElapsedTimeJustForWaitCompletion", SqlDbType.Int)).Value = waitCompletionTime;

                    connection.Open();
                    command.ExecuteNonQuery();
                    connection.Close();
                }
            }
        }
    }
}
