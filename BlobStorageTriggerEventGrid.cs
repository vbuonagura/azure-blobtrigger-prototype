using System;
using System.IO;
using System.Net.Http;
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


namespace Vbu.BlobStorageTriggerEventGrid
{
    public class BlobStorageTriggerEventGrid
    {
        [FunctionName("BlobStorageTriggerEventGrid")]
        public void Run([BlobTrigger("vbu-doc/{name}", Source = BlobTriggerSource.EventGrid, Connection = "AzureWebJobsStorage")]Stream myBlob, string name, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");

            if (name.Contains("pdf")) {
                throw new Exception("Invalid file format");
            }

            log.LogInformation("Sending file to Process API...");
            
            using (var multipartFormContent = new MultipartFormDataContent())
            {
                var fileStreamContent = new StreamContent(myBlob);
                fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                multipartFormContent.Add(fileStreamContent, name: "file", fileName: name);

                var client = new HttpClient();
                var response = client.PostAsync("https://vbu-fileupload-app.azurewebsites.net/api/ProcessFile?", multipartFormContent).Result;
            }

            log.LogInformation("File successfully sent");
        }
    }
}
