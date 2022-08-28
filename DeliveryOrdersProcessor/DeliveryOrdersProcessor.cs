using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;

namespace DeliveryOrdersProcessor
{
    public static class DeliveryOrdersProcessor
    {
        [FunctionName("DeliveryOrderProcessor")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function - upload orders to CosmosDB");

            var CosmosUrl = Environment.GetEnvironmentVariable("CosmosUrl");
            var AuthorizationKey = Environment.GetEnvironmentVariable("AuthorizationKey");
            var DatabaseName = Environment.GetEnvironmentVariable("DatabaseName");
            var ContainerName = Environment.GetEnvironmentVariable("ContainerName");

            var responseMessage = String.Empty;

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var order = JsonConvert.DeserializeObject<OrderItemDetails>(requestBody);

                CosmosClient cosmosClient = new CosmosClient(CosmosUrl, AuthorizationKey);
                Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseName);
                Container container = await database.CreateContainerIfNotExistsAsync(ContainerName, "/partitionKeyPath", 400);

                dynamic orderUpload = new { id = Guid.NewGuid().ToString(), partitionKeyPath = order.Id, order };
                await container.CreateItemAsync(orderUpload);
                responseMessage = $"Order {order.Id} uploaded succsessfully to CosmosDB";
            }
            catch (Exception ex)
            {
                responseMessage = ex.Message;
            }

            return new OkObjectResult(responseMessage);
        }
    }
}
