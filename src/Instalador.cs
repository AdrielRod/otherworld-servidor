// Instalador Otherworld — instala e atualiza o modpack do servidor dos amigos na pasta do TLauncher.
// Compilar: csc /target:winexe /out:InstaladorOtherworld.exe /win32icon:icone.ico /r:System.Web.Extensions.dll
//           /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll Instalador.cs Nbt.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Instalador Otherworld")]
[assembly: System.Reflection.AssemblyProduct("Instalador Otherworld")]
[assembly: System.Reflection.AssemblyDescription("Instala e atualiza o modpack Otherworld do servidor dos amigos")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace InstaladorOtherworld
{
    static class Config
    {
        // Endereço do manifest publicado no GitHub Releases. Um arquivo manifest-url.txt ao lado do .exe sobrescreve (para testes).
        public const string ManifestUrl = "https://github.com/AdrielRod/otherworld-servidor/releases/latest/download/manifest.json";
        public const string StateFile = "otherworld-instalador.json";
    }

    class ModEntry { public string file; public string dir; public string sha1; public string url; public long size; public string Key { get { return (dir ?? "mods") + "/" + file; } } }
    class Component { public string id; public string label; public string target; public bool replace; public string url; public string sha1; public long size; }
    class ServerEntry { public string name; public string ip; }
    class Manifest
    {
        public string name; public string version; public string mcVersion; public string forge;
        public ServerEntry server; public List<ModEntry> mods; public List<Component> components;
        public string optionsUrl; public string optionsSha1;
    }
    class State
    {
        public string version; public List<string> mods = new List<string>();
        public Dictionary<string, string> components = new Dictionary<string, string>();
        public string optionsSha1;
    }

    class MainForm : Form
    {
        readonly Label status = new Label();
        readonly ProgressBar bar = new ProgressBar();
        readonly TextBox log = new TextBox();
        readonly Button go = new Button();
        readonly Button folder = new Button();
        readonly string mc = PastaDoJogo();

        // pasta-minecraft.txt ao lado do .exe troca a pasta de destino (usado só para testar sem mexer no jogo real)
        static string PastaDoJogo()
        {
            string over = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "pasta-minecraft.txt");
            if (File.Exists(over)) return File.ReadAllText(over).Trim();
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        }
        StreamWriter logFile;

        public MainForm()
        {
            Text = "Instalador Otherworld";
            ClientSize = new Size(620, 460);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10f);
            BackColor = Color.FromArgb(30, 26, 39);
            ForeColor = Color.FromArgb(236, 231, 244);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var title = new Label { Text = "Otherworld", Font = new Font("Georgia", 22f, FontStyle.Bold), AutoSize = true, Location = new Point(24, 18), ForeColor = Color.FromArgb(183, 150, 240) };
            var sub = new Label { Text = "Modpack do servidor dos amigos · Minecraft 1.20.1 · Forge", AutoSize = true, Location = new Point(27, 62), ForeColor = Color.FromArgb(167, 159, 182) };
            status.SetBounds(26, 100, 568, 24);
            status.Text = "Pronto para instalar na pasta: " + mc;
            bar.SetBounds(26, 128, 568, 22);
            log.SetBounds(26, 162, 568, 220);
            log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical;
            log.BackColor = Color.FromArgb(21, 18, 27); log.ForeColor = Color.FromArgb(220, 214, 230); log.BorderStyle = BorderStyle.FixedSingle;
            log.Font = new Font("Consolas", 9f);
            go.SetBounds(26, 396, 250, 40); go.Text = "Instalar / Atualizar";
            go.FlatStyle = FlatStyle.Flat; go.BackColor = Color.FromArgb(106, 63, 181); go.ForeColor = Color.White;
            go.FlatAppearance.BorderSize = 0; go.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            folder.SetBounds(290, 396, 200, 40); folder.Text = "Abrir pasta do jogo";
            folder.FlatStyle = FlatStyle.Flat; folder.BackColor = Color.FromArgb(43, 35, 64); folder.ForeColor = Color.White;
            folder.FlatAppearance.BorderSize = 0;
            Controls.AddRange(new Control[] { title, sub, status, bar, log, go, folder });

            go.Click += async (s, e) => await Run();
            folder.Click += (s, e) => { Directory.CreateDirectory(mc); Process.Start("explorer.exe", mc); };
            Log("Este programa só mexe na pasta .minecraft do seu usuário e não pede administrador.");
            Log("Antes de instalar, ele guarda uma cópia das suas pastas atuais em .minecraft\\backup.");
            Log("Feche o Minecraft e o TLauncher antes de continuar.");
        }

        void Log(string msg)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), msg); return; }
            log.AppendText(msg + Environment.NewLine);
            try { if (logFile != null) { logFile.WriteLine(DateTime.Now.ToString("HH:mm:ss ") + msg); logFile.Flush(); } } catch { }
        }
        void Status(string msg) { if (InvokeRequired) { BeginInvoke(new Action<string>(Status), msg); return; } status.Text = msg; }
        void Progress(long done, long total)
        {
            if (InvokeRequired) { BeginInvoke(new Action<long, long>(Progress), done, total); return; }
            bar.Maximum = 1000; bar.Value = total <= 0 ? 0 : (int)Math.Min(1000, done * 1000 / total);
        }

        async Task Run()
        {
            go.Enabled = false;
            try
            {
                if (Process.GetProcessesByName("javaw").Length > 0)
                {
                    var r = MessageBox.Show("Parece que o Minecraft está aberto. Feche o jogo antes de continuar.\n\nContinuar mesmo assim?", "Minecraft aberto", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r != DialogResult.Yes) return;
                }
                Directory.CreateDirectory(mc);
                logFile = new StreamWriter(Path.Combine(mc, "instalador-otherworld-log.txt"), true, Encoding.UTF8);
                await Task.Run(() => Install());
                Status("Pronto! Abra o TLauncher, escolha Forge 1.20.1 e coloque 8 GB de memória.");
                Progress(1, 1);
                MessageBox.Show("Instalação concluída!\n\n1. Abra o TLauncher\n2. Escolha a versão Forge 1.20.1\n3. Nas configurações, coloque 8 GB de memória\n4. Jogue: o servidor já está na lista do Multijogador.", "Instalador Otherworld", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("ERRO: " + ex.Message);
                Status("Deu erro. Mande o arquivo instalador-otherworld-log.txt (na pasta do jogo) para o Adriel.");
                MessageBox.Show("Deu erro na instalação:\n\n" + ex.Message + "\n\nNada foi perdido: o backup está em .minecraft\\backup.", "Instalador Otherworld", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                go.Enabled = true;
                if (logFile != null) { logFile.Dispose(); logFile = null; }
            }
        }

        // ---------------------------------------------------------------- instalação

        void Install()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = 8;
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

            Status("Baixando a lista de arquivos...");
            string url = Config.ManifestUrl;
            string over = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "manifest-url.txt");
            if (File.Exists(over)) url = File.ReadAllText(over).Trim();
            var manifest = js.Deserialize<Manifest>(DownloadString(url));
            Log("Modpack: " + manifest.name + " versão " + manifest.version + " (" + manifest.mods.Count + " mods)");

            string statePath = Path.Combine(mc, Config.StateFile);
            State state = File.Exists(statePath) ? js.Deserialize<State>(File.ReadAllText(statePath)) : null;
            bool first = state == null;
            if (first) state = new State();

            string modsDir = Path.Combine(mc, "mods");
            if (first)
            {
                Status("Guardando cópia das suas pastas atuais...");
                string bk = Path.Combine(mc, "backup", "antes-do-otherworld-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm"));
                Directory.CreateDirectory(bk);
                foreach (var d in new[] { "mods", "config", "defaultconfigs", "configureddefaults" })
                {
                    string p = Path.Combine(mc, d);
                    if (Directory.Exists(p)) { Directory.Move(p, Path.Combine(bk, d)); Log("Backup: " + d); }
                }
                foreach (var f in new[] { "options.txt", "servers.dat" })
                {
                    string p = Path.Combine(mc, f);
                    if (File.Exists(p)) { File.Copy(p, Path.Combine(bk, f), true); Log("Backup: " + f); }
                }
                Log("Cópia guardada em " + bk);

                // reaproveita do backup os mods que já são exatamente os do pack, para não baixar de novo
                Directory.CreateDirectory(modsDir);
                string oldMods = Path.Combine(bk, "mods");
                int reused = 0;
                if (Directory.Exists(oldMods))
                    foreach (var m in manifest.mods.Where(x => (x.dir ?? "mods") == "mods"))
                    {
                        string p = Path.Combine(oldMods, m.file);
                        if (File.Exists(p) && Sha1(p).Equals(m.sha1, StringComparison.OrdinalIgnoreCase)) { File.Copy(p, Path.Combine(modsDir, m.file), true); reused++; }
                    }
                if (reused > 0) Log("Mods reaproveitados (já estavam certos): " + reused);
            }
            Directory.CreateDirectory(modsDir);

            // arquivos do CurseForge (mods, resource packs, shaders): remove os que saíram do pack, baixa os que faltam ou mudaram
            var wanted = new HashSet<string>(manifest.mods.Select(m => m.Key), StringComparer.OrdinalIgnoreCase);
            foreach (var old in state.mods.Where(f => !wanted.Contains(f)))
            {
                string p = Path.Combine(mc, old.Contains("/") ? old : "mods/" + old);
                if (File.Exists(p)) { File.Delete(p); Log("Removido (saiu do pack): " + old); }
            }
            Status("Conferindo os mods...");
            var todo = manifest.mods.Where(m => { string p = FilePath(m); return !File.Exists(p) || !Sha1(p).Equals(m.sha1, StringComparison.OrdinalIgnoreCase); }).ToList();
            var comps = manifest.components.Where(c => !state.components.ContainsKey(c.id) || state.components[c.id] != c.sha1 || !Directory.Exists(Path.Combine(mc, c.target))).ToList();
            long total = todo.Sum(m => m.size) + comps.Sum(c => c.size), done = 0;
            Log(todo.Count == 0 ? "Mods em dia." : "Mods para baixar: " + todo.Count);

            int n = 0;
            Parallel.ForEach(todo, new ParallelOptions { MaxDegreeOfParallelism = 4 }, m =>
            {
                string dest = FilePath(m);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                DownloadVerified(m.url, dest, m.sha1, b => { long d = Interlocked.Add(ref done, b); Progress(d, total); });
                int k = Interlocked.Increment(ref n);
                Status("Baixando mods: " + k + " de " + todo.Count + " (" + (Interlocked.Read(ref done) >> 20) + " de " + (total >> 20) + " MB)");
            });
            state.mods = manifest.mods.Select(m => m.Key).ToList();
            Save(js, statePath, state);

            // componentes: configs, resource packs, shaders, tradução
            foreach (var c in comps)
            {
                Status("Instalando " + c.label + "...");
                string tmp = Path.Combine(Path.GetTempPath(), "otherworld-" + c.id + ".zip");
                DownloadVerified(c.url, tmp, c.sha1, b => { long d = Interlocked.Add(ref done, b); Progress(d, total); });
                string target = Path.Combine(mc, c.target);
                if (c.replace && Directory.Exists(target)) Directory.Delete(target, true);
                Directory.CreateDirectory(target);
                ExtractOver(tmp, target);
                File.Delete(tmp);
                state.components[c.id] = c.sha1;
                Save(js, statePath, state);
                Log("Instalado: " + c.label);
            }

            // options.txt: aplica só as linhas do pack (idioma, resource packs, teclas), mantendo o resto do jogador
            if (state.optionsSha1 != manifest.optionsSha1 || first)
            {
                Status("Aplicando teclas e idioma...");
                string ours = Encoding.UTF8.GetString(DownloadBytes(manifest.optionsUrl));
                // na 1ª instalação aplica tudo (gráficos ajustados pelo autor); depois, só idioma, resource packs e teclas
                MergeOptions(Path.Combine(mc, "options.txt"), ours, first);
                state.optionsSha1 = manifest.optionsSha1;
                Save(js, statePath, state);
                Log("Teclas sem conflito e jogo em português aplicados.");
            }

            // servidor na lista do multijogador
            if (Nbt.EnsureServer(Path.Combine(mc, "servers.dat"), manifest.server.name, manifest.server.ip))
                Log("Servidor adicionado ao Multijogador: " + manifest.server.ip);

            state.version = manifest.version;
            Save(js, statePath, state);
            Log("Tudo pronto. Versão instalada: " + manifest.version);
        }

        string FilePath(ModEntry m) { return Path.Combine(mc, m.dir ?? "mods", m.file); }

        static void Save(JavaScriptSerializer js, string path, State s) { File.WriteAllText(path, js.Serialize(s)); }

        static readonly string[] ManagedOptions = { "lang", "resourcePacks", "incompatibleResourcePacks" };

        static void MergeOptions(string path, string ours, bool everything)
        {
            var mine = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
            var index = new Dictionary<string, int>();
            for (int i = 0; i < mine.Count; i++) { int c = mine[i].IndexOf(':'); if (c > 0) index[mine[i].Substring(0, c)] = i; }
            foreach (var line in ours.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0))
            {
                int c = line.IndexOf(':'); if (c <= 0) continue;
                string key = line.Substring(0, c);
                if (!everything && !key.StartsWith("key_") && !ManagedOptions.Contains(key)) continue;
                if (index.ContainsKey(key)) mine[index[key]] = line; else { index[key] = mine.Count; mine.Add(line); }
            }
            File.WriteAllLines(path, mine, new UTF8Encoding(false));
        }

        static void ExtractOver(string zip, string target)
        {
            string root = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
            using (var z = ZipFile.OpenRead(zip))
                foreach (var e in z.Entries)
                {
                    string dest = Path.GetFullPath(Path.Combine(target, e.FullName));
                    if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue; // ignora caminhos fora da pasta
                    if (e.FullName.EndsWith("/")) { Directory.CreateDirectory(dest); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    e.ExtractToFile(dest, true);
                }
        }

        // ---------------------------------------------------------------- rede e hash

        static string Sha1(string path)
        {
            using (var s = File.OpenRead(path)) using (var h = SHA1.Create())
                return BitConverter.ToString(h.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
        }

        static WebRequest Req(string url)
        {
            var r = WebRequest.Create(url);
            r.Timeout = 60000;
            var h = r as HttpWebRequest;
            if (h != null) { h.UserAgent = "InstaladorOtherworld/1.0"; h.AllowAutoRedirect = true; h.ReadWriteTimeout = 120000; }
            return r;
        }

        static string DownloadString(string url) { return Encoding.UTF8.GetString(DownloadBytes(url)); }

        static byte[] DownloadBytes(string url)
        {
            using (var resp = Req(url).GetResponse()) using (var s = resp.GetResponseStream()) using (var ms = new MemoryStream())
            { s.CopyTo(ms); return ms.ToArray(); }
        }

        void DownloadVerified(string url, string dest, string sha1, Action<long> onBytes)
        {
            for (int attempt = 1; ; attempt++)
            {
                string part = dest + ".baixando";
                long got = 0;
                try
                {
                    using (var resp = Req(url).GetResponse()) using (var s = resp.GetResponseStream()) using (var f = File.Create(part))
                    {
                        var buf = new byte[81920]; int r;
                        while ((r = s.Read(buf, 0, buf.Length)) > 0) { f.Write(buf, 0, r); got += r; onBytes(r); }
                    }
                    if (!Sha1(part).Equals(sha1, StringComparison.OrdinalIgnoreCase)) throw new IOException("arquivo veio corrompido");
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(part, dest);
                    return;
                }
                catch (Exception ex)
                {
                    onBytes(-got);
                    try { File.Delete(part); } catch { }
                    if (attempt >= 3) throw new Exception("Não consegui baixar " + Path.GetFileName(dest) + ": " + ex.Message);
                    Log("Tentando de novo " + Path.GetFileName(dest) + " (" + ex.Message + ")");
                    Thread.Sleep(2000 * attempt);
                }
            }
        }

        // modo de teste sem janela: InstaladorOtherworld.exe --teste (grava tudo no log e sai com código 0 ou 1)
        public int RunHeadless()
        {
            Directory.CreateDirectory(mc);
            logFile = new StreamWriter(Path.Combine(mc, "instalador-otherworld-log.txt"), true, Encoding.UTF8);
            try { Install(); return 0; }
            catch (Exception ex) { Log("ERRO: " + ex); return 1; }
            finally { logFile.Dispose(); }
        }

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Contains("--teste")) return new MainForm().RunHeadless();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }
}
