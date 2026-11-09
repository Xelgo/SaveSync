using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace SaveSync
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaveSync");

            bool firstInstance;
            string instance = @"Local\SaveSync-" + Environment.UserDomainName + "-" + Environment.UserName;
            using (var singleInstance = new Mutex(true, instance, out firstInstance))
            {
                if (!firstInstance)
                {
                    MessageBox.Show("SaveSync уже запущен. Откройте его значок в области уведомлений Windows.", "SaveSync");
                    return;
                }

                var store = new ProfileStore(directory);
                System.Collections.Generic.List<GameProfile> profiles;
                try
                {
                    profiles = store.Load();
                }
                catch (Exception error)
                {
                    MessageBox.Show("Не удалось загрузить профили из " + directory + ".\n\n" + error.Message,
                        "SaveSync", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                using (var window = new MainForm(store, profiles))
                    Application.Run(window);
            }
        }
    }
}
