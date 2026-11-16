using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Emerald3DS {
    internal sealed class BuilderWindow : Form {
        readonly TextBox port = new TextBox();
        readonly TextBox output = new TextBox();
        readonly TextBox key = new TextBox();
        readonly TextBox emulator = new TextBox();
        readonly CheckBox export = new CheckBox { Text = "Exportar el mod separado", Checked = true, AutoSize = true };
        readonly CheckBox install = new CheckBox { Text = "Instalar el mod en Azahar del PC", AutoSize = true };
        readonly Button build = new Button { Text = "Generar CXI", AutoSize = true };
        readonly Label status = new Label { AutoSize = true };

        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BuilderWindow());
        }

        BuilderWindow() {
            Text = "Emerald 3DS · Generador para Azahar";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(675, 320);
            MinimumSize = new Size(610, 350);
            Font = SystemFonts.MessageBoxFont;
            var panel = new TableLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 3, AutoScroll = true
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Controls.Add(panel);
            Line(panel, "Compilación de Emerald", port, true);
            Line(panel, "Carpeta de salida", output, true);
            Line(panel, "Clave fija del ID (opcional)", key, false);
            Wide(panel, export);
            Wide(panel, install);
            Line(panel, "Carpeta de Azahar", emulator, true);
            Wide(panel, build);
            Wide(panel, status);
            var staged = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "build", "upstream", "3ds_port");
            if (File.Exists(Path.Combine(staged, "emerald3ds.elf"))) port.Text = staged;
            output.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Emerald3DS-Azahar");
            emulator.Text = FindEmulator();
            install.CheckedChanged += (sender, args) => emulator.Enabled = install.Checked;
            emulator.Enabled = false;
            build.Click += async (sender, args) => {
                var input = port.Text.Trim();
                var dest = output.Text.Trim();
                var titleKey = key.Text.Trim();
                var root = install.Checked ? emulator.Text.Trim() : null;
                var withMod = export.Checked || install.Checked;
                panel.Enabled = false;
                status.Text = "Generando el CXI…";
                try {
                    var id = await Task.Run(() => Generate(input, dest, titleKey, withMod, root));
                    status.Text = "Listo · Title ID " + id;
                    MessageBox.Show(this, "Archivo creado en " + Path.GetFullPath(dest) +
                        "\nTitle ID " + id, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                } catch (Exception error) {
                    status.Text = "No se completó la generación.";
                    MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                } finally {
                    panel.Enabled = true;
                }
            };
            AcceptButton = build;
        }

        static void Wide(TableLayoutPanel panel, Control control) {
            int row = panel.RowCount++;
            panel.Controls.Add(control, 0, row);
            panel.SetColumnSpan(control, 3);
        }

        static void Line(TableLayoutPanel panel, string label, TextBox box, bool browse) {
            int row = panel.RowCount++;
            panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            box.Dock = DockStyle.Fill;
            panel.Controls.Add(box, 1, row);
            if (!browse) return;
            var button = new Button { Text = "Examinar…", AutoSize = true };
            panel.Controls.Add(button, 2, row);
            button.Click += (sender, args) => {
                if (!box.Enabled) return;
                using (var dialog = new FolderBrowserDialog()) {
                    if (Directory.Exists(box.Text)) dialog.SelectedPath = box.Text;
                    if (dialog.ShowDialog() == DialogResult.OK) box.Text = dialog.SelectedPath;
                }
            };
        }

        static string FindEmulator() {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var plus = Path.Combine(appData, "AzaharPlus");
            var standard = Path.Combine(appData, "Azahar");
            bool hasPlus = Directory.Exists(Path.Combine(plus, "config"));
            bool hasStandard = Directory.Exists(Path.Combine(standard, "config"));
            return hasPlus == hasStandard ? "" : hasPlus ? plus : standard;
        }

        static Stream Resource(string name) {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream == null) throw new InvalidOperationException("Falta el recurso " + name);
            return stream;
        }

        static void CopyTree(string from, string to) {
            Directory.CreateDirectory(to);
            foreach (var file in Directory.EnumerateFiles(from))
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
            foreach (var folder in Directory.EnumerateDirectories(from))
                CopyTree(folder, Path.Combine(to, Path.GetFileName(folder)));
        }

        static void WriteMod(string root, string id) {
            var folder = Path.Combine(root, "load", "mods", id, "romfs", "mods");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "una-pantalla.ini");
            if (File.Exists(file)) return;
            using (var src = Resource("mod.ini"))
            using (var dest = new FileStream(file, FileMode.CreateNew))
                src.CopyTo(dest);
        }

        internal static string Generate(string port, string output, string key, bool export, string emulator) {
            if (String.IsNullOrWhiteSpace(port) || String.IsNullOrWhiteSpace(output))
                throw new ArgumentException("Selecciona la carpeta de compilación y la carpeta de salida.");
            if (emulator != null && (emulator.Length == 0 || !Directory.Exists(Path.Combine(emulator, "config"))))
                throw new ArgumentException("Selecciona la carpeta de usuario de Azahar que contiene config.");
            port = Path.GetFullPath(port);
            output = Path.GetFullPath(output);
            var elf = Path.Combine(port, "emerald3ds.elf");
            var icon = Path.Combine(port, "emerald3ds.smdh");
            var romfs = Path.Combine(port, "romfs");
            if (!File.Exists(elf) || !File.Exists(icon) ||
                !File.Exists(Path.Combine(romfs, "data.embedded")) ||
                !File.Exists(Path.Combine(romfs, "engine", "abi.bin")))
                throw new ArgumentException("Selecciona build/upstream/3ds_port de una compilación con datos incrustados.");
            if (File.Exists(Path.Combine(romfs, "mods", "una-pantalla.ini")))
                throw new ArgumentException("La RomFS de compilación ya contiene el archivo del mod. Usa la RomFS original.");
            if (output.TrimEnd('\\').StartsWith(romfs.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(output, romfs, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Selecciona una salida fuera de romfs.");
            int unique;
            using (var sha = SHA256.Create()) {
                var prefix = Encoding.UTF8.GetBytes("emerald3ds-azahar\0");
                sha.TransformBlock(prefix, 0, prefix.Length, prefix, 0);
                if (String.IsNullOrEmpty(key)) {
                    using (var file = File.OpenRead(elf)) {
                        var buffer = new byte[1024 * 1024];
                        int count;
                        while ((count = file.Read(buffer, 0, buffer.Length)) != 0)
                            sha.TransformBlock(buffer, 0, count, buffer, 0);
                    }
                } else {
                    var tag = Encoding.UTF8.GetBytes(key);
                    sha.TransformBlock(tag, 0, tag.Length, tag, 0);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                unique = 0xF0000 | (sha.Hash[0] << 8) | sha.Hash[1];
            }
            ulong programId = 0x0004000000000000UL | ((ulong)unique << 8);
            string id = programId.ToString("X16");
            Directory.CreateDirectory(output);
            var temp = Path.Combine(output, ".emerald-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try {
                var packer = Path.Combine(temp, "makerom.exe");
                using (var src = Resource("makerom.exe"))
                using (var dest = File.Create(packer)) src.CopyTo(dest);
                var staged = Path.Combine(temp, "romfs");
                CopyTree(romfs, staged);
                Directory.CreateDirectory(Path.Combine(staged, "mods"));
                File.WriteAllText(Path.Combine(staged, "mods", "una-pantalla.ini"),
                    "[emerald3ds-screen-mod-disabled]\n");
                var spec = Path.Combine(temp, "app.rsf");
                using (var reader = new StreamReader(Resource("azahar.rsf")))
                    File.WriteAllText(spec, reader.ReadToEnd()
                        .Replace("@UNIQUE_ID@", "0x" + unique.ToString("X5"))
                        .Replace("@ROMFS@", staged.Replace('\\', '/')));
                var tempCxi = Path.Combine(temp, "app.cxi");
                var start = new ProcessStartInfo(packer,
                    "-f ncch -o \"" + tempCxi + "\" -rsf \"" + spec + "\" -elf \"" + elf +
                    "\" -icon \"" + icon + "\" -target t") {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true
                };
                using (var process = Process.Start(start)) {
                    var errors = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                        throw new IOException("makerom no pudo generar el CXI. " + errors);
                }
                using (var reader = new BinaryReader(File.OpenRead(tempCxi))) {
                    reader.BaseStream.Position = 0x100;
                    if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "NCCH")
                        throw new IOException("El archivo generado no es un CXI válido.");
                    reader.BaseStream.Position = 0x118;
                    if (reader.ReadUInt64() != programId)
                        throw new IOException("El CXI no contiene el Title ID esperado.");
                }
                var final = Path.Combine(output, "Emerald3DS-" + id + ".cxi");
                if (File.Exists(final)) File.Replace(tempCxi, final, null);
                else File.Move(tempCxi, final);
                if (export) WriteMod(output, id);
                if (emulator != null) WriteMod(emulator, id);
                return id;
            } finally {
                Directory.Delete(temp, true);
            }
        }
    }
}
