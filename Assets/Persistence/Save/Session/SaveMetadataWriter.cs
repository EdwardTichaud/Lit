using System;
using System.IO;
using System.Text;

public static class SaveMetadataWriter
{
    public static void WriteAtomic(string path, string json)
    {
        if (string.IsNullOrWhiteSpace(path) || json == null) throw new ArgumentException("Métadonnées manquantes.");
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
