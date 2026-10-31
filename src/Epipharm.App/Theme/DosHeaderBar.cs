using System.Drawing;
using System.Windows.Forms;

namespace Epipharm.App.Theme
{
    /// <summary>
    /// The header block every legacy screen shares (see UI_SPEC.md):
    /// title/version line, a cyan double line, the establishment line,
    /// another cyan double line, and — on form/browse screens, not the
    /// main cascading menu — a black subtitle bar naming the screen.
    ///
    /// Every row height here is explicit (TableLayoutPanel Absolute rows),
    /// not AutoSize. A Dock=Fill child inside an AutoSize row (or inside an
    /// AutoSize UserControl) doesn't reliably hand its preferred size back
    /// up through WinForms' layout — there's no compiler or designer in
    /// this session to catch that going wrong, so it's avoided entirely:
    /// every size here is a number this class sets itself.
    /// </summary>
    public class DosHeaderBar : UserControl
    {
        private const int TitleBarHeight = 34;
        private const int DividerHeight = 5;
        private const int EstablishmentHeight = 44;
        private const int SubtitleBarHeight = 32;

        private readonly TableLayoutPanel _layout;
        private readonly Label _titleLeft;
        private readonly Label _titleCenter;
        private readonly Label _titleRight;
        private readonly Label _establishment;
        private readonly Panel _subtitleBar;
        private readonly Label _subtitleLabel;
        private readonly DosDoubleLine _subtitleDivider;

        public DosHeaderBar()
        {
            Dock = DockStyle.Top;
            BackColor = RetroTheme.HeaderBackground;

            _layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = RetroTheme.HeaderBackground,
                ColumnCount = 1,
                RowCount = 6
            };
            _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, TitleBarHeight));     // 0: title bar
            _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DividerHeight));      // 1: divider
            _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, EstablishmentHeight)); // 2: establishment
            _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, DividerHeight));      // 3: divider
            _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));                 // 4: subtitle bar (0 until shown)
            _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));                 // 5: divider (0 until shown)
            Controls.Add(_layout);

            var titleBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = RetroTheme.HeaderBackground,
                ColumnCount = 3,
                RowCount = 1
            };
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            _titleLeft = NewTitleLabel(ContentAlignment.MiddleLeft);
            _titleCenter = NewTitleLabel(ContentAlignment.MiddleCenter);
            _titleRight = NewTitleLabel(ContentAlignment.MiddleRight);
            titleBar.Controls.Add(_titleLeft, 0, 0);
            titleBar.Controls.Add(_titleCenter, 1, 0);
            titleBar.Controls.Add(_titleRight, 2, 0);
            _layout.Controls.Add(titleBar, 0, 0);

            _layout.Controls.Add(new DosDoubleLine { Dock = DockStyle.Fill }, 0, 1);

            _establishment = new Label
            {
                Dock = DockStyle.Fill,
                BackColor = RetroTheme.HeaderBackground,
                ForeColor = RetroTheme.HeaderTitle,
                Font = RetroTheme.DosFont(11F),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0)
            };
            _layout.Controls.Add(_establishment, 0, 2);

            _layout.Controls.Add(new DosDoubleLine { Dock = DockStyle.Fill }, 0, 3);

            _subtitleBar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = RetroTheme.HeaderBackground
            };
            _subtitleLabel = new Label
            {
                Dock = DockStyle.Fill,
                BackColor = RetroTheme.HeaderBackground,
                ForeColor = RetroTheme.HeaderTitle,
                Font = RetroTheme.DosFont(12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _subtitleBar.Controls.Add(_subtitleLabel);
            _layout.Controls.Add(_subtitleBar, 0, 4);

            _subtitleDivider = new DosDoubleLine { Dock = DockStyle.Fill };
            _layout.Controls.Add(_subtitleDivider, 0, 5);

            Title = "PRODUITS  PHARMACEUTIQUES";
            Version = "Version 98.1";
            AppInitials = "M S P";
            Subtitle = null;
        }

        private Label NewTitleLabel(ContentAlignment align) => new Label
        {
            Dock = DockStyle.Fill,
            BackColor = RetroTheme.HeaderBackground,
            ForeColor = RetroTheme.HeaderTitle,
            Font = RetroTheme.DosFont(13F, FontStyle.Bold),
            TextAlign = align
        };

        public string AppInitials
        {
            get => _titleLeft.Text;
            set => _titleLeft.Text = value;
        }

        public string Title
        {
            get => _titleCenter.Text;
            set => _titleCenter.Text = value;
        }

        public string Version
        {
            get => _titleRight.Text;
            set => _titleRight.Text = value;
        }

        public void SetEstablishment(string code, string name)
        {
            _establishment.Text = $"Etablissement : {code}   {name}";
        }

        public string Subtitle
        {
            get => _subtitleLabel.Text;
            set
            {
                _subtitleLabel.Text = value ?? string.Empty;
                var show = !string.IsNullOrEmpty(value);
                _layout.RowStyles[4] = new RowStyle(SizeType.Absolute, show ? SubtitleBarHeight : 0);
                _layout.RowStyles[5] = new RowStyle(SizeType.Absolute, show ? DividerHeight : 0);
                RecalculateHeight(show);
            }
        }

        private void RecalculateHeight(bool subtitleShown)
        {
            Height = TitleBarHeight + DividerHeight + EstablishmentHeight + DividerHeight
                     + (subtitleShown ? SubtitleBarHeight + DividerHeight : 0);
        }
    }
}
