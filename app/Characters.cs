using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    [DataContract]
    internal sealed class CharacterVoice
    {
        [DataMember(Name = "mode")] public string Mode;
        [DataMember(Name = "style")] public string Style;
        [DataMember(Name = "pitch")] public int Pitch;
        [DataMember(Name = "speed")] public int Speed;
    }

    [DataContract]
    internal sealed class CharacterSprite
    {
        [DataMember(Name = "slot")] public string Slot;
        [DataMember(Name = "file")] public string File;
    }

    [DataContract]
    internal sealed class CharacterManifest
    {
        [DataMember(Name = "schemaVersion")] public int Version;
        [DataMember(Name = "id")] public string Id;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "renderer")] public string Renderer;
        [DataMember(Name = "personalityFile")] public string PersonalityFile;
        [DataMember(Name = "voice")] public CharacterVoice Voice;
        [DataMember(Name = "sprites")] public CharacterSprite[] Sprites;
    }

    internal sealed class CharacterProfile
    {
        internal readonly string Id, Name, Renderer, Folder, Personality, PersonalityPath;
        internal readonly CharacterVoice Voice;
        private readonly Dictionary<string, byte[]> sprites;

        internal CharacterProfile(CharacterManifest manifest, string folder, string personality, string personalityPath, Dictionary<string, byte[]> images)
        {
            Id = manifest.Id; Name = manifest.Name; Renderer = manifest.Renderer;
            Folder = folder; Personality = personality; PersonalityPath = personalityPath;
            Voice = manifest.Voice; sprites = images;
        }

        internal Stream OpenSprite(string resource)
        {
            byte[] image;
            return sprites.TryGetValue(resource, out image) ? new MemoryStream(image, false) : null;
        }
    }

    internal sealed class CharacterCatalog
    {
        internal const string Renderer = "capri-strip-v1";
        internal readonly string Root;
        internal readonly List<string> Errors = new List<string>();
        internal CharacterProfile Current { get; private set; }
        internal readonly CharacterProfile Builtin;
        private readonly Func<string, Stream> resource;

        internal CharacterCatalog(string root, Func<string, Stream> openResource)
        {
            Root = Path.GetFullPath(root);
            resource = openResource;
            using (Stream data = resource("CompanionLab.Character.capri.json"))
            {
                if (data == null) throw new InvalidDataException("The built-in Capri profile is missing. Rebuild CompanionLab.");
                Builtin = Parse(data, null);
            }
            if (Builtin.Id != "capri") throw new InvalidDataException("The built-in profile must be Capri.");
            Current = Builtin;
        }

        internal CharacterProfile Load(string folder)
        {
            try
            {
                folder = Path.GetFullPath(folder);
                if (!string.Equals(Path.GetDirectoryName(folder), Root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Profiles must be direct children of the character folder.");
                folder = SafeFile(Root, Path.GetFileName(folder));
                string path = SafeFile(folder, "character.json");
                if (new FileInfo(path).Length > 65536) throw new InvalidDataException("character.json exceeds 64 KiB.");
                using (Stream data = File.OpenRead(path)) return Parse(data, folder);
            }
            catch (Exception error)
            {
                if (!(error is IOException) && !(error is UnauthorizedAccessException) && !(error is SerializationException) && !(error is ArgumentException) && !(error is System.Xml.XmlException)) throw;
                throw new InvalidDataException("Cannot load character: " + error.Message, error);
            }
        }

        private CharacterProfile Parse(Stream data, string folder)
        {
            CharacterManifest manifest = (CharacterManifest)new DataContractJsonSerializer(typeof(CharacterManifest)).ReadObject(data);
            if (manifest == null || manifest.Version != 1) throw new InvalidDataException("Unsupported character schemaVersion; expected 1.");
            if (!Regex.IsMatch(manifest.Id ?? "", "^[a-z][a-z0-9_-]{0,47}$")) throw new InvalidDataException("Character id must contain 1-48 lowercase letters, digits, underscores or hyphens.");
            if (folder != null && manifest.Id == "capri") throw new InvalidDataException("capri is reserved for the built-in profile; use another id.");
            if (string.IsNullOrWhiteSpace(manifest.Name) || manifest.Name.Length > 48 || Regex.IsMatch(manifest.Name, @"[\p{Cc}\p{Cf}]")) throw new InvalidDataException("Character name must contain 1-48 visible characters.");
            if (manifest.Renderer != Renderer) throw new InvalidDataException("Unsupported character renderer; expected " + Renderer + ". 3D is not implemented.");
            CharacterVoice voice = manifest.Voice ?? new CharacterVoice { Mode = "inherit" };
            manifest.Voice = voice;
            if (voice.Mode != "inherit" && voice.Mode != "local" && voice.Mode != "off") throw new InvalidDataException("Voice mode must be inherit, local or off.");
            if (voice.Mode == "local" && Array.IndexOf(new[] { "Her own", "Bright", "Sly", "Sharp" }, voice.Style) < 0) throw new InvalidDataException("Unknown local voice style.");
            if (voice.Speed == 0) voice.Speed = 100;
            if (voice.Pitch < -12 || voice.Pitch > 12 || voice.Speed < 50 || voice.Speed > 200) throw new InvalidDataException("Voice pitch must be -12..12 and speed 50..200.");
            string personality = folder == null ? null : "You are " + manifest.Name + ", a personal desktop companion. Reply in the user's chosen language.";
            string personalityPath = null;
            if (!string.IsNullOrEmpty(manifest.PersonalityFile))
            {
                if (folder == null) throw new InvalidDataException("The embedded profile cannot read external personality files.");
                personalityPath = SafeFile(folder, manifest.PersonalityFile);
                if (new FileInfo(personalityPath).Length > 36000) throw new InvalidDataException("Personality exceeds 36 KiB.");
                personality = File.ReadAllText(personalityPath);
                if (string.IsNullOrWhiteSpace(personality) || personality.Length > 9000) throw new InvalidDataException("Personality must contain 1-9000 characters.");
            }
            Dictionary<string, byte[]> images = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            long imageBytes = 0;
            CharacterSprite[] entries = manifest.Sprites ?? new CharacterSprite[0];
            if (entries.Length > 64) throw new InvalidDataException("A profile may override at most 64 sprites.");
            foreach (CharacterSprite entry in entries)
            {
                if (folder == null || entry == null || !Regex.IsMatch(entry.Slot ?? "", "^[a-z][a-z0-9_]{0,63}$")) throw new InvalidDataException("Invalid sprite slot.");
                string name = "Capri.Sprite." + entry.Slot + ".png";
                if (images.ContainsKey(name)) throw new InvalidDataException("Duplicate sprite slot: " + entry.Slot);
                string path = SafeFile(folder, entry.File);
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || new FileInfo(path).Length > 50L * 1024 * 1024) throw new InvalidDataException("Sprites must be PNG files no larger than 50 MiB.");
                imageBytes += new FileInfo(path).Length;
                if (imageBytes > 64L * 1024 * 1024) throw new InvalidDataException("Profile sprites exceed 64 MiB in total.");
                byte[] snapshot = File.ReadAllBytes(path);
                using (Stream original = resource(name))
                {
                    if (original == null) throw new InvalidDataException("Unknown animation slot: " + entry.Slot);
                    using (Image expected = Image.FromStream(original)) ValidateImage(snapshot, expected.Size, entry.Slot);
                }
                images.Add(name, snapshot);
            }
            return new CharacterProfile(manifest, folder, personality, personalityPath, images);
        }

        internal CharacterProfile[] Scan()
        {
            Errors.Clear();
            List<CharacterProfile> profiles = new List<CharacterProfile> { Builtin };
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Builtin.Id };
            if (!Directory.Exists(Root)) return profiles.ToArray();
            try
            {
                SafeDirectory(Root);
                string[] folders = Directory.GetDirectories(Root);
                Array.Sort(folders, StringComparer.OrdinalIgnoreCase);
                foreach (string folder in folders)
                {
                    try
                    {
                        CharacterProfile profile = Load(folder);
                        if (!ids.Add(profile.Id)) throw new InvalidDataException("Duplicate character id: " + profile.Id);
                        profiles.Add(profile);
                    }
                    catch (InvalidDataException error) { Errors.Add(Path.GetFileName(folder) + ": " + error.Message); }
                    catch (IOException error) { Errors.Add(Path.GetFileName(folder) + ": " + error.Message); }
                    catch (UnauthorizedAccessException error) { Errors.Add(Path.GetFileName(folder) + ": " + error.Message); }
                }
            }
            catch (InvalidDataException error) { Errors.Add(error.Message); }
            catch (IOException error) { Errors.Add(error.Message); }
            catch (UnauthorizedAccessException error) { Errors.Add(error.Message); }
            return profiles.ToArray();
        }

        private static void ValidateImage(byte[] snapshot, Size expected, string slot)
        {
            byte[] header = new byte[24];
            using (Stream data = new MemoryStream(snapshot, false))
            {
                if (data.Read(header, 0, header.Length) != header.Length || BitConverter.ToString(header, 0, 8) != "89-50-4E-47-0D-0A-1A-0A" || BitConverter.ToString(header, 12, 4) != "49-48-44-52") throw new InvalidDataException("Invalid PNG: " + slot);
            }
            long width = ((long)header[16] << 24) | ((long)header[17] << 16) | ((long)header[18] << 8) | header[19];
            long height = ((long)header[20] << 24) | ((long)header[21] << 16) | ((long)header[22] << 8) | header[23];
            if (width != expected.Width || height != expected.Height) throw new InvalidDataException("Sprite geometry must match the built-in " + slot + " strip.");
            try
            {
                using (Stream data = new MemoryStream(snapshot, false))
                using (Image actual = Image.FromStream(data, false, true))
                using (Bitmap decoded = new Bitmap(actual))
                    if (actual.RawFormat.Guid != System.Drawing.Imaging.ImageFormat.Png.Guid || actual.Size != expected) throw new InvalidDataException("Invalid PNG: " + slot);
            }
            catch (OutOfMemoryException error) { throw new InvalidDataException("Cannot decode PNG: " + slot, error); }
            catch (System.Runtime.InteropServices.ExternalException error) { throw new InvalidDataException("Cannot decode PNG: " + slot, error); }
        }

        internal void Restore()
        {
            CharacterProfile[] profiles = Scan();
            Current = Builtin;
            string path = Path.Combine(Root, "selected.txt");
            if (!File.Exists(path)) return;
            try
            {
                SafeFile(Root, "selected.txt");
                if (new FileInfo(path).Length > 128) throw new InvalidDataException("Invalid character selection file.");
                string id = File.ReadAllText(path).Trim();
                foreach (CharacterProfile profile in profiles) if (profile.Id == id) { Current = profile; return; }
                Errors.Add("Selected character is missing or invalid: " + id + ". Using Capri.");
            }
            catch (InvalidDataException error) { Errors.Add(error.Message); }
            catch (IOException error) { Errors.Add(error.Message); }
            catch (UnauthorizedAccessException error) { Errors.Add(error.Message); }
        }

        internal void Select(CharacterProfile profile)
        {
            CharacterProfile ready = profile == Builtin ? Builtin : Load(profile.Folder);
            Persist(ready);
        }

        internal void RestoreSelection(CharacterProfile cached)
        {
            try { Persist(cached); }
            finally { Current = cached; }
        }

        private void Persist(CharacterProfile ready)
        {
            Directory.CreateDirectory(Root);
            SafeDirectory(Root);
            string path = SafeFile(Root, "selected.txt");
            string temporary = Path.Combine(Root, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporary, ready.Id + Environment.NewLine);
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                Current = ready;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        internal static string SafeFile(string folder, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || relative.IndexOf(':') >= 0 || Path.IsPathRooted(relative)) throw new InvalidDataException("Character file paths must be relative and cannot contain alternate data streams.");
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || relative.IndexOf("..", StringComparison.Ordinal) >= 0) throw new InvalidDataException("Character paths must stay inside their profile folder.");
            SafeDirectory(root.TrimEnd(Path.DirectorySeparatorChar));
            string part = path;
            while (part.Length >= root.Length)
            {
                if ((File.Exists(part) || Directory.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Character files cannot use symbolic links or junctions.");
                part = Path.GetDirectoryName(part);
                if (part == null) break;
            }
            return path;
        }

        private static void SafeDirectory(string folder)
        {
            if (Directory.Exists(folder) && (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("The character folder cannot be a symbolic link or junction.");
        }
    }
}
