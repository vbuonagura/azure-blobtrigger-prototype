using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Identity;

namespace Vbu.ProcessFile
{
    public static class ProcessFile
    {
        [FunctionName("ProcessFile")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {

            Stream memStream = new MemoryStream();
            var file = req.Form.Files["file"];
            memStream = file.OpenReadStream();

            var sourceSystem = req.Form["sourceSystem"];
            var internalId = req.Form["internalId"];

            log.LogInformation($@"Start processing file received 
                from system {sourceSystem} 
                with internalId {internalId}");
            
            var message = new DocumentProcessedMessage();
            message.sourceSystem = sourceSystem;
            message.internalId = internalId;

            string connection = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
            ServiceBusClient client = new ServiceBusClient(connection);
            ServiceBusSender sender = client.CreateSender("document-processed");
 
            var serializedMessage = JsonSerializer.Serialize(message);
            var serviceBusMessage = new ServiceBusMessage(serializedMessage);
            serviceBusMessage.ApplicationProperties.Add("sourceSystem", message.sourceSystem);

            try
            {
                await sender.SendMessageAsync(serviceBusMessage);
                log.LogInformation($"Messagge {serializedMessage} successfully sent!");
            }
            finally
            {
                await sender.DisposeAsync();
                await client.DisposeAsync();
            }

            return new OkObjectResult($"File {file.FileName} successfully processed");
        }
    }

    public class DocumentProcessedMessage
    {
        public string type;
        public string sourceSystem;
        public string internalId;
    }
}
