using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

namespace PSTools
{
    class SpaceNode
    {
        public string Name = "", Path = "", Ext = "";
        public long Size;
        public int Files;
        public bool IsDir, IsGroup;
        public SpaceNode Parent;
        public List<SpaceNode> Kids = new List<SpaceNode>();
    }

    class SpaceScan
    {
        const int KeepFiles = 80;

        public volatile bool Cancel;
        public bool Cancelled;
        long files, bytes;

        public long FilesSeen { get { return Interlocked.Read(ref files); } }
        public long BytesSeen { get { return Interlocked.Read(ref bytes); } }

        public SpaceNode Run(string path)
        {
            var root = ScanDir(path, null);
            root.Name = path;
            Cancelled = Cancel;
            return root;
        }

        static string NameOf(string path)
        {
            string trimmed = path.TrimEnd('\\', '/');
            string n = System.IO.Path.GetFileName(trimmed);
            return n.Length == 0 ? path : n;
        }

        SpaceNode ScanDir(string path, SpaceNode parent)
        {
            var node = new SpaceNode { Name = NameOf(path), Path = path, IsDir = true, Parent = parent };
            var found = new List<SpaceNode>();
            try
            {
                foreach (FileSystemInfo e in new DirectoryInfo(path).EnumerateFileSystemInfos())
                {
                    if (Cancel) break;
                    try
                    {
                        if ((e.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                        var fi = e as FileInfo;
                        if (fi == null) node.Kids.Add(ScanDir(e.FullName, node));
                        else
                        {
                            long len = fi.Length;
                            found.Add(new SpaceNode { Name = fi.Name, Path = fi.FullName, Ext = fi.Extension.ToLowerInvariant(), Size = len, Files = 1, Parent = node });
                            Interlocked.Increment(ref files);
                            Interlocked.Add(ref bytes, len);
                        }
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }

            found.Sort((a, b) => b.Size.CompareTo(a.Size));
            node.Kids.AddRange(found.Take(KeepFiles));
            if (found.Count > KeepFiles)
            {
                var rest = found.Skip(KeepFiles).ToList();
                node.Kids.Add(new SpaceNode
                {
                    Name = "(" + rest.Count.ToString("N0") + " smaller files)", IsGroup = true, Parent = node,
                    Size = rest.Sum(r => r.Size), Files = rest.Count, Path = path
                });
            }
            node.Kids.Sort((a, b) => b.Size.CompareTo(a.Size));
            node.Size = node.Kids.Sum(k => k.Size);
            node.Files = node.Kids.Sum(k => k.Files);
            return node;
        }
    }

    class Tile
    {
        public SpaceNode Node;
        public RectangleF Rect;
    }

    static class Treemap
    {
        public static List<Tile> Layout(SpaceNode parent, RectangleF area, int maxTiles)
        {
            var items = parent.Kids.Where(k => k.Size > 0).OrderByDescending(k => k.Size).ToList();
            if (items.Count > maxTiles)
            {
                var rest = items.Skip(maxTiles - 1).ToList();
                items = items.Take(maxTiles - 1).ToList();
                items.Add(new SpaceNode
                {
                    Name = "(" + rest.Count.ToString("N0") + " smaller items)", IsGroup = true, Parent = parent, Path = parent.Path,
                    Size = rest.Sum(r => r.Size), Files = rest.Sum(r => r.Files)
                });
            }
            return Squarify(items, area);
        }

        static double Worst(double side, double sum, double max, double min)
        {
            double s2 = sum * sum, side2 = side * side;
            return Math.Max(side2 * max / s2, s2 / (side2 * min));
        }

        static List<Tile> Squarify(List<SpaceNode> items, RectangleF r)
        {
            var tiles = new List<Tile>();
            double total = items.Sum(it => (double)it.Size);
            if (total <= 0 || r.Width < 1 || r.Height < 1) return tiles;
            double scale = (double)r.Width * r.Height / total;
            double x = r.X, y = r.Y, w = r.Width, h = r.Height;
            int i = 0;
            while (i < items.Count)
            {
                bool wide = w >= h;
                double side = Math.Min(w, h);
                var row = new List<SpaceNode>();
                double sum = 0, max = 0, worst = double.MaxValue;
                while (i < items.Count)
                {
                    double a = items[i].Size * scale;
                    double nsum = sum + a;
                    double nmax = row.Count == 0 ? a : max;
                    double nworst = Worst(side, nsum, nmax, a);
                    if (row.Count > 0 && nworst > worst) break;
                    row.Add(items[i]); sum = nsum; max = nmax; worst = nworst; i++;
                }
                double thickness = side > 0 ? sum / side : 0;
                double pos = 0;
                foreach (var n in row)
                {
                    double len = thickness > 0 ? n.Size * scale / thickness : 0;
                    RectangleF rc = wide
                        ? new RectangleF((float)x, (float)(y + pos), (float)thickness, (float)len)
                        : new RectangleF((float)(x + pos), (float)y, (float)len, (float)thickness);
                    tiles.Add(new Tile { Node = n, Rect = rc });
                    pos += len;
                }
                if (wide) { x += thickness; w -= thickness; }
                else { y += thickness; h -= thickness; }
            }
            return tiles;
        }
    }
}
