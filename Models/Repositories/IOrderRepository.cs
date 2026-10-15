using ProductManagement.Models;

namespace ProductManagement.Models.Repositories
{
    public interface IOrderRepository
    {
        Order? GetById(int Id);
        void Add(Order o);
        IList<Order> GetAll();
        IQueryable<Order> GetAllOrders();
    }
}
