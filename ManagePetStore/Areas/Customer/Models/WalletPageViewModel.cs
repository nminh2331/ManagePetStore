using ManagePetStore.Models;

namespace ManagePetStore.Areas.Customer.Models
{
    public class WalletPageViewModel : CustomerSidebarViewModel
    {
        public Wallet Wallet { get; set; } = null!;
    }
}
