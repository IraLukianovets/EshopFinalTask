using System;
using System.Collections.Generic;

namespace OrdersItemsReserver
{
    public class OrderItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public double UnitPrice { get; set; }
        public double Discount { get; set; }
        public int Units { get; set; }
        public string PictureUrl { get; set; }
    }

    public class OrderItemDetails
    {
        public List<OrderItem> OrderItems { get; set; }
        public ShippingAddress ShipToAddress { get; set; }
        public DateTime OrderDate { get; set; }
        public int Id { get; set; }
        public string Status { get; set; }
        public double Total { get; set; }
    }

    public class ShippingAddress
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public string ZipCode { get; set; }
    }
}
