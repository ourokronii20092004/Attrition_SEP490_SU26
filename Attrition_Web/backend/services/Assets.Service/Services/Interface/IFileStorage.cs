namespace Assets.Service.Services.Interface;

public interface IFileStorage
{
    Task<string> SaveAsync(string subfolder, string fileName, Stream stream);

    Task<bool> DeleteAsync(string relativePath);
}