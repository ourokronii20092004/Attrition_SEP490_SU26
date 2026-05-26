using System.IO;
using System.Threading.Tasks;

namespace Attrition.API.Services;

public interface IFileStorage
{
    Task<string> SaveAsync(string subfolder, string fileName, Stream stream);
    Task<bool> DeleteAsync(string relativePath);
}
