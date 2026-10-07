using CatEcommerce.Application.Commands.StoredFile;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.Application.DataMappers;

public static class FileMapper
{
    public static StoredFileRegistrationDto ToDomainDto(this RegisterStoredFileCommand command) =>
        new(command.FileId, command.Purpose, command.ContentType, command.SizeInBytes, command.Sha256,
            command.OriginalFileName, command.StorageKey, command.UploadedBy, command.UploadedOn,
            command.ScannerName, command.RetainUntil, command.UserContext, command.CorrelationId);
}
