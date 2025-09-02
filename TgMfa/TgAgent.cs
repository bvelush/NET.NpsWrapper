using System.Threading.Tasks;

namespace TgMfa
{
    public class TgAgent
    {
        public static async Task<bool> AuthenticateUser(string userName)
        {
            var instance = new TgAgent();
            return await instance.Authenticate(userName);
        }

        private async Task<bool> Authenticate(string userName)
        {
            await Task.Yield(); // Simulate async operation
            return true;
        }
    }
}
