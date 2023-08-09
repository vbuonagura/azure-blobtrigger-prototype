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
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;


namespace Vbu.BlobStorageTriggerEventGrid
{
    public class BlobStorageTriggerEventGrid
    {
        [FunctionName("BlobStorageTriggerEventGrid")]
        public void Run([BlobTrigger("vbu-doc/{blobname}.{blobextension}", Source = BlobTriggerSource.EventGrid, Connection = "AzureWebJobsStorage")]Stream myBlob, 
            string blobName,
            string blobExtension,
            string blobTrigger,
            Uri uri,
            IDictionary<string, string> metaData,
            ILogger log)
        {
            log.LogInformation($@"
                blobName      {blobName}
                blobExtension {blobExtension}
                blobTrigger   {blobTrigger}
                uri           {uri}
                sourceSystem  {metaData["sourceSystem"]}
                internalId    {metaData["internalId"]}");

            if (blobName.Contains("pdf")) {
                throw new Exception("Invalid file format");
            }

            log.LogInformation("Sending file to Process API...");
            
            using (var multipartFormContent = new MultipartFormDataContent())
            {
                var fileStreamContent = new StreamContent(myBlob);
                fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                multipartFormContent.Add(fileStreamContent, name: "file", fileName: blobName);
                multipartFormContent.Add(new StringContent(metaData["sourceSystem"]), name: "sourceSystem");
	            multipartFormContent.Add(new StringContent(metaData["internalId"]), name: "internalId");

                var client = new HttpClient();
                var response = client.PostAsync("https://vbu-fileupload-app.azurewebsites.net/api/ProcessFile?", multipartFormContent).Result;
            }

            log.LogInformation("File successfully sent");
        }
    }
}
