using ManagePetStore.Models.CustomerModels;
namespace ManagePetStore.Controllers;

public class PetProfilePageViewModel
{
    public ManagePetStore.Models.User User { get; set; } = null!;
    public ManagePetStore.Models.Customer Customer { get; set; } = null!;
    public List<ManagePetStore.Models.Pet> Pets { get; set; } = [];
    public PetFormModel? EditPet { get; set; }
    public bool OpenCreateModal { get; set; }
    public bool OpenEditModal { get; set; }
}

public class PetFormModel
{
    public int? PetId { get; set; }
    public string Name { get; set; } = "";
    public string Species { get; set; } = "";
    public string Breed { get; set; } = "";
    public DateTime? DateOfBirth { get; set; }
    public string? Age { get; set; }
    public decimal Weight { get; set; }
    public string? Pathology { get; set; }
    public string? CurrentImageUrl { get; set; }
}

public class PetMedicalHistoryViewModel
{
    public ManagePetStore.Models.User User { get; set; } = null!;
    public ManagePetStore.Models.Customer Customer { get; set; } = null!;
    public List<ManagePetStore.Models.Pet> AllPets { get; set; } = [];
    public ManagePetStore.Models.Pet SelectedPet { get; set; } = null!;
    public List<ManagePetStore.Models.MedicalRecord> MedicalRecords { get; set; } = [];
}


