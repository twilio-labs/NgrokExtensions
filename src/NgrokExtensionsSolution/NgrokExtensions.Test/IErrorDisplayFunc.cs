using System.Threading.Tasks;

namespace NgrokExtensions.Test
{
    public interface IErrorDisplayFunc
    {
        Task ShowErrorAsync(string msg);
    }
}