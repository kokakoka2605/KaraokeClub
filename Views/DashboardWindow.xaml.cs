using System.Windows;
using KaraokeClub.Data;
using KaraokeClub.ViewModels;

namespace KaraokeClub.Views
{
    public partial class DashboardWindow : Window
    {
        public DashboardWindow(AppDbContext ctx)
        {
            InitializeComponent();
            DataContext = new DashboardViewModel(ctx);
        }
    }
}