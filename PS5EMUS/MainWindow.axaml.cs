using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using FluentFTP;

namespace PS5EMUS
{
    public partial class MainWindow : Window
    {
        private const int PAYLOAD_PORT = 9026;
        private const int FTP_PORT = 1337;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void OnBrowseClicked(object sender, RoutedEventArgs e)
        {
            var folders = await this.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Seleccionar Carpeta de ROMs", AllowMultiple = false });
            if (folders.Count > 0)
            {
                var pathInput = this.FindControl<TextBox>("PathInput");
                pathInput.Text = folders[0].Path.LocalPath;
            }
        }

        private async void OnLaunchClicked(object sender, RoutedEventArgs e)
        {
            var ipInput = this.FindControl<TextBox>("IpInput");
            var pathInput = this.FindControl<TextBox>("PathInput");
            var skipUpload = this.FindControl<CheckBox>("SkipUpload");
            var emuSelector = this.FindControl<ComboBox>("EmuSelector");

            string ip = ipInput?.Text ?? "";
            string romsPath = pathInput?.Text ?? "";
            bool skip = skipUpload?.IsChecked ?? false;

            // Configuración dinámica según emulador
            string luaFile = emuSelector.SelectedIndex == 0 ? "nes.lua" : "snes.lua";
            string[] extensions = emuSelector.SelectedIndex == 0 ?
                                 new[] { "*.nes" } :
                                 new[] { "*.sfc", "*.smc" };

            Log($"Iniciando {luaFile}...");

            try
            {
                string luaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, luaFile);
                if (!File.Exists(luaPath))
                {
                    Log($"ERROR: No se encontró {luaFile} en la carpeta de la app.");
                    return;
                }

                await SendPayloadAsync(ip, luaPath);
            }
            catch (Exception ex) { Log($"Error: {ex.Message}"); return; }

            if (skip) return;

            await Task.Delay(2500); // Espera para que el FTP de PS5 inicie

            try
            {
                await UploadRomsAsync(ip, romsPath, extensions);
            }
            catch (Exception ex) { Log($"Error FTP: {ex.Message}"); }
        }

        private async Task SendPayloadAsync(string host, string filePath)
        {
            byte[] data = await File.ReadAllBytesAsync(filePath);
            using var client = new TcpClient();
            await client.ConnectAsync(host, PAYLOAD_PORT);
            using var stream = client.GetStream();
            await stream.WriteAsync(data, 0, data.Length);
            Log("Payload enviado con éxito.");
        }

        private async Task UploadRomsAsync(string host, string folderPath, string[] extensions)
        {
            if (!Directory.Exists(folderPath)) { Log("Carpeta de ROMs no encontrada."); return; }

            List<string> files = new List<string>();
            foreach (var ext in extensions)
            {
                files.AddRange(Directory.GetFiles(folderPath, ext));
            }

            if (files.Count == 0) { Log("No se encontraron ROMs compatibles."); return; }

            using var ftp = new AsyncFtpClient(host, "anonymous", "", FTP_PORT);
            await ftp.Connect();

            Log($"Conectado. Subiendo {files.Count} archivos...");
            foreach (var file in files)
            {
                string name = Path.GetFileName(file);
                Log($">> {name}");
                await ftp.UploadFile(file, name, FtpRemoteExists.Skip);
            }

            try { await ftp.Execute("SITE EXIT"); } catch { }
            await ftp.Disconnect();
            Log("¡Todo listo! Disfruta.");
        }

        private void Log(string message)
        {
            var statusLog = this.FindControl<TextBlock>("StatusLog");
            if (statusLog != null)
            {
                statusLog.Text = $"[{DateTime.Now:HH:mm:ss}] {message}\n" + statusLog.Text;
            }
        }
    }
}