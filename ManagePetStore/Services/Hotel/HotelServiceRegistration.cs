using ManagePetStore.Services.Hotel;

namespace ManagePetStore.Services;

public static class HotelServiceRegistration
{
    // [nam] Đăng ký các service nghiệp vụ Hotel/Cage vào dependency injection.
    public static IServiceCollection AddHotelBusinessServices(this IServiceCollection services)
    {
        services.AddScoped<IHotelAvailabilityService, HotelAvailabilityService>();
        services.AddScoped<IHotelBookingService, HotelBookingService>();
        services.AddScoped<IHotelReceptionService, HotelReceptionService>();
        return services;
    }
}
