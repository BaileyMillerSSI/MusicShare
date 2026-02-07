namespace MusicShare.Services.Services
{
    public interface IFrontendRevalidateService
    {
        Task RevalidateAsync(RevalidateFrontendRequest request);
    }
}