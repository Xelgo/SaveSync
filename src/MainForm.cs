using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SaveSync
{
    internal sealed class MainForm : Form
    {
        private sealed class GameState
        {
            public GameProfile Profile;
            public ListViewItem Row;
            public bool? Running;
            public bool Played;
            public DateTime? ClosedAt;
            public string Result = "";
        }

        private readonly ProfileStore store;
        private readonly List<GameState> games = new List<GameState>();
        private readonly ListView gameList = new ListView();
        private readonly TextBox details = new TextBox();
        private readonly Label summary = new Label();
        private readonly Button add = MakeButton("Добавить игру…");
        private readonly Button edit = MakeButton("Настроить…");
        private readonly Button remove = MakeButton("Удалить профиль");
        private readonly Button upload = MakeButton("Скопировать на Google Диск");
        private readonly Button restore = MakeButton("Забрать с Google Диска");
        private readonly Timer timer = new Timer { Interval = 750 };
        private readonly Icon applicationIcon;
        private readonly NotifyIcon tray;
        private GameState active;
        private bool modal;
        private bool exiting;

        public MainForm(ProfileStore store, List<GameProfile> profiles)
        {
            this.store = store;
            Text = "SaveSync";
            applicationIcon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Icon = applicationIcon ?? SystemIcons.Application;
            Font = new Font("Segoe UI", 9);
            ClientSize = new Size(825, 510);
            MinimumSize = new Size(680, 500);
            StartPosition = FormStartPosition.CenterScreen;
            BuildLayout();
            foreach (GameProfile profile in profiles)
                AddGame(profile);
            if (gameList.Items.Count > 0)
                gameList.Items[0].Selected = true;

            var menu = new ContextMenuStrip();
            menu.Items.Add("Открыть SaveSync", null, (sender, args) => ShowWindow());
            menu.Items.Add("Выход", null, (sender, args) => Exit());
            tray = new NotifyIcon { Icon = Icon, Text = "SaveSync", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += (sender, args) => ShowWindow();

            add.Click += (sender, args) => EditGame(null);
            edit.Click += (sender, args) => EditGame(SelectedGame());
            remove.Click += (sender, args) => RemoveGame();
            upload.Click += async (sender, args) => await Copy(SelectedGame(), false, false);
            restore.Click += async (sender, args) => await Restore();
            gameList.SelectedIndexChanged += (sender, args) => UpdateControls();
            gameList.DoubleClick += (sender, args) =>
            {
                if (edit.Enabled)
                    EditGame(SelectedGame());
            };
            timer.Tick += (sender, args) => WatchGames();
            Shown += (sender, args) =>
            {
                WatchGames();
                timer.Start();
            };
            UpdateControls();
        }

        private void BuildLayout()
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 7 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            Controls.Add(layout);

            layout.Controls.Add(new Label
            {
                Text = "Сохранения игр",
                Font = new Font("Segoe UI", 19, FontStyle.Bold),
                Dock = DockStyle.Fill
            }, 0, 0);
            layout.Controls.Add(new Label
            {
                Text = "Копирование в локальную папку Google Диска после закрытия игры.",
                ForeColor = SystemColors.GrayText,
                Dock = DockStyle.Fill
            }, 0, 1);
            layout.Controls.Add(ButtonRow(add, edit, remove), 0, 2);

            gameList.Dock = DockStyle.Fill;
            gameList.View = View.Details;
            gameList.FullRowSelect = true;
            gameList.HideSelection = false;
            gameList.MultiSelect = false;
            gameList.Columns.Add("Игра", 250);
            gameList.Columns.Add("Состояние", 215);
            gameList.Columns.Add("Последнее действие", 280);
            layout.Controls.Add(gameList, 0, 3);

            details.Dock = DockStyle.Fill;
            details.Multiline = true;
            details.ReadOnly = true;
            details.ScrollBars = ScrollBars.Vertical;
            details.Margin = new Padding(3, 10, 3, 5);
            details.BackColor = SystemColors.Window;
            layout.Controls.Add(details, 0, 4);
            layout.Controls.Add(ButtonRow(upload, restore), 0, 5);

            summary.TextAlign = ContentAlignment.MiddleLeft;
            summary.ForeColor = SystemColors.GrayText;
            summary.Dock = DockStyle.Fill;
            layout.Controls.Add(summary, 0, 6);
        }

        private static Button MakeButton(string text)
        {
            return new Button { Text = text, AutoSize = true, Padding = new Padding(8, 3, 8, 3), MinimumSize = new Size(0, 32) };
        }

        private static FlowLayoutPanel ButtonRow(params Control[] buttons)
        {
            var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 4, 0, 4) };
            row.Controls.AddRange(buttons);
            return row;
        }

        private GameState SelectedGame()
        {
            return gameList.SelectedItems.Count == 0 ? null : (GameState)gameList.SelectedItems[0].Tag;
        }

        private GameState AddGame(GameProfile profile)
        {
            var row = new ListViewItem(new[] { profile.Name, "Проверка процесса…", "—" });
            var game = new GameState { Profile = profile, Row = row };
            row.Tag = game;
            games.Add(game);
            gameList.Items.Add(row);
            return game;
        }

        private void EditGame(GameState game)
        {
            modal = true;
            try
            {
                using (var dialog = new ProfileDialog(store, game == null ? null : game.Profile))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    var profiles = games.Select(item => item == game ? dialog.Profile : item.Profile).ToList();
                    if (game == null)
                        profiles.Add(dialog.Profile);
                    store.Save(profiles);

                    if (game == null)
                        game = AddGame(dialog.Profile);
                    else
                    {
                        if (!string.Equals(game.Profile.Exe, dialog.Profile.Exe, StringComparison.OrdinalIgnoreCase))
                        {
                            game.Played = false;
                            game.ClosedAt = null;
                        }
                        game.Profile = dialog.Profile;
                        game.Row.Text = dialog.Profile.Name;
                    }
                    game.Row.Selected = true;
                    game.Row.EnsureVisible();
                }
            }
            catch (Exception error)
            {
                ShowError("Не удалось сохранить профиль.", error.Message);
            }
            finally
            {
                modal = false;
                WatchGames();
            }
        }

        private void RemoveGame()
        {
            GameState game = SelectedGame();
            if (game == null)
                return;

            modal = true;
            try
            {
                var confirm = MessageBox.Show(this, "Удалить профиль «" + game.Profile.Name + "»?",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (confirm != DialogResult.Yes)
                    return;

                store.Save(games.Where(item => item != game).Select(item => item.Profile).ToList());
                games.Remove(game);
                game.Row.Remove();
            }
            catch (Exception error)
            {
                ShowError("Не удалось удалить профиль.", error.Message);
            }
            finally
            {
                modal = false;
                UpdateControls();
            }
        }

        private async Task Restore()
        {
            var game = SelectedGame();
            if (game == null)
                return;

            DialogResult confirm;
            modal = true;
            try
            {
                confirm = MessageBox.Show(this,
                    "Забрать сохранения «" + game.Profile.Name + "» с Google Диска?\n\n"
                    + "Совпадающие файлы будут заменены. Их прежние версии сохранятся в резервной копии.",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            }
            finally
            {
                modal = false;
            }
            if (confirm == DialogResult.Yes)
                await Copy(game, true, false);
        }

        private async void WatchGames()
        {
            DateTime now = DateTime.UtcNow;
            GameState ready = null;
            foreach (GameState game in games)
            {
                game.Running = GameProcess.IsRunning(game.Profile.Exe);
                if (game.Running == true)
                    game.Played = true;

                if (game.Running != false || !game.Played)
                    game.ClosedAt = null;
                else if (!game.ClosedAt.HasValue)
                    game.ClosedAt = now;

                if (ready == null && game.ClosedAt.HasValue && now - game.ClosedAt.Value >= TimeSpan.FromSeconds(2))
                    ready = game;
            }
            UpdateControls();
            if (ready != null && active == null && !modal)
                await Copy(ready, false, true);
        }

        private async Task Copy(GameState game, bool fromDrive, bool automatic)
        {
            if (game == null || active != null)
                return;

            GameProfile profile = game.Profile;
            if (!fromDrive)
            {
                game.Played = false;
                game.ClosedAt = null;
            }
            active = game;
            UpdateControls();
            string error = null;
            try
            {
                var result = await Task.Run(() =>
                {
                    RequireClosed(profile);
                    return SaveTransfer.Copy(profile, store.DirectoryPath, fromDrive, () => RequireClosed(profile));
                });
                string action = fromDrive ? "Забрали с Диска" : "Скопировали на Диск";
                game.Row.SubItems[2].Text = DateTime.Now.ToString("HH:mm") + " · " + action;
                game.Result = action + ". Файлов: " + result.Files + ".";
                if (result.Backup != null)
                    game.Result += Environment.NewLine + "Резервная копия: " + result.Backup;
            }
            catch (Exception failure)
            {
                error = failure.Message;
                game.Row.SubItems[2].Text = DateTime.Now.ToString("HH:mm") + " · Ошибка копирования";
                game.Result = "Ошибка: " + error;
            }
            finally
            {
                active = null;
                if (!IsDisposed)
                    UpdateControls();
            }

            if (error == null || IsDisposed)
                return;
            if (!automatic)
                ShowError("Не удалось скопировать сохранения.", error);
            else if (!Visible || WindowState == FormWindowState.Minimized)
                tray.ShowBalloonTip(4500, "SaveSync · " + profile.Name, "Копирование не удалось. Откройте SaveSync, чтобы узнать причину.", ToolTipIcon.Warning);
        }

        private static void RequireClosed(GameProfile profile)
        {
            if (GameProcess.IsRunning(profile.Exe) != false)
                throw new IOException("Закройте игру и повторите копирование. SaveSync должен видеть, что процесс игры завершён.");
        }

        private void UpdateControls()
        {
            foreach (GameState game in games)
            {
                string status = "Ожидание запуска";
                if (game == active)
                    status = "Копирование…";
                else if (game.Running == true)
                    status = "Игра запущена";
                else if (!game.Running.HasValue)
                    status = "Проверка процесса…";
                else if (game.Played)
                    status = "Ожидание копирования";
                if (game.Row.SubItems[1].Text != status)
                    game.Row.SubItems[1].Text = status;
            }

            var selected = SelectedGame();
            add.Enabled = active == null;
            edit.Enabled = remove.Enabled = add.Enabled && selected != null;
            upload.Enabled = restore.Enabled = edit.Enabled && selected.Running == false && !selected.Played;
            string information = "Добавьте игру и укажите её EXE и две папки.";
            if (selected != null)
            {
                var profile = selected.Profile;
                information = string.Join(Environment.NewLine, new[]
                {
                    "EXE: " + profile.Exe,
                    "Сохранения: " + profile.Saves,
                    "Google Диск: " + profile.Drive,
                    selected.Result
                });
            }
            if (details.Text != information)
                details.Text = information;
            summary.Text = active == null ? "SaveSync продолжает отслеживание в трее после закрытия окна."
                : "Копирование сохранений игры «" + active.Profile.Name + "»…";
        }

        private void ShowError(string title, string error)
        {
            MessageBox.Show(this, title + "\n\n" + error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ShowWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void Exit()
        {
            if (active != null)
            {
                tray.ShowBalloonTip(3500, "SaveSync", "Дождитесь окончания копирования.", ToolTipIcon.Info);
                return;
            }
            exiting = true;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs args)
        {
            if (args.CloseReason == CloseReason.UserClosing && !exiting)
            {
                args.Cancel = true;
                Hide();
            }
            else
                timer.Stop();
            base.OnFormClosing(args);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
            {
                timer.Dispose();
                if (tray != null)
                {
                    tray.Visible = false;
                    tray.ContextMenuStrip.Dispose();
                    tray.Dispose();
                }
                if (applicationIcon != null)
                    applicationIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
