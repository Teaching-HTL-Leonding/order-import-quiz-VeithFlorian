using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

var factory = new OrderImportContextFactory();
using var context = factory.CreateDbContext(args);

switch (args[0])
{
    case "import":
    {
        Import();
        break;
    }
    case "clean":
    {
        Clean();
        break;
    }
    case "check":
    {
        Check();
        break;
    }
    case "full":
    {
        Clean();
        Import();
        Check();
        break;
    }
}

async void Import()
{
    var customers = await File.ReadAllLinesAsync(args[1]);
    var orders = await File.ReadAllLinesAsync(args[2]);
        
    customers.Skip(1).ToList().ForEach(line =>
    {
        var elements = line.Split("\t");
        context.Customers.Add(new Customer() { Name = elements[0], CreditLimit = decimal.Parse(elements[1]) });
    });
        
        
    context.SaveChanges();
        
    orders.Skip(1).ToList().ForEach(line =>
    {
        var elements = line.Split("\t");
        var customer = context.Customers.First(item => item.Name == elements[0]);
        context.Orders.Add(new Order() { CustomerId = customer.Id, Customer = customer, OrderDate = DateTime.Parse(elements[1]), OrderValue = int.Parse(elements[2])});
    });

    context.SaveChanges();
}

void Clean()
{
    var orders = context.Orders;
    foreach (var order in orders)
    {
        context.Orders.Remove(order);    
    }

    var customers = context.Customers;
    foreach (var customer in customers)
    {
        context.Customers.Remove(customer);
    }
    context.SaveChanges();
}

void Check()
{
    var customers = context.Customers;
    context.Orders.GroupBy(item => item.CustomerId)
        .Select(order => new
        {
            CustomerId = order.Key,
            Sum = order.Sum(item => item.OrderValue),
            Limit = customers.First(item => item.Id == order.Key).CreditLimit
        })
        .Where(item => item.Sum > item.Limit)
        .ToList()  
        .ForEach(customer => Console.WriteLine($"{customers.First(item => item.Id == customer.CustomerId).Name}: {customer.Limit - customer.Sum}"));
}






class Customer
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    [Column(TypeName = "decimal(8, 2)")]
    public decimal CreditLimit { get; set; }

    public List<Order> Orders { get; set; }
}


class Order
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public DateTime OrderDate { get; set; }

    [Column(TypeName = "decimal(8, 2)")]
    public decimal OrderValue { get; set; }
}

class OrderImportContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }
    
    public DbSet<Order> Orders { get; set; }

    public OrderImportContext(DbContextOptions<OrderImportContext> options)
        :base(options)
    { }
}


class OrderImportContextFactory : IDesignTimeDbContextFactory<OrderImportContext>
{
    public OrderImportContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        var optionsBuilder = new DbContextOptionsBuilder<OrderImportContext>();
        optionsBuilder
            // Uncomment the following line if you want to print generated
            // SQL statements on the console.
            //.UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))
            .UseSqlServer(configuration["ConnectionStrings:DefaultConnection"]);

        return new OrderImportContext(optionsBuilder.Options);
    }
}
