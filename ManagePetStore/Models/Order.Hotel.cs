namespace ManagePetStore.Models;

public partial class Order
{
    public virtual ICollection<HotelCheckoutStatement> HotelCheckoutStatements { get; set; } = [];
}
