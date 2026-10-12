using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace PSTools
{
    static partial class Actions
    {
        public static void RestartExplorerNow() { RestartExplorer(false); }

        static readonly string[] SkipDirs = { ".git", "node_modules", ".vs", "__pycache__", "bin", "obj", "$RECYCLE.BIN", "System Volume Information" };
        static readonly string[] SkipFiles = { "Thumbs.db", "desktop.ini" };
        static readonly string[] ImageExts = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" };

        public static bool IsSkippedDir(string name)
        {
            return SkipDirs.Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        static IEnumerable<Action> QuickModule()
        {
            yield return new Action
            {
                Id = "undo-last", Group = "Create", Label = "Undo last PS Tools action", Targets = Target.Background, Fast = true, DefaultFav = true,
                Hint = "Reverts the most recent change PS Tools made: moves, renames, new files and folders, pasted files, zips, extractions, converted images, timestamps, attributes.",
                Run = (paths, c) =>
                {
                    string last = Journal.LastTitle();
                    if (last == null) return new Result { Summary = "Nothing to undo." };
                    if (!Program.Ask("Undo:\n" + last + " ?")) return null;
                    return new Result { Summary = Journal.Undo() };
                }
            };

            yield return new Action
            {
                Id = "download-clip", Group = "Create", Label = "Download link from clipboard", Targets = Target.Dirs, DefaultFav = true,
                Hint = "Copy a web address (http or https), then run this in a folder. The file is saved here under the name the server gives it. Shows progress, can be cancelled, and Undo removes it.",
                Run = (paths, c) => DownloadClip(DirOf(paths[0]), c)
            };

            yield return new Action
            {
                Id = "search-here", Group = "Inspect", Label = "Search here...", Targets = Target.Dirs, DefaultFav = true,
                Hint = "Finds files and folders by name, and by text inside files, under this folder. Every word you type must match. Skips .git, node_modules, bin, obj and similar.",
                Window = p => new SearchForm(DirOf(p[0]))
            };

            yield return new Action
            {
                Id = "zip-smart", Group = "Archive", Label = "Zip (skips .git, node_modules, bin, obj)", Targets = Target.Dirs | Target.File, Multi = true, DefaultFav = true,
                Hint = "Creates <name>.zip next to the selection. A folder keeps its name inside the zip so it extracts into one folder. Leaves out .git, node_modules, .vs, __pycache__, bin, obj, Thumbs.db and desktop.ini, and tells you what it left out. Undo deletes the zip.",
                Run = ZipSmart
            };

            yield return new Action
            {
                Id = "unzip-here", Group = "Archive", Label = "Extract here (own folder)", Targets = Target.File, Multi = true, AppliesTo = Modules.ZipQuery,
                Hint = "Extracts a .zip into a new folder next to it, named after the zip. If the zip already holds one top folder, that folder is extracted directly instead of nesting. Entries that try to escape the folder are skipped. Undo removes what was extracted.",
                Run = Unzip
            };

            yield return ImageAction("img-png", "Convert to PNG", "Saves a PNG copy next to each selected picture. Originals are never changed.", "", ".png", 0);
            yield return ImageAction("img-jpg", "Convert to JPEG", "Saves a JPEG copy (quality 90, transparency becomes white) next to each selected picture. Originals are never changed.", "", ".jpg", 0);
            yield return ImageAction("img-resize-1600", "Shrink to 1600 px", "Saves a copy whose longest side is at most 1600 pixels, same format. Pictures already that small are skipped, nothing is enlarged.", "-1600", null, 1600);
            yield return ImageAction("img-resize-800", "Shrink to 800 px", "Saves a copy whose longest side is at most 800 pixels, same format. Pictures already that small are skipped, nothing is enlarged.", "-800", null, 800);
            yield return ImageAction("img-strip", "Strip metadata (EXIF, location)", "Saves a clean copy named <name>-clean with camera, location and other embedded data removed. The picture is rotated to its upright orientation first. JPEGs are re-encoded at quality 90.", "-clean", null, 0);
        }

        static Action ImageAction(string id, string label, string hint, string suffix, string toExt, int maxEdge)
        {
            return new Action
            {
                Id = id, Group = "Image", Label = label, Hint = hint, Targets = Target.File, Multi = true, AppliesTo = Modules.ImageQuery,
                Run = (paths, c) => ImageJob(paths, c, label, suffix, toExt, maxEdge)
            };
        }


        static void EnsureDirs(Journal j, string dir)
        {
            var missing = new Stack<string>();
            string d = dir;
            while (!string.IsNullOrEmpty(d) && !Directory.Exists(d)) { missing.Push(d); d = Path.GetDirectoryName(d); }
            while (missing.Count > 0) j.EnsureDir(missing.Pop());
        }

        static string ExtFromContentType(string contentType)
        {
            string t = (contentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
            switch (t)
            {
                case "application/pdf": return ".pdf";
                case "application/zip": return ".zip";
                case "application/json": return ".json";
                case "application/xml": case "text/xml": return ".xml";
                case "text/html": return ".html";
                case "text/plain": return ".txt";
                case "text/csv": return ".csv";
                case "image/png": return ".png";
                case "image/jpeg": return ".jpg";
                case "image/gif": return ".gif";
                case "image/webp": return ".webp";
                case "image/svg+xml": return ".svg";
                case "video/mp4": return ".mp4";
                case "audio/mpeg": return ".mp3";
            }
            return "";
        }

        static string NameFromResponse(HttpWebResponse r, Uri uri)
        {
            string cd = r.Headers["Content-Disposition"];
            if (!string.IsNullOrEmpty(cd))
            {
                var m = Regex.Match(cd, "filename\\*\\s*=\\s*[^']*'[^']*'([^;]+)", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    try { return Uri.UnescapeDataString(m.Groups[1].Value.Trim().Trim('"')); } catch { }
                }
                m = Regex.Match(cd, "filename\\s*=\\s*\"?([^\";]+)\"?", RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value.Trim();
            }
            string seg = uri.AbsolutePath;
            seg = seg.Substring(seg.LastIndexOf('/') + 1);
            try { seg = Uri.UnescapeDataString(seg); } catch { }
            return seg;
        }

        static Result DownloadClip(string dir, Ctx c)
        {
            var m = Regex.Match(c.ClipboardText ?? "", "https?://[^\\s<>\"']+", RegexOptions.IgnoreCase);
            Uri uri;
            if (!m.Success || !Uri.TryCreate(m.Value, UriKind.Absolute, out uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return new Result { Summary = "The clipboard does not contain an http or https address. Copy the link first, then run this again." };

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 768 | 192);
            string target = null, part = null;
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(uri);
                req.UserAgent = "PSTools";
                req.Timeout = 30000;
                req.ReadWriteTimeout = 30000;
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    string name = Fs.Sanitize(NameFromResponse(resp, uri));
                    if (name.Length == 0) name = "download-" + Stamp();
                    if (Path.GetExtension(name).Length == 0) name += ExtFromContentType(resp.ContentType);
                    target = Fs.FreeName(dir, name);
                    part = target + ".part";
                    long total = resp.ContentLength, got = 0;
                    var buf = new byte[81920];
                    var last = DateTime.UtcNow;
                    using (var input = resp.GetResponseStream())
                    using (var output = new FileStream(part, FileMode.CreateNew, FileAccess.Write))
                    {
                        int n;
                        while ((n = input.Read(buf, 0, buf.Length)) > 0)
                        {
                            c.Check();
                            output.Write(buf, 0, n);
                            got += n;
                            if ((DateTime.UtcNow - last).TotalMilliseconds > 200)
                            {
                                last = DateTime.UtcNow;
                                c.Report(Path.GetFileName(target) + "   " + Fs.Size(got) + (total > 0 ? " of " + Fs.Size(total) : ""));
                            }
                        }
                    }
                }
                File.Move(part, target);
                var j = new Journal("Download " + Path.GetFileName(target));
                j.Link(target);
                j.Save();
                return new Result { Summary = "Downloaded " + Path.GetFileName(target) + " (" + Fs.Size(new FileInfo(target).Length) + ")" };
            }
            catch (WebException ex)
            {
                if (part != null) try { File.Delete(part); } catch { }
                return new Result { Summary = "Download failed: " + ex.Message };
            }
            catch
            {
                if (part != null) try { File.Delete(part); } catch { }
                throw;
            }
        }

        static IEnumerable<string> WalkForZip(string root, HashSet<string> skipped)
        {
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                string[] files = null, subs = null;
                try { files = Directory.GetFiles(dir); } catch { }
                if (files != null)
                    foreach (var f in files)
                        if (!SkipFiles.Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)) yield return f;
                try { subs = Directory.GetDirectories(dir); } catch { }
                if (subs == null) continue;
                foreach (var s in subs)
                {
                    string n = Path.GetFileName(s);
                    if (Fs.IsLink(s)) continue;
                    if (IsSkippedDir(n)) { skipped.Add(n); continue; }
                    stack.Push(s);
                }
            }
        }

        static Result ZipSmart(string[] paths, Ctx c)
        {
            foreach (var p in paths)
                if (Fs.IsRoot(p)) return new Result { Summary = "Select a folder or files, not a whole drive." };

            string parent = ParentOf(paths[0]);
            string baseName;
            if (paths.Length == 1) baseName = Directory.Exists(paths[0]) ? NameOf(paths[0]) : NameNoExt(paths[0]);
            else baseName = Fs.IsRoot(parent) ? "archive" : NameOf(parent);
            baseName = Fs.Sanitize(baseName);
            if (baseName.Length == 0) baseName = "archive";
            string target = Fs.FreeName(parent, baseName + ".zip");

            var skipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int files = 0, locked = 0;
            try
            {
                using (var fs = new FileStream(target, FileMode.CreateNew, FileAccess.Write))
                using (var za = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    foreach (var p in paths)
                    {
                        c.Check();
                        if (Directory.Exists(p))
                        {
                            string root = Fs.Full(p);
                            string rootName = NameOf(p);
                            foreach (var f in WalkForZip(root, skipped))
                            {
                                c.Check();
                                if (string.Equals(f, target, StringComparison.OrdinalIgnoreCase)) continue;
                                string rel = f.Substring(root.TrimEnd('\\').Length).TrimStart('\\');
                                c.Report(rootName + "\\" + rel);
                                try { za.CreateEntryFromFile(f, rootName + "/" + rel.Replace('\\', '/'), CompressionLevel.Optimal); files++; }
                                catch (IOException) { locked++; }
                                catch (UnauthorizedAccessException) { locked++; }
                            }
                        }
                        else
                        {
                            c.Report(NameOf(p));
                            try { za.CreateEntryFromFile(p, NameOf(p), CompressionLevel.Optimal); files++; }
                            catch (IOException) { locked++; }
                            catch (UnauthorizedAccessException) { locked++; }
                        }
                    }
                }
                if (files == 0) { try { File.Delete(target); } catch { } return new Result { Summary = "Nothing to zip: no readable files." }; }
            }
            catch
            {
                try { File.Delete(target); } catch { }
                throw;
            }

            var j = new Journal("Zip into " + target);
            j.Link(target);
            j.Save();
            string s = "Created " + Path.GetFileName(target) + ": " + files.ToString("N0") + " files, " + Fs.Size(new FileInfo(target).Length);
            if (skipped.Count > 0) s += ". Left out: " + string.Join(", ", skipped.OrderBy(x => x).ToArray());
            if (locked > 0) s += ". " + locked + " locked files skipped";
            return new Result { Summary = s };
        }

        static string SingleRoot(ZipArchive za)
        {
            string root = null;
            foreach (var e in za.Entries)
            {
                int i = e.FullName.IndexOf('/');
                if (i <= 0) return null;
                string seg = e.FullName.Substring(0, i);
                if (root == null) root = seg;
                else if (!string.Equals(root, seg, StringComparison.Ordinal)) return null;
            }
            if (root == null || root == ".." || root == ".") return null;
            return root;
        }

        static Result Unzip(string[] paths, Ctx c)
        {
            var j = new Journal(paths.Length == 1 ? "Extract " + NameOf(paths[0]) : "Extract " + paths.Length + " archives");
            int files = 0, blocked = 0, failed = 0, archives = 0;
            var notes = new List<string>();
            string lastDest = null;
            try
            {
                foreach (var zip in paths)
                {
                    c.Check();
                    if (!string.Equals(Path.GetExtension(zip), ".zip", StringComparison.OrdinalIgnoreCase)) { notes.Add(NameOf(zip) + " is not a .zip file."); continue; }
                    ZipArchive za;
                    try { za = ZipFile.OpenRead(zip); }
                    catch (Exception) { notes.Add("Could not read " + NameOf(zip) + " (damaged or not a plain zip)."); continue; }
                    using (za)
                    {
                        string root = SingleRoot(za);
                        string folderName = Fs.Sanitize(root ?? NameNoExt(zip));
                        if (folderName.Length == 0 || folderName == "." || folderName == "..") folderName = "extracted";
                        string dest = Fs.FreeName(ParentOf(zip), folderName);
                        EnsureDirs(j, dest);
                        lastDest = dest;
                        archives++;
                        foreach (var e in za.Entries)
                        {
                            c.Check();
                            string rel = e.FullName.Replace('/', '\\');
                            if (root != null) rel = rel.Substring(root.Length).TrimStart('\\');
                            if (rel.Length == 0) continue;
                            string full;
                            try { full = Path.GetFullPath(Path.Combine(dest, rel)); }
                            catch (Exception) { blocked++; continue; }
                            if (!Fs.IsInside(full, dest) || string.Equals(Fs.Full(full), Fs.Full(dest), StringComparison.OrdinalIgnoreCase)) { blocked++; continue; }
                            c.Report(rel);
                            if (e.FullName.EndsWith("/")) { EnsureDirs(j, full); continue; }
                            EnsureDirs(j, Path.GetDirectoryName(full));
                            try { e.ExtractToFile(full, false); j.Link(full); files++; }
                            catch (Exception)
                            {
                                failed++;
                                try { if (File.Exists(full) && new FileInfo(full).Length == 0) File.Delete(full); } catch { }
                            }
                        }
                    }
                }
            }
            finally { j.Save(); }

            if (archives == 0 && notes.Count > 0) return new Result { Summary = string.Join("\n", notes.ToArray()) };
            string s = "Extracted " + files.ToString("N0") + " files " + (archives == 1 ? "into " + Path.GetFileName(lastDest) : "from " + archives + " archives");
            if (blocked > 0) s += ". Skipped " + blocked + " unsafe entries";
            if (failed > 0) s += ". " + failed + " could not be extracted (password or damaged)";
            if (notes.Count > 0) s += "\n\n" + string.Join("\n", notes.ToArray());
            return new Result { Summary = s };
        }

        static RotateFlipType Upright(Image img)
        {
            try
            {
                if (Array.IndexOf(img.PropertyIdList, 0x0112) < 0) return RotateFlipType.RotateNoneFlipNone;
                byte[] v = img.GetPropertyItem(0x0112).Value;
                switch (v != null && v.Length > 0 ? (int)v[0] : 1)
                {
                    case 2: return RotateFlipType.RotateNoneFlipX;
                    case 3: return RotateFlipType.Rotate180FlipNone;
                    case 4: return RotateFlipType.Rotate180FlipX;
                    case 5: return RotateFlipType.Rotate90FlipX;
                    case 6: return RotateFlipType.Rotate90FlipNone;
                    case 7: return RotateFlipType.Rotate270FlipX;
                    case 8: return RotateFlipType.Rotate270FlipNone;
                }
            }
            catch { }
            return RotateFlipType.RotateNoneFlipNone;
        }

        static bool MultiFrame(Image img)
        {
            try { if (img.GetFrameCount(FrameDimension.Time) > 1) return true; } catch { }
            try { if (img.GetFrameCount(FrameDimension.Page) > 1) return true; } catch { }
            return false;
        }

        static void SaveImage(Image img, string target, string ext)
        {
            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                    var codec = ImageCodecInfo.GetImageEncoders().First(e => e.FormatID == ImageFormat.Jpeg.Guid);
                    using (var ep = new EncoderParameters(1))
                    {
                        ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
                        img.Save(target, codec, ep);
                    }
                    break;
                case ".png": img.Save(target, ImageFormat.Png); break;
                case ".bmp": img.Save(target, ImageFormat.Bmp); break;
                case ".gif": img.Save(target, ImageFormat.Gif); break;
                default: img.Save(target, ImageFormat.Tiff); break;
            }
        }

        static Result ImageJob(string[] paths, Ctx c, string title, string suffix, string toExt, int maxEdge)
        {
            var j = new Journal(title);
            var notes = new List<string>();
            int made = 0;
            try
            {
                foreach (var p in paths)
                {
                    c.Check();
                    c.Report(NameOf(p));
                    string ext = Path.GetExtension(p).ToLowerInvariant();
                    if (!ImageExts.Contains(ext)) { notes.Add(NameOf(p) + ": not a picture"); continue; }
                    string outExt = toExt ?? ext;
                    bool jpegIn = ext == ".jpg" || ext == ".jpeg", jpegOut = outExt == ".jpg" || outExt == ".jpeg";
                    if (toExt != null && (ext == toExt || (jpegIn && jpegOut))) { notes.Add(NameOf(p) + ": already " + toExt.TrimStart('.').ToUpperInvariant()); continue; }
                    string target = null;
                    try
                    {
                        using (var src = Image.FromFile(p))
                        {
                            if (MultiFrame(src)) { notes.Add(NameOf(p) + ": animated or multi-page, skipped"); continue; }
                            var rf = Upright(src);
                            if (rf != RotateFlipType.RotateNoneFlipNone) src.RotateFlip(rf);
                            int w = src.Width, h = src.Height, nw = w, nh = h;
                            if (maxEdge > 0)
                            {
                                int longest = Math.Max(w, h);
                                if (longest <= maxEdge) { notes.Add(NameOf(p) + ": already within " + maxEdge + " px"); continue; }
                                double k = maxEdge / (double)longest;
                                nw = Math.Max(1, (int)Math.Round(w * k));
                                nh = Math.Max(1, (int)Math.Round(h * k));
                            }
                            target = Fs.FreeName(Path.GetDirectoryName(Fs.Full(p)), Path.GetFileNameWithoutExtension(p) + suffix + outExt);
                            using (var bmp = new Bitmap(nw, nh, jpegOut ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppArgb))
                            {
                                if (src.HorizontalResolution > 0 && src.VerticalResolution > 0) bmp.SetResolution(src.HorizontalResolution, src.VerticalResolution);
                                using (var g = Graphics.FromImage(bmp))
                                using (var ia = new ImageAttributes())
                                {
                                    if (jpegOut) g.Clear(Color.White);
                                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                    g.SmoothingMode = SmoothingMode.HighQuality;
                                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                                    g.CompositingQuality = CompositingQuality.HighQuality;
                                    ia.SetWrapMode(WrapMode.TileFlipXY);
                                    g.DrawImage(src, new Rectangle(0, 0, nw, nh), 0, 0, w, h, GraphicsUnit.Pixel, ia);
                                }
                                SaveImage(bmp, target, outExt);
                            }
                        }
                        j.Link(target);
                        made++;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        if (target != null) try { File.Delete(target); } catch { }
                        notes.Add(NameOf(p) + ": " + ex.Message);
                    }
                }
            }
            finally { j.Save(); }

            string s = made == 0 ? "No pictures were created." : "Created " + made + (made == 1 ? " picture" : " pictures") + " next to the originals";
            if (notes.Count > 0) s += "\n\n" + string.Join("\n", notes.ToArray());
            return new Result { Summary = s };
        }
    }
}
