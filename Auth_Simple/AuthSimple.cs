using System.Threading.Tasks;

namespace Auth_Simple
{
    public class SimpleAuth
    {
        public static async Task<bool> AuthenticateUser(string userName)
        {
            var instance = new SimpleAuth();
            return await instance.Authenticate(userName);
        }

        private async Task<bool> Authenticate(string userName)
        {
            await Task.Yield(); // Simulate async operation
            return true;
        }
    }
}
