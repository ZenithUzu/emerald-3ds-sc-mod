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
        readonly CheckBox export = new CheckBox { Text = "Export LayeredFS mod", Checked = true, AutoSize = true };
        readonly CheckBox install = new CheckBox { Text = "Install mod in Azahar on this PC", AutoSize = true };
        readonly Button build = new Button { Text = "Build CXI", AutoSize = true };
        readonly Label status = new Label { AutoSize = true };

        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BuilderWindow());
        }

        BuilderWindow() {
            Text = "Emerald 3DS · Azahar Builder";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(780, 405);
            MinimumSize = new Size(610, 350);
            Font = SystemFonts.MessageBoxFont;
            var panel = new TableLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 3, AutoScroll = true
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Controls.Add(panel);
            Wide(panel, new Label { Text = "Choose the Emerald 3DS download from the official builder, or a compatible CXI.", AutoSize = true });
            Line(panel, "Game folder or file", port, true);
            var chooseFolder = new Button { Text = "Choose game folder…", AutoSize = true };
            Wide(panel, chooseFolder);
            chooseFolder.Click += (sender, args) => {
                using (var dialog = new FolderBrowserDialog { ShowNewFolderButton = false }) {
                    dialog.Description = "Select the folder extracted from the official Emerald 3DS download.";
                    if (dialog.ShowDialog(this) == DialogResult.OK) port.Text = dialog.SelectedPath;
                }
            };
            Wide(panel, new Label { Text = "Folder import: official Emerald 3DS v0.5.0, Spanish.", AutoSize = true });
            port.AllowDrop = true;
            port.DragEnter += (sender, args) => {
                if (args.Data.GetDataPresent(DataFormats.FileDrop)) args.Effect = DragDropEffects.Copy;
            };
            port.DragDrop += (sender, args) => {
                var paths = (string[])args.Data.GetData(DataFormats.FileDrop);
                if (paths.Length == 1) port.Text = paths[0];
            };
            Line(panel, "Save CXI as", output, true);
            Line(panel, "Stable ID key (optional)", key, false);
            Wide(panel, export);
            Wide(panel, install);
            Line(panel, "Azahar user folder", emulator, true);
            Wide(panel, build);
            Wide(panel, status);
            var openOutput = new Button { Text = "Open output folder", AutoSize = true, Enabled = false };
            Wide(panel, openOutput);
            string lastFolder = null;
            openOutput.Click += (sender, args) => Process.Start("explorer.exe", "\"" + lastFolder + "\"");
            port.TextChanged += (sender, args) => {
                bool existing = IsCxi(port.Text.Trim());
                build.Text = existing ? "Set up mod" : "Build CXI";
                output.Enabled = key.Enabled = !existing;
            };
            output.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Emerald3DS.cxi");
            emulator.Text = FindEmulator();
            install.CheckedChanged += (sender, args) => emulator.Enabled = install.Checked;
            emulator.Enabled = false;
            build.Click += async (sender, args) => {
                var input = port.Text.Trim();
                var dest = output.Text.Trim();
                var titleKey = key.Text.Trim();
                var root = install.Checked ? emulator.Text.Trim() : null;
                bool exportMod = export.Checked;
                bool existing = IsCxi(input);
                openOutput.Enabled = false;
                panel.Enabled = false;
                status.Text = "Working…";
                try {
                    var result = await Task.Run(() => existing
                        ? SetUpMod(input, exportMod, root)
                        : ImportGame(input, dest, titleKey, exportMod, root));
                    lastFolder = Path.GetDirectoryName(result.Cxi);
                    openOutput.Enabled = true;
                    status.Text = "Done · Title ID " + result.Id;
                    MessageBox.Show(this, result.Summary(), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                } catch (Exception error) {
                    status.Text = "The operation could not be completed.";
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

        void Line(TableLayoutPanel panel, string label, TextBox box, bool browse) {
            int row = panel.RowCount++;
            panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            box.Dock = DockStyle.Fill;
            panel.Controls.Add(box, 1, row);
            if (!browse) return;
            var button = new Button { Text = "Browse…", AutoSize = true };
            panel.Controls.Add(button, 2, row);
            button.Click += (sender, args) => {
                if (!box.Enabled) return;
                if (box == port) {
                    using (var dialog = new OpenFileDialog {
                        Title = "Open the game inside the extracted Emerald folder", Filter = "Emerald 3DS game (*.3dsx;*.cxi)|*.3dsx;*.cxi",
                        CheckFileExists = true, AutoUpgradeEnabled = true
                    }) {
                        if (dialog.ShowDialog(this) == DialogResult.OK) box.Text = dialog.FileName;
                    }
                    return;
                }
                if (box == output) {
                    using (var dialog = new SaveFileDialog {
                        Title = "Choose the CXI file name and location", Filter = "Nintendo 3DS CXI (*.cxi)|*.cxi",
                        DefaultExt = "cxi", AddExtension = true, OverwritePrompt = false
                    }) {
                        dialog.FileName = Path.GetFileName(box.Text);
                        if (dialog.ShowDialog(this) == DialogResult.OK) box.Text = dialog.FileName;
                    }
                    return;
                }
                using (var dialog = new FolderBrowserDialog()) {
                    if (Directory.Exists(box.Text)) dialog.SelectedPath = box.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) box.Text = dialog.SelectedPath;
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
            if (stream == null) throw new InvalidOperationException("Missing resource: " + name);
            return stream;
        }

        static void CopyTree(string from, string to) {
            Directory.CreateDirectory(to);
            foreach (var file in Directory.EnumerateFiles(from))
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
            foreach (var folder in Directory.EnumerateDirectories(from))
                CopyTree(folder, Path.Combine(to, Path.GetFileName(folder)));
        }

        static string WriteMod(string root, string id) {
            var folder = Path.Combine(root, "load", "mods", id, "romfs", "mods");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "una-pantalla.ini");
            if (File.Exists(file)) return folder;
            using (var src = Resource("mod.ini"))
            using (var dest = new FileStream(file, FileMode.CreateNew))
                src.CopyTo(dest);
            return folder;
        }

        static bool IsCxi(string path) {
            return path.EndsWith(".cxi", StringComparison.OrdinalIgnoreCase);
        }

        internal sealed class Result {
            public string Cxi, Id, Export, Installed;

            public string Summary() {
                var text = "CXI: " + Cxi + "\nTitle ID: " + Id;
                if (Export != null) text += "\n\nExported mod: " + Export;
                if (Installed != null) text += "\n\nInstalled mod: " + Installed;
                return text;
            }
        }

        static void ValidateEmulator(string emulator) {
            if (emulator != null && (emulator.Length == 0 || !Directory.Exists(Path.Combine(emulator, "config"))))
                throw new ArgumentException("Choose the Azahar user folder that contains config.");
        }

        static ulong ReadId(string path, bool checkProduct) {
            using (var reader = new BinaryReader(File.OpenRead(path))) {
                reader.BaseStream.Position = 0x100;
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "NCCH")
                    throw new IOException("The file is not a valid CXI.");
                reader.BaseStream.Position = 0x118;
                ulong id = reader.ReadUInt64();
                reader.BaseStream.Position = 0x150;
                if (checkProduct && Encoding.ASCII.GetString(reader.ReadBytes(16)).TrimEnd('\0') != "CTR-H-EM3S")
                    throw new IOException("Choose an Emerald CXI generated by this fork.");
                return id;
            }
        }

        static Result PlaceMod(string cxi, string id, bool export, string emulator) {
            var result = new Result { Cxi = cxi, Id = id };
            if (export) result.Export = WriteMod(Path.GetDirectoryName(cxi), id);
            if (emulator != null) result.Installed = WriteMod(emulator, id);
            return result;
        }

        internal static Result SetUpMod(string cxi, bool export, string emulator) {
            ValidateEmulator(emulator);
            if (!export && emulator == null) throw new ArgumentException("Select at least one mod destination.");
            cxi = Path.GetFullPath(cxi);
            return PlaceMod(cxi, ReadId(cxi, true).ToString("X16"), export, emulator);
        }

        internal static Result ImportGame(string input, string outputFile, string key, bool export, string emulator) {
            if (String.IsNullOrWhiteSpace(input) || String.IsNullOrWhiteSpace(outputFile))
                throw new ArgumentException("Choose the extracted Emerald game and a CXI output file.");
            ValidateEmulator(emulator);
            string path = Path.GetFullPath(input);
            string folder = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            var temp = Path.Combine(Path.GetTempPath(), "emerald-import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try {
                using (var profile = Resource("official-es.zip"))
                    OfficialFolder.Prepare(folder, temp, profile);
                return Generate(Path.Combine(temp, "emerald3ds.elf"), outputFile,
                                String.IsNullOrWhiteSpace(key) ? "emerald3ds-official-es" : key, export, emulator);
            } finally {
                Directory.Delete(temp, true);
            }
        }

        internal static Result Generate(string input, string outputFile, string key, bool export, string emulator) {
            if (String.IsNullOrWhiteSpace(input) || String.IsNullOrWhiteSpace(outputFile))
                throw new ArgumentException("Choose the input file and CXI output path.");
            ValidateEmulator(emulator);
            var elf = Path.GetFullPath(input);
            if (!elf.EndsWith(".elf", StringComparison.OrdinalIgnoreCase) || !IsCxi(outputFile))
                throw new ArgumentException("Choose an ELF input and a CXI output file.");
            var port = Path.GetDirectoryName(elf);
            var final = Path.GetFullPath(outputFile);
            var output = Path.GetDirectoryName(final);
            if (File.Exists(final)) throw new IOException("That CXI already exists. Choose another file name.");
            var icon = Path.Combine(port, "emerald3ds.smdh");
            var romfs = Path.Combine(port, "romfs");
            if (!File.Exists(elf) || !File.Exists(icon) ||
                !File.Exists(Path.Combine(romfs, "data.embedded")) ||
                !File.Exists(Path.Combine(romfs, "engine", "abi.bin")))
                throw new ArgumentException("Choose emerald3ds.elf from a build with embedded data, alongside emerald3ds.smdh and romfs.");
            if (File.Exists(Path.Combine(romfs, "mods", "una-pantalla.ini")))
                throw new ArgumentException("Select a build whose RomFS does not already contain the mod.");
            if (output.TrimEnd('\\').StartsWith(romfs.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(output, romfs, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose an output path outside the input RomFS.");
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
                        throw new IOException("makerom could not create the CXI. " + errors);
                }
                if (ReadId(tempCxi, true) != programId)
                    throw new IOException("The generated CXI has an unexpected Title ID.");
                File.Move(tempCxi, final);
                return PlaceMod(final, id, export, emulator);
            } finally {
                Directory.Delete(temp, true);
            }
        }
    }
}
