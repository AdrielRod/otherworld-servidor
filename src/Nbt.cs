// Leitura e escrita mínima de NBT (formato do servers.dat do Minecraft), só o suficiente para incluir um servidor na lista
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InstaladorOtherworld
{
    class Tag
    {
        public byte Type; public object Value; public byte ListType;
        public Tag(byte type, object value, byte listType = 0) { Type = type; Value = value; ListType = listType; }
    }

    static class Nbt
    {
        // Garante que o servidor esteja na lista; atualiza o endereço se já existir um com o mesmo nome. Devolve true se mudou algo.
        public static bool EnsureServer(string path, string name, string ip)
        {
            Dictionary<string, Tag> root = null;
            if (File.Exists(path))
            {
                try { using (var r = new BinaryReader(File.OpenRead(path))) { r.ReadByte(); ReadString(r); root = (Dictionary<string, Tag>)ReadPayload(r, 10); } }
                catch { File.Copy(path, path + ".corrompido", true); root = null; }
            }
            if (root == null) root = new Dictionary<string, Tag>();
            if (!root.ContainsKey("servers")) root["servers"] = new Tag(9, new List<object>(), 10);
            var list = (List<object>)root["servers"].Value;
            root["servers"].ListType = 10;

            foreach (Dictionary<string, Tag> s in list.Cast<Dictionary<string, Tag>>())
            {
                string sIp = s.ContainsKey("ip") ? (string)s["ip"].Value : "";
                string sName = s.ContainsKey("name") ? (string)s["name"].Value : "";
                if (sIp == ip) return false;
                if (sName == name) { s["ip"] = new Tag(8, ip); Write(path, root); return true; }
            }
            list.Insert(0, new Dictionary<string, Tag> { { "name", new Tag(8, name) }, { "ip", new Tag(8, ip) } });
            Write(path, root);
            return true;
        }

        static void Write(string path, Dictionary<string, Tag> root)
        {
            using (var w = new BinaryWriter(File.Create(path))) { w.Write((byte)10); WriteString(w, ""); WritePayload(w, 10, root, 0); }
        }

        static short RS(BinaryReader r) { var b = r.ReadBytes(2); return (short)(b[0] << 8 | b[1]); }
        static int RI(BinaryReader r) { var b = r.ReadBytes(4); return b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]; }
        static long RL(BinaryReader r) { long hi = (uint)RI(r), lo = (uint)RI(r); return hi << 32 | lo; }
        static string ReadString(BinaryReader r) { int len = (ushort)RS(r); return Encoding.UTF8.GetString(r.ReadBytes(len)); }

        static object ReadPayload(BinaryReader r, byte t)
        {
            switch (t)
            {
                case 1: return r.ReadByte();
                case 2: return RS(r);
                case 3: return RI(r);
                case 4: return RL(r);
                case 5: return BitConverter.ToSingle(r.ReadBytes(4).Reverse().ToArray(), 0);
                case 6: return BitConverter.ToDouble(r.ReadBytes(8).Reverse().ToArray(), 0);
                case 7: return r.ReadBytes(RI(r));
                case 8: return ReadString(r);
                case 9: { byte lt = r.ReadByte(); int n = RI(r); var l = new List<object>(); for (int i = 0; i < n; i++) l.Add(ReadPayload(r, lt)); return new ListBox(lt, l); }
                case 10:
                    {
                        var d = new Dictionary<string, Tag>();
                        while (true)
                        {
                            byte ct = r.ReadByte(); if (ct == 0) break;
                            string k = ReadString(r); object v = ReadPayload(r, ct);
                            var lb = v as ListBox;
                            d[k] = lb != null ? new Tag(9, lb.Items, lb.Type) : new Tag(ct, v);
                        }
                        return d;
                    }
                case 11: { int n = RI(r); var a = new int[n]; for (int i = 0; i < n; i++) a[i] = RI(r); return a; }
                case 12: { int n = RI(r); var a = new long[n]; for (int i = 0; i < n; i++) a[i] = RL(r); return a; }
            }
            throw new InvalidDataException("tag NBT desconhecida " + t);
        }

        class ListBox { public byte Type; public List<object> Items; public ListBox(byte t, List<object> i) { Type = t; Items = i; } }

        static void WS(BinaryWriter w, short v) { w.Write((byte)(v >> 8)); w.Write((byte)v); }
        static void WI(BinaryWriter w, int v) { w.Write((byte)(v >> 24)); w.Write((byte)(v >> 16)); w.Write((byte)(v >> 8)); w.Write((byte)v); }
        static void WL(BinaryWriter w, long v) { WI(w, (int)(v >> 32)); WI(w, (int)v); }
        static void WriteString(BinaryWriter w, string s) { var b = Encoding.UTF8.GetBytes(s); WS(w, (short)b.Length); w.Write(b); }

        static void WritePayload(BinaryWriter w, byte t, object v, byte listType)
        {
            switch (t)
            {
                case 1: w.Write((byte)v); break;
                case 2: WS(w, (short)v); break;
                case 3: WI(w, (int)v); break;
                case 4: WL(w, (long)v); break;
                case 5: w.Write(BitConverter.GetBytes((float)v).Reverse().ToArray()); break;
                case 6: w.Write(BitConverter.GetBytes((double)v).Reverse().ToArray()); break;
                case 7: { var a = (byte[])v; WI(w, a.Length); w.Write(a); break; }
                case 8: WriteString(w, (string)v); break;
                case 9:
                    {
                        var l = (List<object>)v;
                        w.Write(l.Count == 0 && listType == 0 ? (byte)0 : listType); WI(w, l.Count);
                        foreach (var item in l)
                        {
                            var inner = item as ListBox;
                            if (inner != null) WritePayload(w, 9, inner.Items, inner.Type); else WritePayload(w, listType, item, 0);
                        }
                        break;
                    }
                case 10:
                    foreach (var kv in (Dictionary<string, Tag>)v)
                    {
                        w.Write(kv.Value.Type); WriteString(w, kv.Key); WritePayload(w, kv.Value.Type, kv.Value.Value, kv.Value.ListType);
                    }
                    w.Write((byte)0);
                    break;
                case 11: { var a = (int[])v; WI(w, a.Length); foreach (var x in a) WI(w, x); break; }
                case 12: { var a = (long[])v; WI(w, a.Length); foreach (var x in a) WL(w, x); break; }
            }
        }
    }
}
