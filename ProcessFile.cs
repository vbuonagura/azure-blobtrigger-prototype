using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Vbu.ProcessFile
{
    public static class ProcessFile
    {
        [FunctionName("ProcessFile")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Start processing file received from Blob Storage");
            
            Stream memStream = new MemoryStream();
            var file = req.Form.Files["file"];
            memStream = file.OpenReadStream();
            
            return new OkObjectResult($"File {file.FileName} successfully processed");
        }
    }
}
