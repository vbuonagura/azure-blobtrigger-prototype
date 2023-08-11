using Azure.Messaging.ServiceBus;
using System.Text.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Vbu.Models
{
    public class DocumentProcessedMessage
    {
        public string Status { get; set; }
        public string SourceSystem { get; set; }
        public string DestinationSystem { get; set; }
        public string InternalId { get; set; }
        public string Reason { get; set; }

        public async Task SendToServiceBus(string connection, string topic)
        {
            ServiceBusClient client = new ServiceBusClient(connection);
            ServiceBusSender sender = client.CreateSender(topic);
 
            var serializedMessage = JsonSerializer.Serialize(this);
            var serviceBusMessage = new ServiceBusMessage(serializedMessage);
            serviceBusMessage.ApplicationProperties.Add("sourceSystem", this.SourceSystem);
            serviceBusMessage.ApplicationProperties.Add("destinationSystem", this.DestinationSystem);

            try
            {
                await sender.SendMessageAsync(serviceBusMessage);
            }
            finally
            {
                await sender.DisposeAsync();
                await client.DisposeAsync();
            }
        }
    }
}