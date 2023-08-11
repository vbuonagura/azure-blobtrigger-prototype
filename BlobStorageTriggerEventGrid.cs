using System;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;
using System.Net.Http.Headers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Vbu.Models;

namespace Vbu.BlobStorageTriggerEventGrid
{
    public class BlobStorageTriggerEventGrid
    {
        [FunctionName("BlobStorageTriggerEventGrid")]
        public async Task Run([BlobTrigger("vbu-doc/{blobname}.{blobextension}", Source = BlobTriggerSource.EventGrid, Connection = "StorageConnectionString")]Stream myBlob, 
            string blobName,
            string blobExtension,
            string blobTrigger,
            Uri uri,
            IDictionary<string, string> metaData,
            ILogger log)
        {
            string sourceSystem = "";
            string destinationSystem = "";
            string internalId = "";
            if (!metaData.TryGetValue("sourceSystem", out sourceSystem))
                sourceSystem = "";

            if (!metaData.TryGetValue("destinationSystem", out destinationSystem))
                destinationSystem = "";

            if (!metaData.TryGetValue("internalId", out internalId))
                internalId = "";

            log.LogInformation($@"
                blobName      {blobName}
                blobExtension {blobExtension}
                blobTrigger   {blobTrigger}
                uri           {uri}
                sourceSystem  {sourceSystem}
                internalId    {internalId}");

            string storageConnection = Environment.GetEnvironmentVariable("StorageConnectionString");
            string containerName = Environment.GetEnvironmentVariable("ContainerName");

            var containerClient = new BlobContainerClient(storageConnection, containerName);
            var blobClient = containerClient.GetBlobClient($"{blobName}.{blobExtension}");
            
            string malwareScanningResult = "";
            while (string.IsNullOrEmpty(malwareScanningResult))
            {
                Response<GetBlobTagResult> tagsResponse = await blobClient.GetTagsAsync();
                var indexTags = tagsResponse.Value.Tags;
                indexTags.TryGetValue("Malware Scanning scan result", out malwareScanningResult);
            }

            log.LogInformation($"Malware scanning result: {malwareScanningResult}");

            if (malwareScanningResult.Contains("Malicious")) {
                var message = new DocumentProcessedMessage() {
                    SourceSystem = sourceSystem,
                    InternalId = internalId,
                    DestinationSystem = destinationSystem,
                    Status = "Failed",
                    Reason = "Malware Detected"
                };

                string connection = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
                await message.SendToServiceBus(connection, "document-rejected");

                return;
            }

            log.LogInformation("Sending file to Process API...");
            
            using (var multipartFormContent = new MultipartFormDataContent())
            {
                var fileStreamContent = new StreamContent(myBlob);
                fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                multipartFormContent.Add(fileStreamContent, name: "file", fileName: blobName);
                multipartFormContent.Add(new StringContent(sourceSystem), name: "sourceSystem");
                multipartFormContent.Add(new StringContent(destinationSystem), name: "destinationSystem");
	            multipartFormContent.Add(new StringContent(internalId), name: "internalId");

                var client = new HttpClient();
                var response = client.PostAsync("https://vbu-fileupload-win-app.azurewebsites.net/api/ProcessFile?", multipartFormContent).Result;
            }

            log.LogInformation("File successfully sent");
        }
    }
}
