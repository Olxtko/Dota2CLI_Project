using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using Dota2CLI.Core;
using Dota2CLI.Data;

namespace Dota2GUI;

public sealed class MainForm : Form
{
    private readonly JsonDataStore _store;
    private readonly OpenDotaHeroApiClient _api;
    private readonly HeroCatalog _catalog;
    private readonly BattleGame _game;
    private readonly PortraitCache _portraitCache = new();
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel _heroCards = new()
    {
        Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(12)
    };
    private readonly ComboBox _player = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly ComboBox _opponent = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly RichTextBox _output = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(20, 24, 33),
        ForeColor = Color.Gainsboro, Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None
    };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly Button _refresh = new() { Text = "Оновити каталог", AutoSize = true };
    private bool _busy;

    public MainForm()
    {
        Text = "Dota 2 — каталог персонажів і бойова гра";
        MinimumSize = new Size(1000, 700);
        Size = new Size(1250, 820);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(245, 247, 250);
        _store = new JsonDataStore();
        _api = new OpenDotaHeroApiClient();
        _catalog = new HeroCatalog(_store, _api);
        _game = new BattleGame(_store, _store, _catalog);
        BuildCatalogTab();
        BuildGameTab();
        Controls.Add(_tabs);
        Load += async (_, _) => await InitializeApp();
        FormClosed += (_, _) => _api.Dispose();
    }

    private void BuildCatalogTab()
    {
        var page = new TabPage("Каталог персонажів") { BackColor = Color.FromArgb(245, 247, 250) };
        var header = new Panel { Dock = DockStyle.Top, Height = 74, Padding = new Padding(18, 12, 18, 8) };
        var title = new Label { Text = "ГЕРОЇ DOTA 2", Font = new Font("Segoe UI", 19, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(35, 43, 58), Location = new Point(18, 12) };
        _refresh.Location = new Point(850, 18);
        _refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _refresh.Click += async (_, _) => await RefreshCatalog();
        header.Controls.Add(title);
        header.Controls.Add(_refresh);
        var sub = new Label { Text = "Персонажі, характеристики та портрети з каталогу OpenDota", AutoSize = true, ForeColor = Color.Gray, Location = new Point(20, 48) };
        header.Controls.Add(sub);
        page.Controls.Add(_heroCards);
        page.Controls.Add(header);
        _tabs.TabPages.Add(page);
    }

    private void BuildGameTab()
    {
        var page = new TabPage("Бойова гра") { BackColor = Color.FromArgb(245, 247, 250) };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(16), WrapContents = true };
        top.Controls.Add(MakeLabel("Гравець"));
        top.Controls.Add(_player);
        top.Controls.Add(MakeLabel("Суперник"));
        top.Controls.Add(_opponent);
        top.Controls.Add(MakeButton("Почати бій", Color.FromArgb(44, 105, 180), (_, _) => StartBattle()));
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 58, Padding = new Padding(16, 8, 16, 8) };
        actions.Controls.Add(MakeButton("Атакувати", Color.FromArgb(174, 58, 58), (_, _) => DoAction("attack")));
        actions.Controls.Add(MakeButton("Лікуватися", Color.FromArgb(44, 135, 93), (_, _) => DoAction("heal")));
        actions.Controls.Add(MakeButton("Захищатися", Color.FromArgb(87, 103, 133), (_, _) => DoAction("defend")));
        actions.Controls.Add(MakeButton("Стан бою", Color.FromArgb(90, 98, 110), (_, _) => ShowStatus()));
        actions.Controls.Add(MakeButton("Історія", Color.FromArgb(90, 98, 110), (_, _) => ShowHistory()));
        var saves = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(16, 4, 16, 4) };
        saves.Controls.Add(MakeButton("Зберегти як…", Color.FromArgb(100, 90, 150), (_, _) => SaveAs()));
        saves.Controls.Add(MakeButton("Вибрати та завантажити сейв…", Color.FromArgb(100, 90, 150), (_, _) => LoadFromFile()));
        saves.Controls.Add(_status);
        _status.Margin = new Padding(12, 12, 0, 0);
        page.Controls.Add(_output);
        page.Controls.Add(saves);
        page.Controls.Add(actions);
        page.Controls.Add(top);
        _tabs.TabPages.Add(page);
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text, AutoSize = true, Margin = new Padding(8, 10, 4, 0),
        Font = new Font("Segoe UI", 9, FontStyle.Bold)
    };

    private static Button MakeButton(string text, Color color, EventHandler click)
    {
        var button = new Button
        {
            Text = text, AutoSize = true, Height = 34, FlatStyle = FlatStyle.Flat,
            BackColor = color, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Margin = new Padding(5), Padding = new Padding(8, 2, 8, 2), Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += click;
        return button;
    }

    private async Task InitializeApp()
    {
        try
        {
            RenderCatalog();
            await RefreshCatalog(showMessages: false);
            if (_store.HasSavedGame)
            {
                try
                {
                    _game.Load();
                    Append("Знайдено та відновлено останній бій.");
                    Append(_game.Status());
                    _status.Text = "Останнє збереження відновлено";
                }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or ArgumentException or IOException)
                {
                    Append("Не вдалося відновити збереження: " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Помилка запуску", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RefreshCatalog(bool showMessages = true)
    {
        if (_busy) return;
        _busy = true;
        _refresh.Enabled = false;
        _refresh.Text = "Завантаження…";
        try
        {
            await _catalog.RefreshAsync();
            RenderCatalog();
            if (showMessages)
                MessageBox.Show(this, $"Каталог оновлено. Персонажів: {_catalog.All.Count}", "Готово",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                Append($"Каталог завантажено з OpenDota API. Персонажів: {_catalog.All.Count}");
        }
        catch (Exception ex)
        {
            if (showMessages)
                MessageBox.Show(this, "Не вдалося оновити каталог.\\n" + ex.Message,
                    "Помилка API", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                Append("Не вдалося оновити каталог з OpenDota API. Показано локальні дані. " + ex.Message);
        }
        finally
        {
            _busy = false;
            _refresh.Enabled = true;
            _refresh.Text = "Оновити каталог";
        }
    }

    private void RenderCatalog()
    {
        var cards = _catalog.GetCards().OrderBy(x => x.Name).ToArray();
        _heroCards.SuspendLayout();
        foreach (Control oldCard in _heroCards.Controls)
            oldCard.Dispose();
        _heroCards.Controls.Clear();
        foreach (var hero in cards)
            _heroCards.Controls.Add(CreateHeroCard(hero));
        _heroCards.ResumeLayout();
        var selectedPlayer = _player.SelectedItem as HeroChoice;
        var selectedOpponent = _opponent.SelectedItem as HeroChoice;
        var choices = cards.Select(x => new HeroChoice(x.Id, x.Name)).ToArray();
        _player.DataSource = choices.ToList();
        _player.DisplayMember = nameof(HeroChoice.Name);
        _opponent.DataSource = choices.ToList();
        _opponent.DisplayMember = nameof(HeroChoice.Name);
        if (selectedPlayer is not null) _player.SelectedItem = choices.FirstOrDefault(x => x.Id == selectedPlayer.Id);
        if (selectedOpponent is not null) _opponent.SelectedItem = choices.FirstOrDefault(x => x.Id == selectedOpponent.Id);
        if (_opponent.Items.Count > 1 && _opponent.SelectedIndex == _player.SelectedIndex) _opponent.SelectedIndex = 1;
    }

    private Control CreateHeroCard(HeroCardData hero)
    {
        var card = new Panel
        {
            Width = 265, Height = 310, BackColor = Color.White, Margin = new Padding(10),
            BorderStyle = BorderStyle.FixedSingle
        };
        var picture = new PictureBox
        {
            Location = new Point(12, 12), Size = new Size(239, 128),
            SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(235, 238, 243),
            ErrorImage = SystemIcons.Warning.ToBitmap()
        };
        if (Uri.TryCreate(hero.ArtworkUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var artworkUrl = uri.ToString();
            var cachedPath = _portraitCache.Find(artworkUrl);
            if (cachedPath is not null)
                SetPortrait(picture, cachedPath);
            else
                _ = DownloadAndCachePortrait(picture, artworkUrl);
        }
        var name = new Label
        {
            Text = hero.Name, Location = new Point(14, 148), Width = 235, Height = 28,
            Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = Color.FromArgb(35, 43, 58)
        };
        var info = new Label
        {
            Text = $"Атрибут: {hero.Attribute}\nТип атаки: {hero.AttackType}\nHP: {hero.MaxHp}   Атака: {hero.Attack}\nБроня: {hero.Armor}   Опір: {hero.Resist:P0}\nШвидкість: {hero.Speed}",
            Location = new Point(14, 180), Width = 235, Height = 105,
            Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(75, 83, 98)
        };
        card.Controls.Add(picture);
        card.Controls.Add(name);
        card.Controls.Add(info);
        return card;
    }


    private async Task DownloadAndCachePortrait(PictureBox picture, string artworkUrl)
    {
        try
        {
            var path = await _portraitCache.GetOrDownloadAsync(artworkUrl);
            if (picture.IsDisposed) return;

            if (picture.InvokeRequired && picture.IsHandleCreated)
                picture.BeginInvoke(new Action(() => SetPortrait(picture, path)));
            else if (!picture.InvokeRequired)
                SetPortrait(picture, path);
        }
        catch
        {
            // Якщо зображення недоступне, картка залишається з фоновою заглушкою.
        }
    }

    private static void SetPortrait(PictureBox picture, string path)
    {
        if (picture.IsDisposed || !File.Exists(path)) return;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var source = Image.FromStream(stream);
            var image = new Bitmap(source);
            var previous = picture.Image;
            picture.Image = image;
            previous?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or OutOfMemoryException)
        {
            // Пошкоджений кешований файл буде завантажено повторно під час наступного оновлення.
        }
    }

    private void StartBattle()
    {
        if (_player.SelectedItem is not HeroChoice player || _opponent.SelectedItem is not HeroChoice opponent)
        {
            MessageBox.Show(this, "Спочатку завантажте каталог і виберіть двох персонажів.");
            return;
        }
        try
        {
            _game.Start(player.Id, opponent.Id);
            Append("Новий бій розпочато.");
            Append(_game.Status());
            _status.Text = "Бій активний";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void DoAction(string action)
    {
        try
        {
            Append(_game.Act(action));
            _status.Text = _game.Current?.IsFinished == true ? "Бій завершено" : "Зміни збережено автоматично";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ShowStatus()
    {
        try { Append(_game.Status()); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ShowHistory()
    {
        try { Append(_game.History()); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void SaveAs()
    {
        try
        {
            if (_game.Current is null) { MessageBox.Show(this, "Спочатку почніть або завантажте бій."); return; }
            using var dialog = new SaveFileDialog
            {
                Title = "Зберегти стан гри", Filter = "Файл збереження (*.json)|*.json",
                FileName = "dota2-save.json", DefaultExt = "json"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            var json = JsonSerializer.Serialize(_game.Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dialog.FileName, json);
            _game.Save();
            _status.Text = "Збереження створено";
            Append("Стан бою збережено у файл: " + dialog.FileName);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void LoadFromFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Вибрати файл збереження", Filter = "Файл збереження (*.json)|*.json|Усі файли (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var json = File.ReadAllText(dialog.FileName);
            var state = JsonSerializer.Deserialize<GameState>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Файл не містить стану гри.");
            var tempDirectory = Path.Combine(Path.GetTempPath(), "Dota2GUI-check-" + Guid.NewGuid().ToString("N"));
            try
            {
                var validationStore = new JsonDataStore(tempDirectory);
                validationStore.SaveGame(state);
                var validationGame = new BattleGame(validationStore, validationStore, _catalog);
                validationGame.Load();

                _store.SaveGame(state);
                _game.Load();
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                    Directory.Delete(tempDirectory, recursive: true);
            }
            Append("Завантажено збереження: " + dialog.FileName);
            Append(_game.Status());
            _status.Text = "Збереження завантажено";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void Append(string message)
    {
        _output.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}{Environment.NewLine}");
        _output.SelectionStart = _output.TextLength;
        _output.ScrollToCaret();
    }

    private void ShowError(Exception ex) =>
        MessageBox.Show(this, ex.Message, "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private sealed record HeroChoice(string Id, string Name)
    {
        public override string ToString() => Name;
    }
}
