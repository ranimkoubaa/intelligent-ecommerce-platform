using Microsoft.EntityFrameworkCore;

namespace ProductManagement.Models.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        readonly AppDbContext context;

        public OrderRepository(AppDbContext context)
        {
            this.context = context;
        }

        public void Add(Order o)
        {
            context.Orders.Add(o);
            context.SaveChanges();
        }

        public Order? GetById(int id)
        {
            return context.Orders
                .Include(o => o.Items)
                .FirstOrDefault(o => o.Id == id);
        }

        public IList<Order> GetAll()
        {
            return context.Orders
                .Include(o => o.Items)
                .OrderByDescending(o => o.OrderDate)
                .ToList();
        }

        public IQueryable<Order> GetAllOrders()
        {
            return context.Orders
                .Include(o => o.Items)
                .AsQueryable();
        }
    }
}
