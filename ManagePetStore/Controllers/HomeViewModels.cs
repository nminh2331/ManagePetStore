namespace ManagePetStore.Controllers;

public class HomepageViewModel
{
    public List<CategoryItem> Categories { get; set; } = [];
    public List<ProductCardItem> BestSellers { get; set; } = [];
    public List<BlogCardItem> BlogArticles { get; set; } = [];
    public FeaturedBlogItem FeaturedBlog { get; set; } = new();
    public List<PetOptionItem> Pets { get; set; } = [];
    public List<RoomTypeOptionItem> RoomTypes { get; set; } = [];
    public int HotelDiscountPercent { get; set; }
    public string HotelMembershipTier { get; set; } = "Thành viên";
    public string? SearchKeyword { get; set; }
    public string? SelectedCategorySlug { get; set; }
    public bool IsFiltered { get; set; }
}

public class CategoryItem
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Slug { get; set; } = "";
}

public class ProductCardItem
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string ImageUrl { get; set; } = "";
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public string? Badge { get; set; }
    public string BadgeType { get; set; } = "discount";
    public bool InStock { get; set; } = true;
}

public class BlogCardItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Excerpt { get; set; } = "";
    public string Tag { get; set; } = "";
    public string ImageUrl { get; set; } = "";
}

public class FeaturedBlogItem
{
    public string Title { get; set; } = "";
    public string Excerpt { get; set; } = "";
    public string ImageUrl { get; set; } = "";
}

public class PetOptionItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Breed { get; set; } = "";
}

public class RoomTypeOptionItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal DailyPrice { get; set; }
}

public class ProductDetailViewModel
{
    public string Sku { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Name { get; set; } = "";
    public string FullTitle { get; set; } = "";
    public decimal Price { get; set; }
    public decimal OriginalPrice { get; set; }
    public int DiscountPercent { get; set; }
    public decimal Savings { get; set; }
    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public string SoldCount { get; set; } = "";
    public string Description { get; set; } = "";
    public int Stock { get; set; }
    public bool InStock { get; set; } = true;
    public List<string> Images { get; set; } = [];
    public List<string> Features { get; set; } = [];
}
