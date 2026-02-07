
namespace MusicShare.Worker.Services
{
    public interface IFrontendRevalidateService
    {
        Task RevalidateAsync(RevalidateFrontendRequest request);
    }
}