namespace PostalIdempotencyDemo.Api.Repositories
{
    public interface ISignatureRepository
    {
        Task CreateSignatureAsync(object signature);
    }
}
