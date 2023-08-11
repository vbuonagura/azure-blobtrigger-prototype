using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Vbu.FileUpload
{
    public static class FileUpload
    {
        [FunctionName("FileUpload")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string connection = Environment.GetEnvironmentVariable("StorageConnectionString");
            string containerName = Environment.GetEnvironmentVariable("ContainerName");
            
            Stream myBlob = new MemoryStream();
            var file = req.Form.Files["File"];
            myBlob = file.OpenReadStream();
            var containerClient = new BlobContainerClient(connection, containerName);
            var blobClient = containerClient.GetBlobClient(file.FileName);
            
            var blobMetadata = new Dictionary<string, string>();
            blobMetadata.Add("sourceSystem", req.Query["sourceSystem"]);
            blobMetadata.Add("destinationSystem", req.Query["destinationSystem"]);
            blobMetadata.Add("internalId", req.Query["internalId"]);

            var options = new BlobUploadOptions() { Metadata = blobMetadata };

            await blobClient.UploadAsync(myBlob, options);
            
            return new OkObjectResult("File successfully uploaded");

        }
    }
}
