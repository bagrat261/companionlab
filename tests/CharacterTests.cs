using System;
using System.Drawing;
using System.IO;
using System.Text;
using CapriDesktop;

internal static class CharacterTests
{
    private static int count;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        count++;
        Console.WriteLine("PASS " + name);
    }

    private static void Reject(Action action, string name)
    {
        bool rejected = false;
        try { action(); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, name);
    }

    private static Stream Resource(string name)
    {
        if (name == "CompanionLab.Character.capri.json") return new MemoryStream(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"id\":\"capri\",\"name\":\"Capri\",\"renderer\":\"capri-strip-v1\"}"));
        if (name != "Capri.Sprite.idle.png") return null;
        MemoryStream data = new MemoryStream();
        using (Bitmap bitmap = new Bitmap(32, 16)) bitmap.Save(data, System.Drawing.Imaging.ImageFormat.Png);
        data.Position = 0;
        return data;
    }

    private static string Json(string extra)
    {
        return "{\"schemaVersion\":1,\"id\":\"luna\",\"name\":\"Luna\",\"renderer\":\"capri-strip-v1\"," + extra + "}";
    }

    public static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(root);
        string folder = Path.Combine(root, "luna");
        Directory.CreateDirectory(folder);
        string manifest = Path.Combine(folder, "character.json");
        CharacterCatalog catalog = new CharacterCatalog(root, Resource);
        Check(catalog.Current.Id == "capri", "built-in Capri works without external files");
        File.WriteAllText(manifest, Json("\"personalityFile\":\"personality.md\",\"voice\":{\"mode\":\"local\",\"style\":\"Bright\",\"pitch\":2,\"speed\":110}"));
        File.WriteAllText(Path.Combine(folder, "personality.md"), "You are Luna. Speak Russian or English as requested.", Encoding.UTF8);
        CharacterProfile luna = catalog.Load(folder);
        Check(luna.Name == "Luna" && luna.Personality.Contains("Luna") && luna.Voice.Style == "Bright", "loads name personality and voice from disk");
        catalog.Select(luna);
        Check(catalog.Current.Id == "luna" && File.ReadAllText(Path.Combine(root, "selected.txt")).Trim() == "luna", "selection persists independently of AI settings");
        CharacterCatalog reopened = new CharacterCatalog(root, Resource);
        reopened.Restore();
        Check(reopened.Current.Id == "luna", "selection restores after restart");
        File.WriteAllText(manifest, "{");
        Reject(delegate { catalog.Load(folder); }, "rejects malformed JSON");
        Check(catalog.Current.Id == luna.Id, "invalid load preserves current selection");
        reopened.Restore();
        Check(reopened.Current.Id == "capri" && reopened.Errors.Count > 0, "invalid saved profile falls back with a diagnostic");
        File.WriteAllText(manifest, Json("\"personalityFile\":\"../outside.md\""));
        Reject(delegate { catalog.Load(folder); }, "rejects parent traversal");
        File.WriteAllText(manifest, Json("\"personalityFile\":\"C:/outside.md\""));
        Reject(delegate { catalog.Load(folder); }, "rejects absolute paths");
        File.WriteAllText(manifest, Json("\"personalityFile\":\"note.md:stream\""));
        Reject(delegate { catalog.Load(folder); }, "rejects alternate data streams");
        File.WriteAllText(manifest, Json("\"personalityFile\":\"" + Path.Combine(folder, "personality.md").Replace("\\", "/") + "\""));
        Reject(delegate { catalog.Load(folder); }, "rejects absolute paths even inside the profile");
        File.WriteAllText(manifest, Json("\"personalityFile\":\"missing.md\""));
        Reject(delegate { catalog.Load(folder); }, "rejects missing personality file");
        File.WriteAllText(manifest, Json("\"voice\":{\"mode\":\"execute\",\"style\":\"Bright\"}"));
        Reject(delegate { catalog.Load(folder); }, "rejects executable or unsupported voice modes");
        File.WriteAllText(manifest, Json("\"voice\":{\"mode\":\"local\",\"style\":\"Unknown\"}"));
        Reject(delegate { catalog.Load(folder); }, "rejects unavailable local styles");
        File.WriteAllText(manifest, Json("\"voice\":{\"mode\":\"local\",\"style\":\"Bright\",\"speed\":999}"));
        Reject(delegate { catalog.Load(folder); }, "rejects invalid voice ranges");
        File.WriteAllText(manifest, Json("\"voice\":{\"mode\":\"off\"}").Replace("capri-strip-v1", "vrm"));
        Reject(delegate { catalog.Load(folder); }, "rejects unsupported renderer explicitly");
        File.WriteAllText(manifest, Json("\"voice\":{\"mode\":\"off\"}").Replace("\"luna\"", "\"../luna\""));
        Reject(delegate { catalog.Load(folder); }, "rejects invalid identifiers");
        File.WriteAllText(manifest, Json("\"voice\":{\"mode\":\"off\"}").Replace(":1,", ":99,"));
        Reject(delegate { catalog.Load(folder); }, "rejects unknown schema version");
        File.WriteAllText(manifest, Json("\"sprites\":[{\"slot\":\"idle\",\"file\":\"idle.png\"}],\"voice\":{\"mode\":\"off\"}"));
        using (Bitmap bitmap = new Bitmap(32, 16)) { bitmap.SetPixel(0, 0, Color.Magenta); bitmap.Save(Path.Combine(folder, "idle.png")); }
        CharacterProfile visual = catalog.Load(folder);
        using (Stream data = visual.OpenSprite("Capri.Sprite.idle.png"))
        using (Bitmap bitmap = new Bitmap(data)) Check(bitmap.GetPixel(0, 0).ToArgb() == Color.Magenta.ToArgb(), "custom PNG reaches the renderer resource seam");
        using (Bitmap bitmap = new Bitmap(33, 16)) bitmap.Save(Path.Combine(folder, "idle.png"));
        using (Stream data = visual.OpenSprite("Capri.Sprite.idle.png"))
        using (Bitmap bitmap = new Bitmap(data)) Check(bitmap.Size == new Size(32, 16) && bitmap.GetPixel(0, 0).ToArgb() == Color.Magenta.ToArgb(), "validated sprite stays stable after its file changes");
        Reject(delegate { catalog.Load(folder); }, "rejects incompatible frame geometry");
        File.WriteAllText(Path.Combine(folder, "idle.png"), "corrupt png");
        Reject(delegate { catalog.Load(folder); }, "rejects corrupt images as profile validation errors");
        Check(catalog.Scan().Length == 1 && catalog.Errors.Count == 1, "corrupt PNG cannot crash profile discovery");
        File.WriteAllText(manifest, Json("\"sprites\":[{\"slot\":\"invented\",\"file\":\"idle.png\"}]"));
        Reject(delegate { catalog.Load(folder); }, "rejects unknown animation names");
        File.WriteAllText(manifest, Json("\"voice\":{\"mode\":\"off\"}"));
        string duplicate = Path.Combine(root, "duplicate");
        Directory.CreateDirectory(duplicate);
        File.Copy(manifest, Path.Combine(duplicate, "character.json"), true);
        CharacterProfile[] found = catalog.Scan();
        Check(found.Length == 2 && catalog.Errors.Count == 1, "duplicate IDs do not silently overwrite profiles");
        File.WriteAllText(manifest, new string(' ', 70000));
        Reject(delegate { catalog.Load(folder); }, "rejects oversized manifests");
        Console.WriteLine("PASS " + count + " character tests");
        return 0;
    }
}
