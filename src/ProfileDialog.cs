using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SaveSync
{
    internal sealed class ProfileDialog : Form
    {
        private readonly TextBox name = new TextBox();
        private readonly TextBox exe = new TextBox();
        private readonly TextBox saves = new TextBox();
        private readonly TextBox drive = new TextBox();

        public ProfileDialog(ProfileStore store, GameProfile existing)
        {
            Text = existing == null ? "Добавить игру" : "Настроить игру";
            ClientSize = new Size(680, 260);
            MinimumSize = new Size(600, 290);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 9);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 3, RowCount = 6 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (int row = 0; row < 4; row++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout);

            AddField(layout, 0, "Название", name, null);
            AddField(layout, 1, "EXE игры", exe, PickExe);
            AddField(layout, 2, "Папка сохранений", saves, () => PickFolder(saves));
            AddField(layout, 3, "Папка на Google Диске", drive, () => PickFolder(drive));
            var hint = new Label
            {
                Text = "Выберите отдельную папку Google Диска для этой игры. Можно ввести путь к новой папке.",
                Dock = DockStyle.Fill,
                ForeColor = SystemColors.GrayText
            };
            layout.Controls.Add(hint, 0, 4);
            layout.SetColumnSpan(hint, 3);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "Отмена", AutoSize = true, DialogResult = DialogResult.Cancel };
            var save = new Button { Text = "Сохранить", AutoSize = true };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);
            layout.Controls.Add(buttons, 0, 5);
            layout.SetColumnSpan(buttons, 3);
            CancelButton = cancel;
            AcceptButton = save;

            if (existing != null)
            {
                name.Text = existing.Name;
                exe.Text = existing.Exe;
                saves.Text = existing.Saves;
                drive.Text = existing.Drive;
            }

            save.Click += (sender, args) =>
            {
                var profile = new GameProfile
                {
                    Id = existing == null ? Guid.NewGuid() : existing.Id,
                    Name = name.Text,
                    Exe = exe.Text,
                    Saves = saves.Text,
                    Drive = drive.Text
                };
                try
                {
                    store.Validate(profile);
                    if (!File.Exists(profile.Exe))
                        throw new ArgumentException("Файл EXE не найден. Проверьте путь к игре.");
                    Profile = profile;
                    DialogResult = DialogResult.OK;
                }
                catch (Exception error)
                {
                    MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
        }

        public GameProfile Profile { get; private set; }

        private static void AddField(TableLayoutPanel layout, int row, string caption, TextBox input, Action browse)
        {
            layout.Controls.Add(new Label { Text = caption, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, row);
            input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            layout.Controls.Add(input, 1, row);
            if (browse == null)
                return;
            var button = new Button { Text = "Обзор…", AutoSize = true, Anchor = AnchorStyles.Left };
            button.Click += (sender, args) => browse();
            layout.Controls.Add(button, 2, row);
        }

        private void PickExe()
        {
            using (var picker = new OpenFileDialog { Filter = "Приложение (*.exe)|*.exe", Title = "Выберите EXE игры" })
            {
                if (File.Exists(exe.Text))
                    picker.InitialDirectory = Path.GetDirectoryName(exe.Text);
                if (picker.ShowDialog(this) == DialogResult.OK)
                    exe.Text = picker.FileName;
            }
        }

        private void PickFolder(TextBox input)
        {
            using (var picker = new FolderBrowserDialog { Description = "Выберите папку", ShowNewFolderButton = true })
            {
                if (Directory.Exists(input.Text))
                    picker.SelectedPath = input.Text;
                if (picker.ShowDialog(this) == DialogResult.OK)
                    input.Text = picker.SelectedPath;
            }
        }
    }
}
