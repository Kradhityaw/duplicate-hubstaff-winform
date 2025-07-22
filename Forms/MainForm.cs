using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;

namespace TimeTracker.Forms
{
    public partial class MainForm : Form
    {
        private Timer _timer; // Insialisasi timer
        private Timer _screenshotTimer;
        private DateTime _startTime; // Inisialisasi waktu mulai
        private bool _tracking; // Inisialisasi toggle/state tracking
        private readonly string _logFile = Path.Combine(Application.StartupPath, "timelog.csv"); // Inisialisasi csv untuk menyimpan log
        private readonly string _screenshotDir = Path.Combine(Application.StartupPath, "Screenshots");

        public MainForm()
        {
            InitializeComponent();
            SetupTimer();
            EnsureLogFile();
        }

        private void SetupTimer() // Fungsi untuk setup timer set interval dan event ketika timer berjalan
        {
            _timer = new Timer { Interval = 1000 }; // 1 detik untuk memperbarui User Interface
            _screenshotTimer = new Timer { Interval = 60000 }; // 1 menit untuk setiap waktu mengambil tangkapan layar
            _timer.Tick += Timer_Tick;
            _screenshotTimer.Tick += ScreenshotTimer_Tick;
        }

        private void ScreenshotTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                var timestamp = DateTime.Now;
                var dateFolder = Path.Combine(_screenshotDir, timestamp.ToString("yyyy-MM-dd"));
                if (!Directory.Exists(dateFolder))
                    Directory.CreateDirectory(dateFolder);

                var filename = Path.Combine(dateFolder, $"screenshot_{timestamp:HHmmss}.png");

                var screenBounds = Screen.PrimaryScreen.Bounds;
                using (Bitmap bmp = new Bitmap(screenBounds.Width, screenBounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(screenBounds.Location, Point.Empty, screenBounds.Size);
                    }
                    bmp.Save(filename, ImageFormat.Png);
                }

                listLog.Items.Add($"Screenshot saved: {timestamp:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                listLog.Items.Add($"Screenshot error: {ex.Message}");
            }
        }

        private void Timer_Tick(object sender, EventArgs e) // Fungsi yang berfungsi ketika timer sedang berjalan
        {
            var elapsed = DateTime.Now - _startTime; // Membuat seperti stopwatch
            labelStatus.Text = $"Tracking: {elapsed:hh\\:mm\\:ss}"; // Set stopwatch ke label
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (_tracking) return; // Jika sedang melakukan tracking maka tidak akan menjalankan fungsi dibawahnya
            _startTime = DateTime.Now; // Menetapkan waktu memulai
            _timer.Start(); // Menjalankan timer
            _screenshotTimer.Start();
            _tracking = true; // Set state _tracking ke true
            btnStart.Enabled = false; // Disable button start
            btnStop.Enabled = true; // Enable button stop
            LogEntry("START", _startTime);
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (!_tracking) return; // Jika tidak sedang melakukan tracking maka tidak akan menjalankan fungsi dibawahnya
            _timer.Stop(); // Berhentikan timer
            _screenshotTimer.Stop();
            var stopTime = DateTime.Now; // Untuk menyimpan waktu berhenti
            var elapsed = stopTime - _startTime; // Untuk menghitung waktu yang telah berlalu
            _tracking = false;
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            labelStatus.Text = "Not Tracking";
            LogEntry("STOP", stopTime, elapsed);
            listLog.Items.Add($"{_startTime:yyyy-MM-dd HH:mm:ss} -> {stopTime:HH:mm:ss} | {elapsed:hh\\:mm\\:ss}");
        }

        private void EnsureLogFile()
        {
            if (!File.Exists(_logFile)) // Jika file belum ada maka buat file dan tambahkan isi dari header csv
                File.WriteAllText(_logFile, "Event,TimeStamp,Duration\r\n");
            else
            {
                var lines = File.ReadAllLines(_logFile);
                foreach (var line in lines)
                {
                    if (line.StartsWith("START") || line.StartsWith("STOP"))
                        listLog.Items.Add(line);
                }
            }
        }

        private void LogEntry(string eventType, DateTime timestamp, TimeSpan? duration = null)
        {
            var line = duration == null
                ? $"{eventType},{timestamp:yyyy-MM-dd HH:mm:ss}," // Ketika start time
                : $"{eventType},{timestamp:yyyy-MM-dd HH:mm:ss},{duration:hh\\:mm\\:ss}";
            File.AppendAllText(_logFile, line + "\r\n");
        }
    }
}
