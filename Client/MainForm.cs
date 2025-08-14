using Shared;
using System.Windows.Forms;

namespace Client
{
    public partial class MainForm : Form
    {
        readonly GameClient _client = new();
        UpdateSnapshot? _last;

        bool _keyUp, _keyDown;
        System.Windows.Forms.Timer _netTimer;

        public MainForm()
        {
            InitializeComponent();
            DoubleBuffered = true;
            KeyPreview = true;

            _client.OnSnapshot += snap =>
            {
                // UI 스레드로 마샬링
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke(new Action(() => { _last = snap; Invalidate(); }));
            };

            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Up) { _keyUp = true; e.Handled = true; }
                if (e.KeyCode == Keys.Down) { _keyDown = true; e.Handled = true; }
            };

            this.KeyUp += (s, e) =>
            {
                if (e.KeyCode == Keys.Up) { _keyUp = false; e.Handled = true; }
                if (e.KeyCode == Keys.Down) { _keyDown = false; e.Handled = true; }
            };

            _client.OnRoleChanged += n =>
            {
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke(new Action(() => this.Text = $"Pong - Player {(n == 0 ? "Spectator" : n)}"));
            };
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                await _client.ConnectAsync("127.0.0.1", 7777, "Player", CancellationToken.None);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Close();
                return;
            }

            // ★ 60Hz로 현재 키 상태를 서버에 전송
            _netTimer = new System.Windows.Forms.Timer { Interval = 1000 / 60 };
            _netTimer.Tick += async (_, __) =>
            {
                // 폼이 포커스 없으면 전송해도 입력은 의미 없지만, 상태 송신 자체는 OK
                await _client.SendInputAsync(_keyUp, _keyDown);
                Invalidate();
            };
            _netTimer.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _netTimer?.Stop();
            _ = _client.LeaveAsync();
            _client.Dispose();
            base.OnFormClosing(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            bool up = Keyboard.IsKeyDown(Keys.Up);
            bool down = Keyboard.IsKeyDown(Keys.Down);
            _ = _client.SendInputAsync(up, down);
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_last is null) return;
            var g = e.Graphics;
            g.Clear(Color.Black);

            // 패들/공 렌더
            g.FillRectangle(Brushes.Red, _last.P1X, _last.P1Y, 10, 80);
            g.FillRectangle(Brushes.Blue, _last.P2X, _last.P2Y, 10, 80);
            g.FillEllipse(Brushes.Green, _last.BallX, _last.BallY, 10, 10);

            using var f = new Font(FontFamily.GenericSansSerif, 14);
            g.DrawString($"{_last.Score1} : {_last.Score2}", f, Brushes.White, Width / 2 - 20, 10);

            // ==== 여기부터 내 역할 표시 ====
            // myRole: 0=관전자, 1=P1, 2=P2
            string roleText = _client.myRole == 0 ? "You: Spectator" : $"You: Player {_client.myRole}";
            using var roleFont = new Font(FontFamily.GenericSansSerif, 12, FontStyle.Bold);

            // 살짝 반투명 배경 박스
            var size = g.MeasureString(roleText, roleFont);
            float pad = 6f;
            var rect = new RectangleF(10f, Height - size.Height - 20f, size.Width + pad * 2, size.Height + pad * 2);
            using var bg = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
            g.FillRectangle(bg, rect);

            // 라벨 텍스트
            g.DrawString(roleText, roleFont, Brushes.Yellow, rect.Left + pad, rect.Top + pad);
            // ==== 내 역할 표시 끝 ====
        }


        static class Keyboard
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            static extern short GetAsyncKeyState(Keys vKey);
            public static bool IsKeyDown(Keys k) => (GetAsyncKeyState(k) & 0x8000) != 0;
        }
    }
}
