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
using System.Drawing.Imaging;
using Gma.System.MouseKeyHook;

namespace TimeTracker.Forms
{
    public partial class MainForm : Form
    {
        private IKeyboardMouseEvents _globalHook; // Inisialisasi hook dari interface KeyboardMouseEvents
        private int _keyboardActivityCount = 0; // Inisialisasi state untuk menghitung aktivitas keyboard
        private int _mouseActivityCount = 0; // Inisialisasi state untuk menghitung aktivitas mouse
        private Timer _timer; // Insialisasi timer
        private Timer _screenshotTimer; // Inisialisasi timer untuk screenshot
        private Timer _activityTimer; // Inisialisasi timer untuk deteksi aktivitas mouse dan keyboard
        private DateTime _startTime; // Inisialisasi waktu mulai
        private bool _tracking; // Inisialisasi toggle/state tracking
        private readonly string _logFile = Path.Combine(Application.StartupPath, "timelog.csv"); // Inisialisasi csv untuk menyimpan log
        private readonly string _screenshotDir = Path.Combine(Application.StartupPath, "Screenshots"); // Inisialisasi folder untuk menyimpan screenshot

        public MainForm()
        {
            InitializeComponent(); // Insialisasi komponen UI
            SetupTimer(); // Inisialisasi timer
            EnsureLogFile(); // Inisialisasi file csv untuk menyimpan aktivitas
            SetupActivityTracking(); // Inisialisasi method untuk fitur sctivity tracking
        }

        private void SetupActivityTracking()
        {
            // Mulai hook global mouse & keyboard
            _globalHook = Hook.GlobalEvents();

            // Set state ke keyboardActivity dan mouseActivity ketika event terjadi
            _globalHook.KeyDown += (s, e) => _keyboardActivityCount++;
            _globalHook.MouseMove += (s, e) => _mouseActivityCount++;
            _globalHook.MouseClick += (s, e) => _mouseActivityCount++;
        }

        private void SetupTimer() // Fungsi untuk setup timer set interval dan event ketika timer berjalan
        {
            _timer = new Timer { Interval = 1000 }; // 1 detik untuk memperbarui User Interface
            _screenshotTimer = new Timer { Interval = 60000 }; // 1 menit untuk setiap waktu mengambil tangkapan layar
            _activityTimer = new Timer { Interval = 10000 }; // Timer untuk evaluasi aktivitas setiap 10 detik

            _timer.Tick += Timer_Tick; // Menerapkan event _timer.Tick ke method Timer_Tick
            _screenshotTimer.Tick += ScreenshotTimer_Tick; // Menerapkan event _screenshotTimer.Tick ke method ScreenshotTimer_Tick
            _activityTimer.Tick += ActivityTimer_Tick;
        }

        private void ActivityTimer_Tick(object sender, EventArgs e)
        {
            bool isIdle = _keyboardActivityCount == 0 && _mouseActivityCount == 0;
            string status = isIdle
                ? "Idle"
                : $"Active | Keys: {_keyboardActivityCount}, Mouse: {_mouseActivityCount}";

            listLog.Items.Add($"{DateTime.Now:HH:mm:ss} - {status}");
            labelActivity.Text = $"Activity Status: {status}";
            labelActivity.ForeColor = isIdle ? Color.Red : Color.Green;
            _keyboardActivityCount = 0;
            _mouseActivityCount = 0;
        }

