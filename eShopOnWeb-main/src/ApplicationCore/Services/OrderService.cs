using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Azure.Messaging.ServiceBus;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Newtonsoft.Json;
using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    private static readonly HttpClient httpClient = new HttpClient();
    private readonly IConfiguration _config;

    public OrderService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer,
        IConfiguration config)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;
        _config = config;
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.GetBySpecAsync(basketSpec);

        Guard.Against.NullBasket(basketId, basket);
        Guard.Against.EmptyBasketOnCheckout(basket.Items);

        var catalogItemsSpecification = new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        var order = new Order(basket.BuyerId, shippingAddress, items);

        await _orderRepository.AddAsync(order);
        await SendOrderToServiseBus(order);

        var functionUrl = _config.GetValue<string>("FunctionUrls:DeliveryProcessUrl");
        await TriggerHttpFunction(order, functionUrl);
    }

    private async Task SendOrderToServiseBus(Order order)
    {
        var serviseBusConnectionString = _config.GetValue<string>("ConnectionStrings:ServiceBusConnection");
        string queueName = "ordersqueue";
        ServiceBusClient client = new ServiceBusClient(serviseBusConnectionString);
        ServiceBusSender sender = client.CreateSender(queueName);
 
        using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync();
        string messageBody = JsonConvert.SerializeObject(order);

        if (!messageBatch.TryAddMessage(new ServiceBusMessage(messageBody)))
        {
            throw new Exception($"The message is too large to fit in the batch.");
        }
        try
        {
            await sender.SendMessagesAsync(messageBatch);
            Console.WriteLine($"A batch of messages has been published to the queue.");
            var functionUrl = _config.GetValue<string>("FunctionUrls:OrdersItemsReserver");
            await TriggerHttpFunction(order, functionUrl);
        }
        finally
        {
            await sender.DisposeAsync();
            await client.DisposeAsync();
        }
    }

    private async Task TriggerHttpFunction(Order order, string functionUrl)
    {
        var json = JsonConvert.SerializeObject(order);
        var data = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(functionUrl, data);
    }
}
