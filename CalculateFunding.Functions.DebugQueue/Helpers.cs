using Azure.Messaging.ServiceBus;
using CalculateFunding.Common.ServiceBus;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Text;

namespace CalculateFunding.Functions.DebugQueue
{
    public static class Helpers
    {
        public static ServiceBusReceivedMessage ConvertToMessage<T>(string body) where T : class
        {
            QueueMessage<T> queueMessage = null;

            if (body.IsBase64())
            {
                byte[] zippedBytes = Convert.FromBase64String(body);

                using MemoryStream inputStream = new MemoryStream(zippedBytes);
                using GZipStream gZipStream = new GZipStream(inputStream, CompressionMode.Decompress);
                using StreamReader streamReader = new StreamReader(gZipStream);
                string decompressed = streamReader.ReadToEnd();

                queueMessage = JsonConvert.DeserializeObject<QueueMessage<T>>(decompressed);
            }
            else
            {
                queueMessage = JsonConvert.DeserializeObject<QueueMessage<T>>(body);
            }
            
            string data = JsonConvert.SerializeObject(queueMessage.Data);

            byte[] bytes = Encoding.UTF8.GetBytes(data);

            var binaryData = BinaryData.FromString(data);
          
            var properties = new Dictionary<string, object>();

            foreach (KeyValuePair<string, string> property in queueMessage.ApplicationProperties)
            {
                properties.Add(property.Key, property.Value);
            }

            ServiceBusReceivedMessage? serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
              body: binaryData,
              properties : properties,
              messageId : Guid.NewGuid().ToString()
              );

            return serviceBusReceivedMessage;
        }
     
    }
}