        private void ScreenshotTimer_Tick(object sender, EventArgs e) // Fungsi ketika timer dari screenshot berjalan setiap tick berdasarkan interval
        {
            try
            {
                var timestamp = DateTime.Now; // Menyimpan waktu untuk nama sub-direktori untuk menyimpan screenshot
                var dateFolder = Path.Combine(_screenshotDir, timestamp.ToString("yyyy-MM-dd")); // Membuat sub-direktori untuk menyimpan screenshot
                if (!Directory.Exists(dateFolder)) // Validasi jika direkti screenschot belum ada, maka buat direktori
                    Directory.CreateDirectory(dateFolder);

                foreach (var screen in Screen.AllScreens) // Mendapatkan tampilan dari semua layar yang terdeteksi
                {
                    Rectangle bounds = screen.Bounds; // Mendapatkan dimensi dari layar
                    using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height)) // Membuat bitmap dan mengambil ukuran lebar dan tinggi dari screen yang dibuka
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                        }
                        string fileName = Path.Combine(dateFolder, $"screen_{screen.DeviceName.Replace('\\', '_')}_{timestamp:HHmmss}.png"); // Membuat filename screenshotnya
                        bmp.Save(fileName, ImageFormat.Png); // Menyimpan gambar dengan format .png
                    }
                }

                listLog.Items.Add($"Screenshot saved: {timestamp:HH:mm:ss}"); // Menambahkan log ke dalam listBox bahwa scrennshot berhasil disimpan
            }
            catch (Exception ex)
            {
                listLog.Items.Add($"Screenshot error: {ex.Message}"); // Memberi log error ke dalam listBox bahwa screenshot gagal diambil
            }
        }

        private void Timer_Tick(object sender, EventArgs e) // Fungsi ketika timer sedang berjalan
        {
            var elapsed = DateTime.Now - _startTime; // Membuat seperti stopwatch
            labelStatus.Text = $"Tracking: {elapsed:hh\\:mm\\:ss}"; // Set stopwatch ke label
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (_tracking) return; // Jika sedang melakukan tracking maka tidak akan menjalankan fungsi dibawahnya
            _startTime = DateTime.Now; // Menetapkan waktu memulai
            _timer.Start(); // Menjalankan timer
            _screenshotTimer.Start(); // Menjalankan timer screenshot
            _activityTimer.Start(); // Menjalankan timer untuk activity detection
            _tracking = true; // Set state _tracking ke true
            btnStart.Enabled = false; // Disable button start
            btnStop.Enabled = true; // Enable button stop
            LogEntry("START", _startTime);
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (!_tracking) return; // Jika tidak sedang melakukan tracking maka tidak akan menjalankan fungsi dibawahnya
            _timer.Stop(); // Berhentikan timer
            _screenshotTimer.Stop(); // Menjalankan timer screenshot
            _activityTimer.Stop(); // Menjalankan timer untuk activity detection
            var stopTime = DateTime.Now; // Untuk menyimpan waktu berhenti
            var elapsed = stopTime - _startTime; // Untuk menghitung waktu yang telah berlalu
            _tracking = false; // Set state _tracking ke false
            btnStart.Enabled = true; // Enable button start
            btnStop.Enabled = false; // Disable button start
            labelStatus.Text = "Not Tracking"; // Set label status ke "Not Tracking"
            _keyboardActivityCount = 0; // Set state aktivitas keyboard ke 0
            _mouseActivityCount = 0; // Set state aktivitas mouse ke 0
            LogEntry("STOP", stopTime, elapsed);
            listLog.Items.Add($"{_startTime:yyyy-MM-dd HH:mm:ss} -> {stopTime:HH:mm:ss} | {elapsed:hh\\:mm\\:ss}"); // Menambahkan log ke dalam listBox
        }

        private void EnsureLogFile() // Fungsi untuk mengecek keberadaan file csv untuk menyimpan log
        {
            if (!File.Exists(_logFile)) // Jika file belum ada maka buat file dan tambahkan isi dari header csv
                File.WriteAllText(_logFile, "Event,TimeStamp,Duration\r\n");
            else
            {
                var lines = File.ReadAllLines(_logFile); // Membaca file csv
                foreach (var line in lines)
                {
                    if (line.StartsWith("START") || line.StartsWith("STOP")) // Validasi file csv berdasarkan column Event
                        listLog.Items.Add(line); // Menambahkan log ke dalam listBox dari file csv
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

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Stop semua timer saat form ditutup agar tidak tejadi StackOverflowException
            _timer?.Stop();
            _screenshotTimer?.Stop();
            _activityTimer?.Stop();
            _globalHook?.Dispose(); // Memastikan hook dimatikan saat form ditutup
        }
    }
}
