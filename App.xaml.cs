using AmoraApp.Views;
using Microsoft.Maui.Controls;

namespace AmoraApp
{
    public partial class App : Application
    {
        public App(WelcomePage welcomePage)
        {
            InitializeComponent();
            MainPage = new NavigationPage(welcomePage);
        }
    }
}
