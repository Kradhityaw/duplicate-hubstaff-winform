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
using System.Runtime.InteropServices; // Untuk P/Invoke

namespace TimeTracker.Forms
{
    public partial class MainForm : Form
    {
        // P/Invoke untuk deteksi idle
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }
        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        private Timer _timer;              // Timer untuk tracking durasi kerja
        private Timer _screenshotTimer;    // Timer untuk mengambil screenshot
        private Timer _activityTimer;      // Timer untuk deteksi idle
        private DateTime _startTime;       // Waktu dimulainya tracking
        private bool _tracking;            // State flag tracking

        // Path untuk file csv yang digunakan untuk menyimpan log
        private readonly string _logFile = Path.Combine(Application.StartupPath, "timelog.csv");
        // Inisialisasi folder untuk menyimpan screenshot
        private readonly string _screenshotDir = Path.Combine(Application.StartupPath, "Screenshots");

        public MainForm()
        {
            InitializeComponent();   // Insialisasi komponen UI
            SetupTimer();            // Setup semua timer
            EnsureLogFile();         // Menyiapkan file log
        }

        // Setup timer untuk tracking durasi kerja, screenshot, dan deteksi idle
        private void SetupTimer()
        {
            // Timer untuk durasi kerja, Akan memperbarui ui setiap detik
            _timer = new Timer { Interval = 1000 };
            _timer.Tick += Timer_Tick;

            // Timer untuk screenshot, dengan default setiap 60 detik
            _screenshotTimer = new Timer { Interval = 60000 };
            _screenshotTimer.Tick += ScreenshotTimer_Tick;

            // Timer untuk deteksi idle, dengan default setiap 10 detik
            _activityTimer = new Timer { Interval = 10000 };
            _activityTimer.Tick += ActivityTimer_Tick;
        }

        // Cek waktu idle dan perbarui label
        private void ActivityTimer_Tick(object sender, EventArgs e)
        {
            // Mendapatkan waktu idle berupa milidetik
            LASTINPUTINFO lastInput = new LASTINPUTINFO();
            lastInput.cbSize = (uint)Marshal.SizeOf(lastInput);
            GetLastInputInfo(ref lastInput);
            uint idleTime = (uint)Environment.TickCount - lastInput.dwTime;

            bool isIdle = idleTime >= _activityTimer.Interval;

            // Update tampilan label
            if (isIdle)
            {
                labelActivity.Text = "Activity Status: Idle";
                labelActivity.ForeColor = Color.Red;
            }
            else
            {
                labelActivity.Text = "Activity Status: Active";
                labelActivity.ForeColor = Color.Green;
            }

            // Log status
            listLog.Items.Add($"{DateTime.Now:HH:mm:ss} - {(isIdle ? "Idle" : "Active")} (Idle {idleTime/1000}s)");
        }

        // Ambil scrennshot dari semua monitor dan simpan gambarnya
        private void ScreenshotTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                var timestamp = DateTime.Now;                                    // Menyimpan waktu untuk nama sub-direktori untuk menyimpan screenshot
                var dateFolder = Path
                    .Combine(_screenshotDir, timestamp.ToString("yyyy-MM-dd"));  // Membuat sub-direktori untuk menyimpan screenshot
                if (!Directory.Exists(dateFolder))                               // Validasi jika direkti screenschot belum ada, maka buat direktori
                    Directory.CreateDirectory(dateFolder);

                foreach (var screen in Screen.AllScreens)                        // Mendapatkan tampilan dari semua layar yang terdeteksi
                {
                    Rectangle bounds = screen.Bounds;                            // Mendapatkan dimensi dari layar
                    using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height)) // Membuat bitmap dan mengambil ukuran lebar dan tinggi dari screen yang dibuka
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                        }

                        // Membuat filename screenshotnya
                        string fileName = Path.Combine(dateFolder, $"screen_{screen.DeviceName.Replace('\\', '_')}_{timestamp:HHmmss}.png");
                        bmp.Save(fileName, ImageFormat.Png);                     // Menyimpan gambar dengan format .png
                    }
                }

                listLog.Items.Add($"Screenshot saved: {timestamp:HH:mm:ss}");    // Menambahkan log ke dalam listBox bahwa scrennshot berhasil disimpan
            }
            catch (Exception ex)
            {
                listLog.Items.Add($"Screenshot error: {ex.Message}");            // Memberi log error ke dalam listBox bahwa screenshot gagal diambil
            }
        }

        // Memperbarui durasi kerja di UI
        private void Timer_Tick(object sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _startTime;
            labelStatus.Text = $"Tracking: {elapsed:hh\\:mm\\:ss}";
        }

        // Memulai tracking
        private void btnStart_Click(object sender, EventArgs e)
        {
            if (_tracking) return;      // Jika sedang melakukan tracking maka tidak akan menjalankan fungsi dibawahnya
            _startTime = DateTime.Now;  // Menetapkan waktu memulai

            _timer.Start();
            _screenshotTimer.Start();
            _activityTimer.Start();

            _tracking = true;          // Set state flag _tracking ke true
            btnStart.Enabled = false;
            btnStop.Enabled = true;

            LogEntry("START", _startTime);
        }

        // Stop tracking
        private void btnStop_Click(object sender, EventArgs e)
        {
            if (!_tracking) return;    // Jika tidak sedang melakukan tracking maka tidak akan menjalankan fungsi dibawahnya

            _timer.Stop();
            _screenshotTimer.Stop();
            _activityTimer.Stop();

            _tracking = false;         // Set state flag _tracking ke false
            btnStart.Enabled = true;
            btnStop.Enabled = false;

            labelStatus.Text = "Not Tracking";

            LogEntry("STOP", DateTime.Now, DateTime.Now - _startTime);
        }

        // Memastikan log CSV telah ada atau buat baru
        private void EnsureLogFile()
        {
            // Jika file belum ada maka buat file dan tambahkan isi dari header csv
            if (!File.Exists(_logFile))
                File.WriteAllText(_logFile, "Event,TimeStamp,Duration\r\n");
        }

        // Menambahkan event, beserta dengan waktu kerja ke dalam log
        private void LogEntry(string eventType, DateTime timestamp, TimeSpan? duration = null)
        {
            var line = duration == null
                ? $"{eventType},{timestamp:yyyy-MM-dd HH:mm:ss}," // Ketika start time
                : $"{eventType},{timestamp:yyyy-MM-dd HH:mm:ss},{duration:hh\\:mm\\:ss}";
            File.AppendAllText(_logFile, line + "\r\n");
        }

        // Berhentikan timer ketika form ditutup
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Stop semua timer saat form ditutup agar tidak tejadi StackOverflowException
            _timer?.Stop();
            _screenshotTimer?.Stop();
            _activityTimer?.Stop();
        }
    }
}
