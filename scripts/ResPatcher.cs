using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace ResPatcher
{
    public static class Patcher
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr BeginUpdateResource(string p, bool d);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UpdateResource(IntPtr h, IntPtr type, IntPtr name, ushort lang, byte[] data, uint size);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool EndUpdateResource(IntPtr h, bool d);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr FindResource(IntPtr h, IntPtr name, IntPtr type);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr FindResourceEx(IntPtr h, IntPtr type, IntPtr name, ushort lang);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool EnumResourceNames(IntPtr h, IntPtr type, EnumResProc cb, IntPtr param);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint SizeofResource(IntPtr h, IntPtr res);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LoadResource(IntPtr h, IntPtr res);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LockResource(IntPtr res);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibraryEx(string p, IntPtr h, uint f);
        [DllImport("kernel32.dll")]
        static extern bool FreeLibrary(IntPtr h);

        static byte[] ReadRes(string exe, IntPtr type, IntPtr name)
        {
            IntPtr h = LoadLibraryEx(exe, IntPtr.Zero, 2);
            if (h == IntPtr.Zero) return null;
            try
            {
                IntPtr r = FindResource(h, name, type);
                if (r == IntPtr.Zero) return null;
                uint sz = SizeofResource(h, r);
                IntPtr p = LockResource(LoadResource(h, r));
                byte[] buf = new byte[sz];
                Marshal.Copy(p, buf, 0, (int)sz);
                return buf;
            }
            finally { FreeLibrary(h); }
        }

        static ushort ProbeLang(string exe, IntPtr type, IntPtr name)
        {
            IntPtr h = LoadLibraryEx(exe, IntPtr.Zero, 2);
            if (h == IntPtr.Zero) return 0;
            try
            {
                if (FindResourceEx(h, type, name, 0x409) != IntPtr.Zero) return 0x409;
                return 0;
            }
            finally { FreeLibrary(h); }
        }

        delegate bool EnumResProc(IntPtr h, IntPtr type, IntPtr name, IntPtr param);

        public static List<string> EnumNames(string exe, IntPtr type)
        {
            List<string> names = new List<string>();
            IntPtr h = LoadLibraryEx(exe, IntPtr.Zero, 2);
            if (h == IntPtr.Zero) return names;
            try
            {
                EnumResProc proc = delegate(IntPtr hm, IntPtr t, IntPtr n, IntPtr p)
                {
                    if (n == IntPtr.Zero) names.Add("#0");
                    else if (((long)n >> 16) == 0) names.Add("#" + ((long)n & 0xFFFF));
                    else names.Add(Marshal.PtrToStringUni(n));
                    return true;
                };
                EnumResourceNames(h, type, proc, IntPtr.Zero);
                return names;
            }
            finally { FreeLibrary(h); }
        }

        static IntPtr NamePtr(string r)
        {
            if (r != null && r.StartsWith("#")) return (IntPtr)int.Parse(r.Substring(1));
            return Marshal.StringToHGlobalUni(r);
        }

        class VBlock
        {
            public string Key;
            public byte[] Value;
            public int ValueType;
            public List<VBlock> Children = new List<VBlock>();
        }

        static byte[] BuildBlock(VBlock b, int baseOff)
        {
            byte[] keyBytes = Encoding.Unicode.GetBytes(b.Key + "\0");
            int headerLen = 6 + keyBytes.Length;
            int pad = (4 - ((baseOff + headerLen) % 4)) % 4;
            int valLen = b.Value == null ? 0 : (b.ValueType == 1 ? b.Value.Length / 2 : b.Value.Length);
            int childOff = baseOff + headerLen + pad + (b.Value == null ? 0 : b.Value.Length);
            List<byte[]> childBytes = new List<byte[]>();
            foreach (VBlock c in b.Children)
            {
                byte[] cb = BuildBlock(c, childOff);
                childOff += cb.Length;
                childBytes.Add(cb);
            }
            int total = headerLen + pad + (b.Value == null ? 0 : b.Value.Length);
            foreach (byte[] cb in childBytes) total += cb.Length;
            MemoryStream ms = new MemoryStream();
            BinaryWriter w = new BinaryWriter(ms);
            w.Write((ushort)total);
            w.Write((ushort)valLen);
            w.Write((ushort)b.ValueType);
            w.Write(keyBytes);
            for (int i = 0; i < pad; i++) w.Write((byte)0);
            if (b.Value != null) w.Write(b.Value);
            foreach (byte[] cb in childBytes) w.Write(cb);
            w.Flush();
            return ms.ToArray();
        }

        static int FindUtf16Key(byte[] data, string key)
        {
            byte[] kb = Encoding.Unicode.GetBytes(key);
            for (int i = 0; i <= data.Length - kb.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < kb.Length; j++) { if (data[i + j] != kb[j]) { ok = false; break; } }
                if (ok) return i;
            }
            return -1;
        }

        static Bitmap BmpToBitmap(byte[] d)
        {
            int width = BitConverter.ToInt32(d, 4);
            int h = BitConverter.ToInt32(d, 8) / 2;
            Bitmap bmp = new Bitmap(width, h, PixelFormat.Format32bppArgb);
            Rectangle rect = new Rectangle(0, 0, width, h);
            BitmapData bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int rowBytes = width * 4;
            for (int y = 0; y < h; y++)
                Marshal.Copy(d, 40 + (h - 1 - y) * rowBytes, new IntPtr(bd.Scan0.ToInt64() + y * bd.Stride), rowBytes);
            bmp.UnlockBits(bd);
            return bmp;
        }

        static byte[] ToBmpFrame(Bitmap b)
        {
            int w = b.Width, h = b.Height;
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Write((uint)40); bw.Write((int)w); bw.Write((int)(h * 2)); bw.Write((ushort)1); bw.Write((ushort)32);
            bw.Write((uint)0); bw.Write((uint)(w * h * 4)); bw.Write((int)0); bw.Write((int)0); bw.Write((uint)0); bw.Write((uint)0);
            Rectangle rect = new Rectangle(0, 0, w, h);
            BitmapData bd = b.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] row = new byte[w * 4];
            for (int y = h - 1; y >= 0; y--)
            {
                Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + y * bd.Stride), row, 0, w * 4);
                bw.Write(row);
            }
            b.UnlockBits(bd);
            int maskRow = ((w + 31) / 32) * 4;
            bw.Write(new byte[maskRow * h]);
            bw.Flush();
            return ms.ToArray();
        }

        static byte[] ToPngFrame(Bitmap b)
        {
            MemoryStream ms = new MemoryStream();
            b.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

        static Dictionary<int, Bitmap> LoadIcoFrames(string path)
        {
            byte[] d = File.ReadAllBytes(path);
            ushort count = BitConverter.ToUInt16(d, 4);
            Dictionary<int, Bitmap> res = new Dictionary<int, Bitmap>();
            for (int i = 0; i < count; i++)
            {
                int e = 6 + 16 * i;
                int w = d[e] == 0 ? 256 : d[e];
                uint size = BitConverter.ToUInt32(d, e + 8);
                uint off = BitConverter.ToUInt32(d, e + 12);
                byte[] data = new byte[size];
                Array.Copy(d, (int)off, data, 0, (int)size);
                Bitmap bmp;
                if (data.Length > 4 && data[0] == 0x89 && data[1] == 0x50)
                {
                    using (MemoryStream ms = new MemoryStream(data)) bmp = new Bitmap(ms);
                }
                else bmp = BmpToBitmap(data);
                res[w] = bmp;
            }
            return res;
        }

        public static string Patch(string exePath, string icoPath, string desc, string product, string company,
            string fileVer, string internalName, string copyright, string origName, string productVer)
        {
            string log = "";
            List<string> groups = EnumNames(exePath, (IntPtr)14);
            if (groups.Count == 0) throw new Exception("group icon resource not found");
            string gref = groups[0];
            byte[] grp = ReadRes(exePath, (IntPtr)14, NamePtr(gref));
            ushort count = BitConverter.ToUInt16(grp, 4);
            Dictionary<int, Bitmap> frames = LoadIcoFrames(icoPath);
            log += "group=" + gref + " entries=" + count + " frames=" + frames.Count + "; ";

            Dictionary<int, byte[]> iconUpdates = new Dictionary<int, byte[]>();
            byte[] newEntries = new byte[14 * count];
            for (int i = 0; i < count; i++)
            {
                int e = 6 + 14 * i;
                Array.Copy(grp, e, newEntries, i * 14, 14);
                int w = grp[e] == 0 ? 256 : grp[e];
                ushort id = BitConverter.ToUInt16(grp, e + 12);
                uint origSize = BitConverter.ToUInt32(grp, e + 8);
                if (!frames.ContainsKey(w)) { log += "s" + w + ":keep; "; continue; }
                byte[] old = ReadRes(exePath, (IntPtr)3, (IntPtr)id) ?? new byte[0];
                bool png = old.Length > 4 && old[0] == 0x89 && old[1] == 0x50;
                Bitmap bmp = frames[w];
                byte[] nd = png ? ToPngFrame(bmp) : ToBmpFrame(bmp);
                if (nd.Length > origSize) log += "s" + w + ":grow(" + origSize + "->" + nd.Length + "); ";
                else if (nd.Length < origSize)
                {
                    byte[] padded = new byte[origSize];
                    Array.Copy(nd, padded, nd.Length);
                    nd = padded;
                    log += "s" + w + ":pad; ";
                }
                iconUpdates[id] = nd;
                byte[] eb = BitConverter.GetBytes((uint)nd.Length);
                Array.Copy(eb, 0, newEntries, i * 14 + 8, 4);
            }

            ushort ilang = ProbeLang(exePath, (IntPtr)14, NamePtr(gref));

            List<string> vnames = EnumNames(exePath, (IntPtr)16);
            string vref = vnames.Count > 0 ? vnames[0] : "#1";
            byte[] ver = ReadRes(exePath, (IntPtr)16, NamePtr(vref));
            bool haveVer = ver != null && ver.Length >= 92;
            byte[] fixedInfo = new byte[52];
            byte[] translation = new byte[] { 0x09, 0x04, 0xB0, 0x04 };
            if (haveVer)
            {
                Array.Copy(ver, 40, fixedInfo, 0, 52);
                int ki = FindUtf16Key(ver, "VarFileInfo");
                if (ki >= 0)
                {
                    int after = ki + "VarFileInfo\0".Length * 2;
                    after = after + ((4 - (after % 4)) % 4);
                    Array.Copy(ver, after, translation, 0, 4);
                }
            }
            ushort vlang = haveVer ? ProbeLang(exePath, (IntPtr)16, NamePtr(vref)) : (ushort)0;

            Dictionary<string, string> strings = new Dictionary<string, string>();
            strings["FileDescription"] = desc;
            strings["ProductName"] = product;
            strings["CompanyName"] = company;
            strings["FileVersion"] = fileVer;
            strings["InternalName"] = internalName;
            strings["LegalCopyright"] = copyright;
            strings["OriginalFilename"] = origName;
            strings["ProductVersion"] = productVer;

            VBlock root = new VBlock(); root.Key = "VS_VERSION_INFO"; root.ValueType = 0; root.Value = fixedInfo;
            VBlock sfi = new VBlock(); sfi.Key = "StringFileInfo"; sfi.ValueType = 1;
            VBlock table = new VBlock(); table.Key = "040904b0"; table.ValueType = 1;
            foreach (KeyValuePair<string, string> kv in strings)
            {
                VBlock s = new VBlock(); s.Key = kv.Key; s.ValueType = 1;
                s.Value = Encoding.Unicode.GetBytes(kv.Value + "\0");
                table.Children.Add(s);
            }
            sfi.Children.Add(table);
            VBlock vfi = new VBlock(); vfi.Key = "VarFileInfo"; vfi.ValueType = 1;
            VBlock tr = new VBlock(); tr.Key = "Translation"; tr.ValueType = 0; tr.Value = translation;
            vfi.Children.Add(tr);
            root.Children.Add(sfi);
            root.Children.Add(vfi);
            byte[] verBytes = BuildBlock(root, 0);

            IntPtr hUp = BeginUpdateResource(exePath, false);
            if (hUp == IntPtr.Zero) throw new Exception("BeginUpdateResource failed err=" + Marshal.GetLastWin32Error());
            bool ok = true;
            foreach (KeyValuePair<int, byte[]> kv in iconUpdates)
                ok = ok & UpdateResource(hUp, (IntPtr)3, (IntPtr)kv.Key, ilang, kv.Value, (uint)kv.Value.Length);
            ok = ok & UpdateResource(hUp, (IntPtr)14, NamePtr(gref), ilang, newEntries, (uint)newEntries.Length);
            if (haveVer) ok = ok & UpdateResource(hUp, (IntPtr)16, NamePtr(vref), vlang, verBytes, (uint)verBytes.Length);
            if (!ok) { EndUpdateResource(hUp, true); throw new Exception("UpdateResource failed err=" + Marshal.GetLastWin32Error()); }
            if (!EndUpdateResource(hUp, false)) throw new Exception("EndUpdateResource failed err=" + Marshal.GetLastWin32Error());
            log += "icons=" + iconUpdates.Count + " version=" + haveVer + " lang=" + ilang + "/" + vlang;
            return log;
        }
    }
}
