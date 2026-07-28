using System.Windows;
using UniOrganiser.Services;

namespace UniOrganiser
{
    public partial class App : Application
    {
        public static DatabaseService Db { get; private set; } = null!;
        public static RecurrenceService Recurrence { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Db = new DatabaseService();
            Recurrence = new RecurrenceService(Db);
            Recurrence.MaterialiseAll();
            DispatcherUnhandledException += (_, args) =>
            {
                MessageBox.Show(args.Exception.ToString(), "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Db.Dispose();
            base.OnExit(e);
        }
    }
}
