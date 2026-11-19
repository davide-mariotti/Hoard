using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;
using SystemInformation = System.Windows.Forms.SystemInformation;

namespace HoardApp
{
    public class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            // The WebView2 libraries are packed inside the exe: one file to download, like Sentinel and Vigil.
            AppDomain.CurrentDomain.AssemblyResolve += Libs.Resolve;
            return Run(args);
        }

        // Kept apart from Main: the JIT must not meet a library type before the resolver is in place.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static int Run(string[] args)
        {
            if (Array.IndexOf(args, "--selftest") >= 0) return SelfTest.Run();

            // --restart: the old Hoard is closing to hand over to a new one (as administrator), wait for it
            bool restart = Array.IndexOf(args, "--restart") >= 0;
            bool fromStartup = Array.IndexOf(args, "--startup") >= 0;
            int i = Array.IndexOf(args, "--scan");
            string scanRoot = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            Mutex single = null;
            bool owned = false;
            try
            {
                single = new Mutex(false, "Local\\HoardApp");
                owned = single.WaitOne(restart ? 15000 : 0);
            }
            catch (AbandonedMutexException) { owned = true; }
            catch (UnauthorizedAccessException) { }   // the running Hoard is elevated: it's there all the same
            if (!owned)
            {
                if (!fromStartup) MessageBox.Show(L.T("Hoard è già acceso: lo trovi nell'area di notifica, vicino all'orologio.",
                                                      "Hoard is already running: look for it in the notification area, next to the clock."),
                                                  "Hoard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new HoardContext(fromStartup, scanRoot));
            }
            finally { single.ReleaseMutex(); }
            return 0;
        }
    }

    public static class Libs
    {
        static readonly Dictionary<string, Assembly> Loaded = new Dictionary<string, Assembly>();

        static byte[] Resource(string name)
        {
            using (Stream s = Open(name))
            {
                if (s == null) return null;
                var m = new MemoryStream();
                s.CopyTo(m);
                return m.ToArray();
            }
        }

        public static Stream Open(string name) { return Assembly.GetExecutingAssembly().GetManifestResourceStream(name); }

        public static Assembly Resolve(object sender, ResolveEventArgs e)
        {
            string file = new AssemblyName(e.Name).Name + ".dll";
            lock (Loaded)
            {
                Assembly a;
                if (Loaded.TryGetValue(file, out a)) return a;
                byte[] bytes = Resource(file);
                if (bytes == null) return null;
                a = Assembly.Load(bytes);
                Loaded[file] = a;
                return a;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibrary(string path);

        // A native DLL can't be loaded from memory: it goes to disk once, then into the process by full path.
        public static void LoadNative(string name, string folder)
        {
            byte[] bytes = Resource(name);
            string path = Path.Combine(folder, name);
            Directory.CreateDirectory(folder);
            if (!File.Exists(path) || new FileInfo(path).Length != bytes.Length) File.WriteAllBytes(path, bytes);
            if (LoadLibrary(path) == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    // Italian if Windows speaks Italian, English otherwise. Same as Sentinel.
    public static class L
    {
        public static readonly bool It = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "it";
        public static string T(string it, string en) { return It ? it : en; }

        public static Icon AppIcon(Size size)
        {
            using (var s = Libs.Open("Hoard.ico"))
                return s == null ? SystemIcons.Application : new Icon(s, size);
        }
    }

    public static class Json
    {
        static readonly JavaScriptSerializer S = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        public static string Write(object o) { return S.Serialize(o); }
        public static T Read<T>(string s) { return S.Deserialize<T>(s); }
    }

    // What kind of file, from its extension. Eight kinds and the rest: the window gives each one a colour.
    public static class Cats
    {
        public const int Video = 0, Archive = 1, Image = 2, Program = 3, Audio = 4, Code = 5, Document = 6, System = 7, Other = 8, Count = 9;
        static readonly Dictionary<string, int> Map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        static Cats()
        {
            Add(Video, "mp4 mkv avi mov wmv webm m4v mpg mpeg ts m2ts mts flv vob 3gp");
            Add(Archive, "zip rar 7z tar gz tgz bz2 xz zst cab iso img vhd vhdx vmdk vdi qcow2 wim esd dmg");
            Add(Image, "jpg jpeg png gif bmp tif tiff webp heic heif avif raw cr2 cr3 nef arw dng orf rw2 psd xcf svg ico");
            Add(Program, "exe dll msi msix msixbundle appx appxbundle msp ocx mui drv");
            Add(Program, "pak vpk ucas utoc bundle assets resource forge bdt rpf bsa ba2 wad pck cpk");   // game data: programs and games share a colour
            Add(Audio, "mp3 flac wav aac m4a ogg opus wma aiff aif mid");
            Add(Code, "js mjs cjs tsx jsx py pyc pyd cs java class jar c h cpp hpp go rs rb php html css scss json xml yml yaml map pdb obj o lib a nupkg whl node");
            Add(Document, "pdf doc docx xls xlsx xlsm ppt pptx odt ods odp txt rtf md csv epub mobi one pst ost");
            Add(System, "sys log etl evtx dmp tmp temp cache db sqlite edb bak old");
        }

        static void Add(int cat, string exts) { foreach (string e in exts.Split(' ')) Map[e] = cat; }

        public static int Of(string name)
        {
            int dot = name.LastIndexOf('.');
            int c;
            return dot >= 0 && Map.TryGetValue(name.Substring(dot + 1), out c) ? c : Other;
        }

        public static byte Dominant(long[] bytes)
        {
            int best = Other;
            long max = 0;
            for (int i = 0; i < Count; i++) if (bytes[i] > max) { max = bytes[i]; best = i; }
            return (byte)best;
        }
    }

    // The folder listing straight from Windows: FindFirstFileEx hands over name, size and attributes in one go.
    public static class Native
    {
        public const uint Directory = 0x10, ReparsePoint = 0x400, Offline = 0x1000, RecallOnOpen = 0x40000, RecallOnDataAccess = 0x400000;
        public const uint MountPoint = 0xA0000003, Symlink = 0xA000000C;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct FindData
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
            public uint SizeHigh, SizeLow, Reserved0, Reserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string AltName;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr FindFirstFileExW(string path, int infoLevel, out FindData data, int searchOp, IntPtr filter, int flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool FindNextFileW(IntPtr h, out FindData data);
        [DllImport("kernel32.dll")]
        static extern bool FindClose(IntPtr h);

        // One folder, without its sub-folders. Returns 0 or the Windows error (5 = access denied).
        public static int List(string folder, Action<FindData> each)
        {
            FindData f;
            // basic info (no 8.3 names) and large fetch: the two switches that make it fast
            IntPtr h = FindFirstFileExW(LongPath(folder) + "*", 1, out f, 0, IntPtr.Zero, 2);
            if (h == new IntPtr(-1)) return Marshal.GetLastWin32Error();
            try { do { if (f.Name != "." && f.Name != "..") each(f); } while (FindNextFileW(h, out f)); }
            finally { FindClose(h); }
            return 0;
        }

        // \\?\ lifts the 260-character limit: node_modules goes deeper than that
        public static string LongPath(string p)
        {
            if (p.StartsWith(@"\\?\")) return p;
            if (p.StartsWith(@"\\")) return @"\\?\UNC\" + p.Substring(2);
            return @"\\?\" + p;
        }

        // What a file takes on this disk. A OneDrive file kept "online only" takes nothing: it lives on the server.
        public static long Size(FindData f)
        {
            if ((f.Attributes & (Offline | RecallOnOpen | RecallOnDataAccess)) != 0) return 0;
            return ((long)f.SizeHigh << 32) | f.SizeLow;
        }

        // Junctions and links point at a folder that is counted where it really lives (and some point back up: a loop).
        // Other reparse points, like OneDrive's own folders, are real folders.
        public static bool Follow(FindData f)
        {
            if ((f.Attributes & Directory) == 0) return false;
            return (f.Attributes & ReparsePoint) == 0 || (f.Reserved0 != MountPoint && f.Reserved0 != Symlink);
        }
    }

    public class Dir
    {
        public string Name;
        public Dir Parent;
        public Dir[] Subs;                         // null: no sub-folders
        public long Size, Own;                     // the whole branch; the files right here
        public int Files, OwnFiles, Id, Top;
        public long[] Cat = new long[Cats.Count];  // bytes per kind of file in the whole branch; null for small folders after the scan
        public byte Main = Cats.Other, OwnMain = Cats.Other;
        public bool Denied;
    }

    public class Big
    {
        public Dir Dir;
        public string Name;
        public long Size;
        public int Cat;
    }

    // One drive (or folder), read by a few threads at once. Only folders are kept in memory:
    // the files of a folder are listed again when you open it, so a drive with millions of files stays light.
    public class Scan
    {
        public const int Threads = 16;              // ponytail: fixed; 16 read C: in 7 s against 8 in 12 s (warm, NVMe). An old HDD might like fewer
        public const int KeepBig = 100;
        public const long SmallDir = 16L << 20;     // under 16 MB a folder keeps its main kind of file, not the whole breakdown

        public readonly string Root;
        public Dir Tree;
        public Dir[] All = new Dir[0];
        public List<Big> Biggest = new List<Big>();
        public long Files, Dirs, Bytes, Denied;
        public volatile string Current = "";
        public volatile Dir[] Tops = new Dir[0];    // the folders right under the root, for the live bars
        public long[] TopBytes = new long[1];        // one more slot at the end: the files in the root itself
        public volatile bool Cancel, Done;
        public double Seconds;

        readonly List<Big> big = new List<Big>();
        long threshold;

        public Scan(string root) { Root = root.TrimEnd('\\') + "\\"; }

        public string PathOf(Dir d) { return d.Parent == null ? Root : PathOf(d.Parent) + d.Name + "\\"; }

        public void Run()
        {
            var watch = Stopwatch.StartNew();
            Tree = new Dir { Name = Root.TrimEnd('\\'), Top = -1 };
            // a stack, not a queue: deep first, so the waiting list stays short
            var jobs = new BlockingCollection<KeyValuePair<Dir, string>>(new ConcurrentStack<KeyValuePair<Dir, string>>());
            int pending = 1;
            jobs.Add(new KeyValuePair<Dir, string>(Tree, Root));
            Action<Dir, string> push = (d, p) => { Interlocked.Increment(ref pending); jobs.Add(new KeyValuePair<Dir, string>(d, p)); };
            var threads = new List<Thread>();
            for (int i = 0; i < Threads; i++)
            {
                var t = new Thread(() =>
                {
                    foreach (var job in jobs.GetConsumingEnumerable())
                    {
                        if (!Cancel)
                            try { Read(job.Key, job.Value, push); }
                            catch { }   // one odd folder never stops the scan
                        if (Interlocked.Decrement(ref pending) == 0) jobs.CompleteAdding();
                    }
                });
                t.IsBackground = true;
                t.Priority = ThreadPriority.BelowNormal;
                t.Name = "Hoard scan";
                t.Start();
                threads.Add(t);
            }
            foreach (var t in threads) t.Join();
            if (Cancel) return;
            Finish();
            Seconds = watch.Elapsed.TotalSeconds;
            Done = true;
        }

        void Read(Dir d, string path, Action<Dir, string> push)
        {
            Current = path;
            var subs = new List<Dir>();
            long[] own = d.Cat;
            int files = 0;
            bool root = d.Parent == null;
            int err = Native.List(path, f =>
            {
                if ((f.Attributes & Native.Directory) != 0)
                {
                    if (Native.Follow(f)) subs.Add(new Dir { Name = f.Name, Parent = d, Top = root ? subs.Count : d.Top });
                    return;
                }
                long size = Native.Size(f);
                int c = Cats.Of(f.Name);
                own[c] += size;
                files++;
                if (size > Interlocked.Read(ref threshold)) Consider(d, f.Name, size, c);
            });
            if (err == 5) { d.Denied = true; Interlocked.Increment(ref Denied); }
            long bytes = own.Sum();
            d.Own = d.Size = bytes;
            d.OwnFiles = d.Files = files;
            d.OwnMain = Cats.Dominant(own);
            if (subs.Count > 0) d.Subs = subs.ToArray();
            if (root) { TopBytes = new long[subs.Count + 1]; Tops = subs.ToArray(); }
            Interlocked.Add(ref TopBytes[d.Top < 0 ? TopBytes.Length - 1 : d.Top], bytes);
            Interlocked.Add(ref Bytes, bytes);
            Interlocked.Add(ref Files, files);
            Interlocked.Increment(ref Dirs);
            foreach (var s in subs) push(s, path + s.Name + "\\");
        }

        void Consider(Dir d, string name, long size, int cat)
        {
            lock (big)
            {
                if (size <= threshold) return;
                big.Add(new Big { Dir = d, Name = name, Size = size, Cat = cat });
                if (big.Count >= 2 * KeepBig) TrimBig();
            }
        }

        void TrimBig()
        {
            big.Sort((a, b) => b.Size.CompareTo(a.Size));
            if (big.Count > KeepBig) big.RemoveRange(KeepBig, big.Count - KeepBig);
            if (big.Count == KeepBig) Interlocked.Exchange(ref threshold, big[KeepBig - 1].Size);
        }

        // Sizes go up the tree: breadth-first order, walked backwards, meets every folder after all of its sub-folders.
        void Finish()
        {
            var order = new List<Dir> { Tree };
            for (int i = 0; i < order.Count; i++)
            {
                order[i].Id = i;
                if (order[i].Subs != null) order.AddRange(order[i].Subs);
            }
            for (int i = order.Count - 1; i > 0; i--)
            {
                Dir d = order[i], p = d.Parent;
                p.Size += d.Size;
                p.Files += d.Files;
                for (int c = 0; c < Cats.Count; c++) p.Cat[c] += d.Cat[c];
            }
            foreach (var d in order)
            {
                d.Main = Cats.Dominant(d.Cat);
                if (d.Size < SmallDir) d.Cat = null;
            }
            All = order.ToArray();
            lock (big) { TrimBig(); Biggest = big.ToList(); }
        }

        public Dir Find(string path)
        {
            if (Tree == null || path == null || !path.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) return null;
            Dir d = Tree;
            foreach (string part in path.Substring(Root.Length).Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (d.Subs == null) return null;
                d = d.Subs.FirstOrDefault(s => string.Equals(s.Name, part, StringComparison.OrdinalIgnoreCase));
                if (d == null) return null;
            }
            return d;
        }
    }

    // One tile of the map, or one row of the list.
    public class Item
    {
        public string Kind = "d";   // d folder, f file, o "the files right here" of a folder, m "N more" too small to draw
        public string Name;
        public long Size;
        public int Files, Cat = Cats.Other, Count;
        public Dir Dir;
        public List<Item> Children;

        public static Item Of(Dir d) { return new Item { Name = d.Name, Size = d.Size, Files = d.Files, Cat = d.Main, Dir = d }; }

        public Dictionary<string, object> ToJson()
        {
            var j = new Dictionary<string, object> { { "k", Kind }, { "n", Name }, { "s", Size }, { "f", Files }, { "c", Cat } };
            if (Dir != null) { j["id"] = Dir.Id; if (Dir.Denied) j["dn"] = true; }
            if (Kind == "m") j["x"] = Count;
            if (Children != null) j["ch"] = Children.Select(c => c.ToJson()).ToList();
            return j;
        }
    }

    // What one folder looks like in the window: its folders and files, biggest first, two levels deep.
    public static class Views
    {
        public const int MaxItems = 150, MaxInner = 40;

        // Keeps the biggest; the crumbs too small to draw become one "N more" block
        public static List<Item> Trim(List<Item> items, int max, double minShare)
        {
            items.Sort((a, b) => b.Size.CompareTo(a.Size));
            long total = items.Sum(i => i.Size);
            var keep = new List<Item>();
            var more = new Item { Kind = "m" };
            foreach (var i in items)
            {
                if (i.Size > 0 && keep.Count < max && i.Size >= total * minShare) keep.Add(i);
                else { more.Size += i.Size; more.Files += i.Files; more.Count++; }
            }
            if (more.Count > 0) keep.Add(more);
            return keep;
        }

        public static Dictionary<string, object> Build(Scan s, Dir d)
        {
            var items = new List<Item>();
            if (d.Subs != null) items.AddRange(d.Subs.Select(Item.Of));
            Native.List(s.PathOf(d), f =>
            {
                if ((f.Attributes & Native.Directory) == 0)
                    items.Add(new Item { Kind = "f", Name = f.Name, Size = Native.Size(f), Files = 1, Cat = Cats.Of(f.Name) });
            });
            long total = items.Sum(i => i.Size);
            var top = Trim(items, MaxItems, 0.0005);
            foreach (var i in top)
                if (i.Dir != null && i.Size >= total * 0.01)
                {
                    var inner = i.Dir.Subs == null ? new List<Item>() : i.Dir.Subs.Select(Item.Of).ToList();
                    if (i.Dir.Own > 0) inner.Add(new Item { Kind = "o", Name = i.Dir.Name, Size = i.Dir.Own, Files = i.Dir.OwnFiles, Cat = i.Dir.OwnMain });
                    if (inner.Count > 1 || (inner.Count == 1 && inner[0].Kind == "d")) i.Children = Trim(inner, MaxInner, 0.002);   // only its own files: nothing new to show inside
                }
            var crumbs = new List<object>();
            for (Dir c = d; c != null; c = c.Parent) crumbs.Insert(0, new Dictionary<string, object> { { "id", c.Id }, { "n", c.Name } });
            return new Dictionary<string, object> {
                { "t", "view" }, { "id", d.Id }, { "path", s.PathOf(d) }, { "crumbs", crumbs },
                { "size", total }, { "files", d.Files }, { "denied", d.Denied },
                { "cats", d.Cat }, { "main", (int)d.Main },
                { "items", top.Select(i => i.ToJson()).ToList() } };
        }
    }

    // Folders that fill up on their own and can go. Found by place (or by name, node_modules); the window writes the words.
    public static class Hogs
    {
        public const long Min = 100L << 20;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out string path);

        static string Downloads()
        {
            string p;
            try { return SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out p) == 0 ? p : null; }
            catch { return null; }
        }

        public static List<KeyValuePair<string, string>> Known(string root)
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var k = new List<KeyValuePair<string, string>>();
            Action<string, string> add = (key, path) => { if (!string.IsNullOrEmpty(path)) k.Add(new KeyValuePair<string, string>(key, path)); };
            add("temp", Path.Combine(local, "Temp"));
            add("temp", Path.Combine(win, "Temp"));
            add("downloads", Downloads());
            add("recycle", root + "$Recycle.Bin");
            add("winold", root + "Windows.old");
            add("dumps", Path.Combine(local, "CrashDumps"));
            add("npm", Path.Combine(local, "npm-cache"));
            add("yarn", Path.Combine(local, "Yarn\\Cache"));
            add("pip", Path.Combine(local, "pip\\cache"));
            add("nuget", Path.Combine(home, ".nuget\\packages"));
            add("gradle", Path.Combine(home, ".gradle\\caches"));
            return k;
        }

        public static List<Dictionary<string, object>> Find(Scan s, List<KeyValuePair<string, string>> known, long min)
        {
            var res = new List<Dictionary<string, object>>();
            Action<string, Dir, long, int, string> add = (key, d, size, count, file) =>
            {
                if (size < min || size == 0) return;
                var h = new Dictionary<string, object> { { "key", key }, { "id", d.Id }, { "s", size }, { "p", s.PathOf(d) + (file ?? "") } };
                if (count > 0) h["x"] = count;
                if (file != null) h["file"] = file;
                res.Add(h);
            };
            foreach (var kv in known)
            {
                Dir d = s.Find(kv.Value);
                if (d != null) add(kv.Key, d, d.Size, 0, null);
            }
            // every node_modules, the outer ones only: the inner ones are already inside
            var nm = new List<Dir>();
            var stack = new Stack<Dir>();
            stack.Push(s.Tree);
            while (stack.Count > 0)
            {
                Dir d = stack.Pop();
                if (d.Subs == null) continue;
                foreach (Dir c in d.Subs)
                    if (string.Equals(c.Name, "node_modules", StringComparison.OrdinalIgnoreCase)) nm.Add(c);
                    else stack.Push(c);
            }
            if (nm.Count > 0) add("node", nm.OrderByDescending(d => d.Size).First(), nm.Sum(d => d.Size), nm.Count, null);
            // the hibernation file sits in the root of the system drive, as big as a good part of the RAM
            long hiber = 0;
            Native.List(s.Root, f => { if (string.Equals(f.Name, "hiberfil.sys", StringComparison.OrdinalIgnoreCase)) hiber = Native.Size(f); });
            if (hiber > 0) add("hiberfil", s.Tree, hiber, 0, "hiberfil.sys");
            return res.OrderByDescending(h => (long)h["s"]).ToList();
        }
    }

    public static class SelfTest
    {
        static void Check(bool ok, string what) { if (!ok) throw new Exception("self-test failed: " + what); }

        static Native.FindData F(string name, uint attrs, long size, uint tag)
        {
            return new Native.FindData { Name = name, Attributes = attrs, SizeHigh = (uint)(size >> 32), SizeLow = (uint)size, Reserved0 = tag };
        }

        public static int Run()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "hoard-selftest-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                Check(Cats.Of("Movie.MKV") == Cats.Video && Cats.Of("a.tar.gz") == Cats.Archive && Cats.Of("README") == Cats.Other && Cats.Of("x.weird") == Cats.Other, "kinds by extension");
                Check(Cats.Of("pakchunk0-Windows.pak") == Cats.Program, "game data goes with programs");
                Check(Cats.Dominant(new long[] { 0, 5, 9, 0, 0, 0, 0, 0, 3 }) == Cats.Image && Cats.Dominant(new long[Cats.Count]) == Cats.Other, "main kind");
                Check(Cats.Dominant(new long[] { 2, 0, 0, 0, 0, 0, 0, 0, 9 }) == Cats.Other, "main kind can be 'other'");
                Check(Native.Size(F("a", 0x20, 5L << 32 | 7, 0)) == (5L << 32 | 7), "sizes over 4 GB");
                Check(Native.Size(F("a", 0x20 | Native.RecallOnDataAccess, 1 << 30, 0)) == 0, "OneDrive online-only file takes no space");
                Check(!Native.Follow(F("j", Native.Directory | Native.ReparsePoint, 0, Native.MountPoint)), "junction not followed");
                Check(!Native.Follow(F("l", Native.Directory | Native.ReparsePoint, 0, Native.Symlink)), "link not followed");
                Check(Native.Follow(F("OneDrive", Native.Directory | Native.ReparsePoint, 0, 0x9000601A)), "OneDrive folder followed");
                Check(Native.Follow(F("d", Native.Directory, 0, 0)) && !Native.Follow(F("f", 0x20, 0, 0)), "plain folder yes, file no");
                Check(Native.LongPath(@"C:\") == @"\\?\C:\" && Native.LongPath(@"\\nas\share\") == @"\\?\UNC\nas\share\", "long paths");

                var trimmed = Views.Trim(new List<Item> { new Item { Name = "a", Size = 100 }, new Item { Name = "b", Size = 900 }, new Item { Name = "c", Size = 0, Files = 2 } }, 1, 0);
                Check(trimmed.Count == 2 && trimmed[0].Name == "b" && trimmed[1].Kind == "m" && trimmed[1].Size == 100 && trimmed[1].Count == 2 && trimmed[1].Files == 2, "trim: biggest first, the rest in one block");

                // a real scan of a small tree: sizes, counts, kinds, the biggest file, a junction counted once
                Directory.CreateDirectory(Path.Combine(tmp, @"a\b"));
                Directory.CreateDirectory(Path.Combine(tmp, @"a\node_modules"));
                Directory.CreateDirectory(Path.Combine(tmp, "e"));
                File.WriteAllBytes(Path.Combine(tmp, @"a\big.mp4"), new byte[300000]);
                File.WriteAllBytes(Path.Combine(tmp, @"a\b\c.txt"), new byte[1000]);
                File.WriteAllBytes(Path.Combine(tmp, @"a\node_modules\m.js"), new byte[500]);
                File.WriteAllBytes(Path.Combine(tmp, "x.zip"), new byte[2000]);
                bool junction = false;
                try
                {
                    var psi = new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + Path.Combine(tmp, "j") + "\" \"" + Path.Combine(tmp, "a") + "\"");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    using (var p = Process.Start(psi)) p.WaitForExit();
                    junction = Directory.Exists(Path.Combine(tmp, "j"));
                }
                catch { }

                var s = new Scan(tmp);
                s.Run();
                Check(s.Done && s.Tree.Size == 303500, "branch sizes add up" + (junction ? ", the junction counted once" : ""));
                Check(s.Tree.Files == 4 && s.All.Length == 5, "files and folders counted");
                Check(s.Biggest.Count == 4 && s.Biggest[0].Name == "big.mp4" && s.PathOf(s.Biggest[0].Dir) == tmp + @"\a\", "biggest files, with their folder");
                Check(s.Tree.Main == Cats.Video && s.Find(tmp + @"\A\b").Size == 1000 && s.Find(tmp + @"\nope") == null, "main kind and lookup by path");
                Check(s.Tree.Cat == null && s.Find(tmp + @"\a").OwnMain == Cats.Video, "small folders drop the breakdown, keep the main kind");

                var v = Views.Build(s, s.Tree);
                var items = (List<Dictionary<string, object>>)v["items"];
                Check((long)v["size"] == 303500 && (string)items[0]["n"] == "a" && (string)items[1]["n"] == "x.zip", "view: folders and files, biggest first");
                var inner = (List<Dictionary<string, object>>)items[0]["ch"];
                Check(inner.Any(i => (string)i["k"] == "o" && (long)i["s"] == 300000), "view: a folder's own files as one block");
                Check((string)items[items.Count - 1]["k"] == "m", "view: the empty folder goes in the 'more' block");

                var hogs = Hogs.Find(s, new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("temp", tmp + @"\a\b") }, 0);
                Check(hogs.Count == 2 && (string)hogs[0]["key"] == "temp" && (long)hogs[0]["s"] == 1000, "known folder found by path");
                Check((string)hogs[1]["key"] == "node" && (int)hogs[1]["x"] == 1 && (long)hogs[1]["s"] == 500, "node_modules found by name");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(Path.Combine(tmp, "j"))) Directory.Delete(Path.Combine(tmp, "j"));   // the link only, not what it points at
                    if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                }
                catch { }
            }
        }
    }

    // The tray shows the app icon while the disks have room; a number (the free %) on amber or red when one is filling up.
    public static class TrayIcons
    {
        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr handle);

        public static Icon Number(int n, int level)
        {
            int w = SystemInformation.SmallIconSize.Width;
            Color bg = level == 2 ? Color.FromArgb(0xdc, 0x26, 0x26) : Color.FromArgb(0xd9, 0x77, 0x06);
            using (var bmp = new Bitmap(w, w))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                int r = w / 4;
                using (var path = new GraphicsPath())
                using (var b = new SolidBrush(bg))
                {
                    path.AddArc(0, 0, r * 2, r * 2, 180, 90);
                    path.AddArc(w - 1 - r * 2, 0, r * 2, r * 2, 270, 90);
                    path.AddArc(w - 1 - r * 2, w - 1 - r * 2, r * 2, r * 2, 0, 90);
                    path.AddArc(0, w - 1 - r * 2, r * 2, r * 2, 90, 90);
                    path.CloseFigure();
                    g.FillPath(b, path);
                }
                using (var f = new Font("Segoe UI", w * 0.7f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(n.ToString(), f, Brushes.White, new RectangleF(-1, 0, w + 2, w + 1), sf);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        public static void Free(Icon icon)
        {
            if (icon == null) return;
            DestroyIcon(icon.Handle);
            icon.Dispose();
        }
    }

    public class HoardContext : ApplicationContext
    {
        public static readonly bool Admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        const string RegistryPath = "SOFTWARE\\HoardApp";
        const string RunPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        const int ForgetMinutes = 10;

        // Portable version: a Hoard.ini next to the exe. Settings live there, the registry is never touched.
        static readonly string IniPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Hoard.ini");
        public static readonly bool Portable = File.Exists(IniPath);
        const string IniHeader = "; Hoard settings. While this file sits next to Hoard.exe, nothing goes to the registry.";

        readonly NotifyIcon tray;
        readonly MainForm form;
        readonly Icon appIcon;
        readonly System.Windows.Forms.Timer watch = new System.Windows.Forms.Timer { Interval = 60000 };
        readonly System.Windows.Forms.Timer progress = new System.Windows.Forms.Timer { Interval = 150 };
        readonly System.Windows.Forms.Timer forget = new System.Windows.Forms.Timer { Interval = ForgetMinutes * 60000 };
        readonly HashSet<string> warned = new HashSet<string>();
        Scan scan;
        Thread scanThread;
        Icon numberIcon;
        string iconKey = "", lastWarned;
        bool startup, alertsOn = true;
        int alertAt = 10;

        public HoardContext(bool fromStartup, string scanRoot)
        {
            LoadSettings();
            appIcon = L.AppIcon(SystemInformation.SmallIconSize);

            tray = new NotifyIcon();
            tray.Icon = appIcon;
            tray.Text = "Hoard";
            tray.Visible = true;
            tray.MouseDoubleClick += (s, e) => ShowForm();
            tray.BalloonTipClicked += (s, e) => { ShowForm(); if (scan == null && lastWarned != null) StartScan(lastWarned); };

            var menu = new ContextMenuStrip();
            var header = new ToolStripMenuItem("Hoard");
            header.Enabled = false;
            header.Font = new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add(header);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("Apri Hoard", "Open Hoard"), null, (s, e) => ShowForm());
            if (!Admin) menu.Items.Add(L.T("Riavvia come amministratore", "Restart as administrator"), null, (s, e) => Restart(null));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("Esci", "Exit"), null, (s, e) => Exit());
            tray.ContextMenuStrip = menu;

            startup = !Portable && RunKeySet();
            if (startup) SetRunKey(true);   // the exe may have moved: the entry follows it

            form = new MainForm(DataFolder());
            form.Message += OnMessage;
            form.VisibleChanged += (s, e) => { if (form.Visible) forget.Stop(); else { forget.Stop(); forget.Start(); } };
            var handle = form.Handle;

            // the window closed for a while: the scan goes, the memory comes back. Reopening starts from the drives.
            forget.Tick += (s, e) => { forget.Stop(); if (!form.Visible) DropScan(); };
            progress.Tick += (s, e) => Progress();
            watch.Tick += (s, e) => CheckDrives();
            watch.Start();
            CheckDrives();

            if (!fromStartup) ShowForm();   // at login it stays quiet in the tray
            if (scanRoot != null) StartScan(scanRoot);
        }

        // WebView2 keeps its cache here. Portable: in TEMP, so the stick stays clean.
        // Elevated and plain Hoard get separate folders: WebView2 refuses one made with other rights.
        static string DataFolder()
        {
            string root = Portable ? Path.GetTempPath() : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "Hoard", Admin ? "web-admin" : "web");
        }

        static List<DriveInfo> Drives(bool fixedOnly)
        {
            var res = new List<DriveInfo>();
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    bool kind = d.DriveType == DriveType.Fixed || (!fixedOnly && (d.DriveType == DriveType.Removable || d.DriveType == DriveType.Network));
                    try { if (kind && d.IsReady && d.TotalSize > 0) res.Add(d); } catch { }
                }
            }
            catch { }
            return res;
        }

        static object DriveJson(DriveInfo d)
        {
            string label = "", fs = "";
            try { label = d.VolumeLabel; fs = d.DriveFormat; } catch { }
            return new Dictionary<string, object> {
                { "root", d.Name }, { "label", label }, { "fs", fs },
                { "type", d.DriveType == DriveType.Network ? "network" : d.DriveType == DriveType.Removable ? "removable" : "fixed" },
                { "system", string.Equals(d.Name, Path.GetPathRoot(Environment.SystemDirectory), StringComparison.OrdinalIgnoreCase) },
                { "total", d.TotalSize }, { "free", d.AvailableFreeSpace }, { "used", d.TotalSize - d.TotalFreeSpace } };
        }

        object DrivesMsg()
        {
            return new Dictionary<string, object> { { "t", "drives" }, { "drives", Drives(false).Select(DriveJson).ToList() } };
        }

        // ── Tray: the fullest fixed drive, and one warning when a drive goes under the limit ──

        void CheckDrives()
        {
            DriveInfo worst = null;
            double worstPct = 101;
            var tips = new List<string>();
            foreach (var d in Drives(true))
            {
                double pct;
                try { pct = 100.0 * d.AvailableFreeSpace / d.TotalSize; } catch { continue; }
                tips.Add(string.Format("{0} {1:0}%", d.Name.TrimEnd('\\'), Math.Floor(pct)));
                if (pct < worstPct) { worstPct = pct; worst = d; }
                if (pct >= alertAt + 2) warned.Remove(d.Name);   // re-armed only with some room back, so a drive on the line doesn't nag
                else if (alertsOn && pct < alertAt && warned.Add(d.Name))
                {
                    lastWarned = d.Name;
                    tray.ShowBalloonTip(15000, "Hoard",
                        string.Format(L.T("{0} ha solo {1} liberi ({2:0}%). Clicca qui per vedere cosa lo riempie.",
                                          "{0} has only {1} free ({2:0}%). Click here to see what fills it."),
                                      d.Name.TrimEnd('\\'), Gb(d.AvailableFreeSpace), Math.Floor(pct)), ToolTipIcon.Warning);
                }
            }
            int level = worst == null ? 0 : worstPct < alertAt ? 2 : worstPct < alertAt + 5 ? 1 : 0;
            int n = (int)Math.Floor(Math.Max(0, worstPct));
            string key = level == 0 ? "app" : n + ":" + level;
            if (key != iconKey)
            {
                Icon old = numberIcon;
                numberIcon = level == 0 ? null : TrayIcons.Number(n, level);
                tray.Icon = numberIcon ?? appIcon;
                TrayIcons.Free(old);
                iconKey = key;
            }
            string tip = tips.Count == 0 ? "Hoard" : "Hoard · " + L.T("libero: ", "free: ") + string.Join(" · ", tips);
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
        }

        static string Gb(long b) { double g = b / 1073741824.0; return g.ToString(g < 10 ? "0.0" : "0", CultureInfo.CurrentCulture) + " GB"; }   // 1024³, like Explorer

        // ── Scanning ─────────────────────────────────────────────────────────────

        void StartScan(string root)
        {
            root = (root ?? "").TrimEnd('\\') + "\\";
            if (!Drives(false).Any(d => string.Equals(d.Name, root, StringComparison.OrdinalIgnoreCase))) return;   // only drives Windows lists
            if (scan != null) scan.Cancel = true;
            scan = new Scan(root);
            scanThread = new Thread(scan.Run);
            scanThread.IsBackground = true;
            scanThread.Name = "Hoard scan";
            scanThread.Start();
            progress.Start();
            Progress();
        }

        void DropScan()
        {
            if (scan != null) scan.Cancel = true;
            scan = null;
            scanThread = null;
            progress.Stop();
        }

        void Progress()
        {
            if (scan == null) { progress.Stop(); return; }
            if (scan.Done) { progress.Stop(); form.Post(Summary()); CheckDrives(); return; }
            if (scanThread != null && !scanThread.IsAlive) { progress.Stop(); return; }
            var tops = scan.Tops;
            var bytes = scan.TopBytes;
            var bars = new List<object>();
            for (int i = 0; i < bytes.Length; i++)
            {
                long b = Interlocked.Read(ref bytes[i]);
                if (b > 0) bars.Add(new Dictionary<string, object> { { "n", i < tops.Length ? tops[i].Name : null }, { "s", b } });
            }
            form.Post(new Dictionary<string, object> {
                { "t", "progress" }, { "root", scan.Root }, { "files", Interlocked.Read(ref scan.Files) }, { "dirs", Interlocked.Read(ref scan.Dirs) },
                { "bytes", Interlocked.Read(ref scan.Bytes) }, { "denied", Interlocked.Read(ref scan.Denied) }, { "current", scan.Current },
                { "used", Used(scan.Root) }, { "tops", bars } });
        }

        static long Used(string root)
        {
            try { var d = new DriveInfo(root); return d.TotalSize - d.TotalFreeSpace; } catch { return 0; }
        }

        object Summary()
        {
            long total = 0, free = 0;
            string label = "";
            try { var d = new DriveInfo(scan.Root); total = d.TotalSize; free = d.TotalFreeSpace; label = d.VolumeLabel; } catch { }
            return new Dictionary<string, object> {
                { "t", "done" }, { "root", scan.Root }, { "label", label }, { "size", scan.Tree.Size }, { "files", scan.Tree.Files }, { "dirs", scan.All.Length },
                { "denied", scan.Denied }, { "seconds", Math.Round(scan.Seconds, 1) }, { "total", total }, { "used", total - free },
                { "biggest", scan.Biggest.Take(50).Select(b => new Dictionary<string, object> {
                    { "id", b.Dir.Id }, { "n", b.Name }, { "s", b.Size }, { "c", b.Cat }, { "p", scan.PathOf(b.Dir) } }).ToList() },
                { "hogs", Hogs.Find(scan, Hogs.Known(scan.Root), Hogs.Min) } };
        }

        Dir DirAt(Dictionary<string, object> m)
        {
            object v;
            if (scan == null || !scan.Done || !m.TryGetValue("id", out v)) return null;
            int id;
            if (!int.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out id) || id < 0 || id >= scan.All.Length) return null;
            return scan.All[id];
        }

        void SendInit()
        {
            form.Post(new Dictionary<string, object> {
                { "t", "init" }, { "lang", L.It ? "it" : "en" },
                { "version", Assembly.GetExecutingAssembly().GetName().Version.ToString(3) },
                { "admin", Admin }, { "portable", Portable }, { "alertMin", 5 }, { "alertMax", 30 } });
            form.Post(Settings());
            form.Post(DrivesMsg());
            if (scan != null) { if (scan.Done) form.Post(Summary()); else Progress(); }
        }

        object Settings()
        {
            return new Dictionary<string, object> { { "t", "settings" }, { "startup", startup }, { "alertsOn", alertsOn }, { "alertAt", alertAt } };
        }

        // Everything the window asks for. The page is ours, but what it sends is still checked here.
        void OnMessage(Dictionary<string, object> m)
        {
            object v;
            Dir d;
            switch (Convert.ToString(m.TryGetValue("t", out v) ? v : ""))
            {
                case "ready": SendInit(); break;
                case "drives": form.Post(DrivesMsg()); break;
                case "scan": StartScan(m.TryGetValue("root", out v) ? Convert.ToString(v) : null); break;
                case "home": DropScan(); form.Post(DrivesMsg()); break;
                case "view":
                    d = DirAt(m);
                    if (d != null) form.Post(Views.Build(scan, d));
                    break;
                case "reveal":
                    d = DirAt(m);
                    if (d != null) Reveal(scan.PathOf(d), m.TryGetValue("file", out v) ? Convert.ToString(v) : null);
                    break;
                case "admin": Restart(scan != null ? scan.Root : null); break;
                case "open":
                    string url = m.TryGetValue("url", out v) ? Convert.ToString(v) : "";
                    if (url == "https://github.com/davide-mariotti/Hoard" || url == "https://ko-fi.com/Z0X32ATNHL" || url == "https://davide-mariotti.github.io/apps/hoard")
                        OpenLink(url);
                    break;
                case "set":
                    if (m.TryGetValue("startup", out v) && !Portable)
                    {
                        try { SetRunKey(Convert.ToBoolean(v)); } catch { }
                        startup = RunKeySet();
                    }
                    if (m.TryGetValue("alertsOn", out v)) alertsOn = Convert.ToBoolean(v);
                    if (m.TryGetValue("alertAt", out v)) alertAt = Math.Max(5, Math.Min(30, Convert.ToInt32(v)));
                    warned.Clear();
                    iconKey = "";
                    SaveSettings();
                    CheckDrives();
                    form.Post(Settings());
                    break;
            }
        }

        // Explorer opens with the file (or folder) selected. Deleting is done there, with the Recycle Bin as a safety net.
        static void Reveal(string folder, string file)
        {
            string target = folder;
            if (!string.IsNullOrEmpty(file) && file.IndexOfAny(new[] { '\\', '/', ':' }) < 0 && file != "..") target = folder + file;
            else if (folder.Length > 3) target = folder.TrimEnd('\\');
            try
            {
                if (target.Length <= 3) Process.Start("explorer.exe", "\"" + target + "\"");   // a drive: open it
                else Process.Start("explorer.exe", "/select,\"" + target + "\"");
            }
            catch { }
        }

        // Through Explorer: a browser started by an elevated Hoard would run as administrator too
        static void OpenLink(string url)
        {
            try { Process.Start("explorer.exe", "\"" + url + "\""); } catch { }
        }

        void ShowForm()
        {
            if (form.Visible) { if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal; form.Activate(); }
            else form.Show();
        }

        // As administrator, through UAC; the new Hoard picks up the same drive
        void Restart(string root)
        {
            try
            {
                var psi = new ProcessStartInfo(Application.ExecutablePath, "--restart" + (root != null ? " --scan " + root.TrimEnd('\\') : ""));
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                Exit();
            }
            catch (Win32Exception) { }   // UAC said no: stay as we are
        }

        void Exit()
        {
            DropScan();
            tray.Visible = false;
            TrayIcons.Free(numberIcon);
            form.Dispose();
            Application.Exit();
        }

        // ── Start with Windows: the Run key, like Sentinel ──────────────────────

        bool RunKeySet()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(RunPath, false)) return k != null && k.GetValue("Hoard") != null; }
            catch { return false; }
        }

        void SetRunKey(bool on)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunPath, true))
            {
                if (on) k.SetValue("Hoard", "\"" + Application.ExecutablePath + "\" --startup");   // quoted: folders with spaces
                else k.DeleteValue("Hoard", false);
            }
        }

        // ── Settings: registry, or Hoard.ini in the portable version ─────────────

        void LoadSettings()
        {
            try
            {
                var v = ReadValues();
                string s;
                if (v.TryGetValue("AlertsEnabled", out s)) alertsOn = Convert.ToBoolean(s);
                if (v.TryGetValue("AlertAt", out s)) alertAt = Math.Max(5, Math.Min(30, Convert.ToInt32(s)));
            }
            catch { }   // broken values: defaults win
        }

        Dictionary<string, string> ReadValues()
        {
            var d = new Dictionary<string, string>();
            if (Portable)
            {
                foreach (string line in File.ReadAllLines(IniPath))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0 && !line.StartsWith(";")) d[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
                return d;
            }
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                if (key != null)
                    foreach (string name in key.GetValueNames())
                        d[name] = Convert.ToString(key.GetValue(name), CultureInfo.InvariantCulture);
            return d;
        }

        void SaveSettings()
        {
            var values = new Dictionary<string, object> { { "AlertsEnabled", alertsOn }, { "AlertAt", alertAt } };
            try
            {
                if (Portable)
                {
                    var lines = new List<string> { IniHeader };
                    foreach (var kv in values) lines.Add(kv.Key + "=" + Convert.ToString(kv.Value, CultureInfo.InvariantCulture));
                    File.WriteAllLines(IniPath, lines.ToArray());
                    return;
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                    foreach (var kv in values) key.SetValue(kv.Key, kv.Value);
            }
            catch { }   // read-only stick or locked key: the change doesn't stick, Hoard keeps working
        }
    }
}
