using System.Threading.Tasks;

namespace NgrokExtensions.Test
{
    public interface IErrorDisplayFunc
    {
        Task ShowError(string msg);
    }
}