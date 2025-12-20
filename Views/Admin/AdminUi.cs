using System.Threading.Tasks;
using AmoraApp.Services;

namespace AmoraApp.Views.Admin
{
    public static class AdminUi
    {
        public static async Task<bool> GuardAsync()
        {
            var uid = FirebaseAuthService.Instance.CurrentUserUid;
            if (!AdminAccessService.IsAdmin(uid))
            {
                await Shell.Current.DisplayAlert("Acesso negado", "Você não tem permissão para acessar o Admin.", "OK");
                await Shell.Current.GoToAsync("..");
                return false;
            }
            return true;
        }
    }
}
