namespace PostalIdempotencyDemo.Api.Repositories
{
    public interface IDeliveryRepository
    {
        Task CreateDeliveryAsync(object delivery);
    }

    // ...existing code...
}
