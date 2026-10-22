using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SocietyOS.Services
{
    public class VisitorPhotoStorageService : IVisitorPhotoStorageService
    {
        private readonly string _storageFolder;
        private readonly ILogger<VisitorPhotoStorageService> _logger;
        private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

        // Magic byte definitions for allowed image types
        private static readonly Dictionary<string, List<byte[]>> AllowedSignatures = new()
        {
            { "image/jpeg", new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } } },
            { "image/png",  new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47 } } },
            { "image/webp", new List<byte[]> { new byte[] { 0x52, 0x49, 0x46, 0x46 } } } // RIFF header
        };

        public VisitorPhotoStorageService(IWebHostEnvironment env, ILogger<VisitorPhotoStorageService> logger)
        {
            _logger = logger;
            // Place outside wwwroot so photos are completely inaccessible via direct HTTP URLs
            _storageFolder = Path.Combine(env.ContentRootPath, "App_Data", "ProtectedVisitorPhotos");

            if (!Directory.Exists(_storageFolder))
            {
                Directory.CreateDirectory(_storageFolder);
            }
        }

        public async Task<string> SavePhotoAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("A visitor photo is required.");

            if (file.Length > MaxFileSize)
                throw new InvalidOperationException("Photo exceeds the maximum allowed size of 5MB.");

            // Read file header and validate magic bytes
            using (var stream = file.OpenReadStream())
            {
                var headerBytes = new byte[8];
                var bytesRead = await stream.ReadAsync(headerBytes.AsMemory(0, headerBytes.Length), cancellationToken);

                var isValid = false;
                foreach (var signatures in AllowedSignatures.Values)
                {
                    foreach (var signature in signatures)
                    {
                        if (bytesRead >= signature.Length && headerBytes.AsSpan(0, signature.Length).SequenceEqual(signature))
                        {
                            isValid = true;
                            break;
                        }
                    }
                    if (isValid) break;
                }

                if (!isValid)
                    throw new InvalidOperationException("Invalid image format. Only authentic JPEG, PNG, and WebP images are permitted.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png" && extension != ".webp")
            {
                extension = ".jpg";
            }

            var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
            var destinationPath = Path.Combine(_storageFolder, uniqueFileName);

            using (var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(destinationStream, cancellationToken);
            }

            return uniqueFileName;
        }

        public async Task<(byte[] FileBytes, string ContentType)?> GetPhotoAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(storageKey)) return null;

            var safeFileName = Path.GetFileName(storageKey);
            var filePath = Path.Combine(_storageFolder, safeFileName);

            if (!File.Exists(filePath)) return null;

            var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var contentType = ext switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };

            return (bytes, contentType);
        }

        public Task<bool> DeletePhotoAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(storageKey)) return Task.FromResult(true);

            try
            {
                var safeFileName = Path.GetFileName(storageKey);
                var filePath = Path.Combine(_storageFolder, safeFileName);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete visitor photo file from disk.");
                return Task.FromResult(false);
            }
        }
    }
}