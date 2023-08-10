using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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

            Stream memStream = new MemoryStream();
            var file = req.Form.Files["file"];
            memStream = file.OpenReadStream();

            var sourceSystem = req.Form["sourceSystem"];
            var internalId = req.Form["internalId"];

            log.LogInformation($@"Start processing file received 
                from system {sourceSystem} 
                with internalId {internalId}");
            
            var message = new DocumentProcessedMessage() {
                SourceSystem = sourceSystem,
                InternalId = internalId,
                Status = "Processed"
            };

            string connection = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
            await message.SendToServiceBus(connection);

            return new OkObjectResult($"File {file.FileName} successfully processed");
        }
    }
}
