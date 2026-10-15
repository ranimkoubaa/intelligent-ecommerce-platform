using ProductManagement.Models;

namespace ProductManagement.Models.Help
{
    public class ListeCart
    {
        public List<Item> Items { get; private set; }
        public static readonly ListeCart Instance;

        static ListeCart()
        {
            Instance = new ListeCart();
            Instance.Items = new List<Item>();
        }

        protected ListeCart() { }

        public void AddItem(Product prod)
        {
            if (prod == null) return;
            Boolean iswhat = false;
            // Create a new item to add to the cart
            foreach (Item a in Items)
            {
                if (a.Prod != null && a.Prod.ProductId == prod.ProductId)
                {
                    a.quantite++;
                    iswhat = true;
                    return;
                }
            }
            if (iswhat == false)
            {
                Item newItem = new Item(prod);
                newItem.quantite = 1;
                Items.Add(newItem);
            }
        }

        public void setToNUll()
        { }

        public void SetLessOneItem(Product prod)
        {
            if (prod == null) return;
            foreach (Item a in Items)
            {
                if (a.Prod != null && a.Prod.ProductId == prod.ProductId)
                {
                    if (a.quantite <= 1)
                    {
                        RemoveItem(a.Prod);
                        return;
                    }
                    else
                    {
                        a.quantite--;
                        return;
                    }
                }
            }
        }

        public void SetItemQuantity(Product prod, int quantity)
        {
            if (prod == null) return;
            if (quantity <= 0)
            {
                RemoveItem(prod);
                return;
            }
            foreach (Item a in Items)
            {
                if (a.Prod != null && a.Prod.ProductId == prod.ProductId)
                {
                    a.quantite = quantity;
                    return;
                }
            }
        }

        public void RemoveItem(Product prod)
        {
            if (prod == null) return;
            Item? t = null;
            foreach (Item a in Items)
            {
                if (a.Prod != null && a.Prod.ProductId == prod.ProductId)
                {
                    t = a;
                }
            }
            if (t != null)
            {
                Items.Remove(t);
            }
        }

        public float GetSubTotal()
        {
            float subTotal = 0;
            foreach (Item i in Items)
                subTotal += i.TotalPrice;
            return (float)subTotal;
        }
    }
}
