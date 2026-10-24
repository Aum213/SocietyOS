using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SocietyOS.Services
{
    public interface IIssuePhotoStorageService
    {
        Task<string> SavePhotoAsync(IFormFile file, CancellationToken cancellationToken = default);
        Task<(byte[] FileBytes, string ContentType)?> GetPhotoAsync(string storageKey, CancellationToken cancellationToken = default);
        Task<bool> DeletePhotoAsync(string storageKey, CancellationToken cancellationToken = default);
    }
}