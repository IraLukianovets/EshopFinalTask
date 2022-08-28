using Azure.Storage.Blobs;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.IO;
using System;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace OrdersItemsReserver
{
    public class OrdersItemsReserver
    {
        public static readonly HttpClient httpClient = new HttpClient();

        [FunctionName("OrdersItemsReserver")]
        public void Run([ServiceBusTrigger("ordersqueue", Connection = "ServiceBusConnectionString")] Message inputMessage, ILogger log)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {inputMessage}");
            string inputMessageBody = Encoding.UTF8.GetString(inputMessage.Body);
            var order = JsonConvert.DeserializeObject<OrderItemDetails>(inputMessageBody);

            var blobConnectionString = Environment.GetEnvironmentVariable("BlobConnectionString");
            var blobContainerName = Environment.GetEnvironmentVariable("blobContainerName");

            try
            {
                var containerClient = GetBlobContainerClient(blobContainerName, blobConnectionString);

                string filename = $"order-{order.Id}.json";
                BlobClient blob = containerClient.GetBlobClient(filename);

                using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(inputMessageBody)))
                {
                    blob.Upload(ms);
                }
            }
            catch (Exception ex)
            {
                var logicAppUrl = Environment.GetEnvironmentVariable("LogicAppUrl");
                httpClient.PostAsync(logicAppUrl, new StringContent(inputMessageBody, Encoding.UTF8, "application/json"));
            }
        }

        private static BlobContainerClient GetBlobContainerClient(string containerName, string connectionString)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            return containerClient;
        }
    }
}
