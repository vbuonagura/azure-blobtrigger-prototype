using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Vbu.Models;

namespace Vbu.ProcessFile
{
    public static class ProcessFile
    {
        [FunctionName("ProcessFile")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {

            var sourceSystem = req.Form["sourceSystem"];
            var destinationSystem = req.Form["destinationSystem"];
            var internalId = req.Form["internalId"];
            var blobUriString = req.Form["blobUri"];

            log.LogInformation($@"Start processing file 
                {blobUriString} received 
                from system {sourceSystem} 
                with internalId {internalId}");

            var blobUri = new Uri(blobUriString);
            var blobUriBuilder = new BlobUriBuilder(blobUri);

            string storageConnection = Environment.GetEnvironmentVariable("StorageConnectionString");
            string containerName = Environment.GetEnvironmentVariable("ContainerName");

            var containerClient = new BlobContainerClient(storageConnection, containerName);
            var blobClient = containerClient.GetBlobClient(blobUriBuilder.BlobName);

            //File can be downloaded here to be processed using blobClient

            //Notify external systems via Service Bus
            var message = new DocumentProcessedMessage() {
                SourceSystem = sourceSystem,
                DestinationSystem = destinationSystem,
                InternalId = internalId,
                Status = "Processed",
                Reason = "Document successfully processed"
            };

            string connection = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
            await message.SendToServiceBus(connection, "document-processed");

            return new OkObjectResult($"File {blobUriBuilder.BlobName} successfully processed");
        }
    }
}
