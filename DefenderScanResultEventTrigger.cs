// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Azure;
using Azure.Identity;
using Azure.Messaging.EventGrid;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;
using System.Net.Http.Headers;
using Vbu.Models;

namespace Vbu.DefenderScanResultEventTrigger
{
    public static class DefenderScanResultEventTrigger
    {
        private const string AntimalwareScanEventType = "Microsoft.Security.MalwareScanningResult";
        private const string MaliciousVerdict = "Malicious";
        private const string MalwareContainer = "malware";

        [FunctionName("DefenderScanResultEventTrigger")]
        public static async Task RunAsync([EventGridTrigger] EventGridEvent eventGridEvent, ILogger log)
        {
            if (eventGridEvent.EventType != AntimalwareScanEventType)
            {
                log.LogInformation("Event type is not an {0} event, event type:{1}", AntimalwareScanEventType, eventGridEvent.EventType);
                return;
            }

            string storageConnection = Environment.GetEnvironmentVariable("StorageConnectionString");
            string containerName = Environment.GetEnvironmentVariable("ContainerName");
            string ProcessFileApiUrl = Environment.GetEnvironmentVariable("ProcessFileApiUrl");

            var storageAccountName = eventGridEvent?.Subject?.Split("/")[^1];
            log.LogInformation("Received new scan result for storage {0}", storageAccountName);
            var decodedEventData = JsonDocument.Parse(eventGridEvent.Data).RootElement.ToString();
            var eventData = JsonDocument.Parse(decodedEventData).RootElement;
            var verdict = eventData.GetProperty("scanResultType").GetString();
            var blobUriString = eventData.GetProperty("blobUri").GetString();

            if (blobUriString.Contains("result-dlq")) {
                log.LogInformation("No need to scan DLQ file event");
                return;
            }

            if (verdict == null || blobUriString == null)
            {
                log.LogError("Event data doesn't contain 'verdict' or 'blobUri' fields");
                throw new ArgumentException("Event data doesn't contain 'verdict' or 'blobUri' fields");
            }

            //Simulate an error
            if (blobUriString.Contains("pdf"))
            {
                log.LogError("Wrong file");
                throw new ArgumentException("Wrong file provided!");
            }

            var blobUri = new Uri(blobUriString);
            var blobUriBuilder = new BlobUriBuilder(blobUri);

            var containerClient = new BlobContainerClient(storageConnection, containerName);
            var blobClient = containerClient.GetBlobClient(blobUriBuilder.BlobName);

            Response<BlobProperties> blobProperties = await blobClient.GetPropertiesAsync();
            
            //Reading blob metadata
            var metadata = blobProperties.Value.Metadata;
            string sourceSystem = "";
            string destinationSystem = "";
            string internalId = "";

            if (!metadata.TryGetValue("sourceSystem", out sourceSystem))
                sourceSystem = "";

            if (!metadata.TryGetValue("destinationSystem", out destinationSystem))
                destinationSystem = "";

            if (!metadata.TryGetValue("internalId", out internalId))
                internalId = "";

            if (verdict == MaliciousVerdict) {
                var message = new DocumentProcessedMessage() {
                    SourceSystem = sourceSystem,
                    InternalId = internalId,
                    DestinationSystem = destinationSystem,
                    Status = "Failed",
                    Reason = "Malware Detected. File has been deleted!"
                };

                string connection = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
                await message.SendToServiceBus(connection, "document-rejected");

                await blobClient.DeleteAsync();

                return;
            }            

            log.LogInformation("Sending file to Process API... to {0}", ProcessFileApiUrl);

            using (var multipartFormContent = new MultipartFormDataContent())
            {
                multipartFormContent.Add(new StringContent(sourceSystem), name: "sourceSystem");
                multipartFormContent.Add(new StringContent(destinationSystem), name: "destinationSystem");
	            multipartFormContent.Add(new StringContent(internalId), name: "internalId");
                multipartFormContent.Add(new StringContent(blobUriString), name: "blobUri");

                var client = new HttpClient();
                var response = client.PostAsync(ProcessFileApiUrl, multipartFormContent).Result;
            }

            log.LogInformation("File successfully sent");

        }

    }
}
