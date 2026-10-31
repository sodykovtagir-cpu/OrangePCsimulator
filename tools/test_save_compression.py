#!/usr/bin/env python3
"""Compile the actual SaveUtility/DataLoader methods and test OPC3 + old saves.

Does not substitute for Unity save/reload smoke tests with a real user's world.
"""
from pathlib import Path
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1] / 'Assets/Scripts/Assembly-CSharp'
source = (root / 'SaveManagement/SaveUtility.cs').read_text()
assert 'CompressedTag = "OPC3:"' in source
assert 'new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true)' in source
assert 'MaxDecodedBytes' in source
assert 'fileText.StartsWith(CompressedTag' in source
assert 'if (!FixedEquals(hmac, ComputeHmac(body)))' in source
assert 'public static SaveIntegrity DecodeHeader(' in source
assert 'using (var reader = new StreamReader(zip, new UTF8Encoding(false, true)))' in source
loader_source = (root / 'SaveManagement/DataLoader.cs').read_text()
assert 'public void LoadHeaderFromPath()' in loader_source
assert 'SaveUtility.DecodeHeader(File.ReadAllText(Path), out header)' in loader_source
assert 'if (headerOnly)' in loader_source and 'SaveUtility.Decode(File.ReadAllText(Path), out payload)' in loader_source
file_menu = (root / 'FileMenu.cs').read_text()
assert 'l.LoadHeaderFromPath();' in file_menu and 'var world = l.Content;' in file_menu
if not shutil.which('mcs') or not shutil.which('mono'):
    print('NOTE: Mono unavailable; checked OPC3 source guards only')
    raise SystemExit(0)

harness = r'''using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using SaveManagement;
namespace UnityEngine {
    public enum CompressionLevel { None, Optimal } // emulate Unity 2022 naming collision
    public static class Application {
        public static string persistentDataPath = "/tmp";
        public static string streamingAssetsPath = "/tmp";
    }
    public static class JsonUtility {
        public static string ToJson(object data) { return "{\"version\":\"test\"}"; }
        public static T FromJson<T>(string text) where T : new() { return new T(); }
    }
}
namespace UnityEngine.Networking {
    public class DownloadHandler { public string text; }
    public class UnityWebRequestAsyncOperation {
        public event Action<UnityWebRequestAsyncOperation> completed;
    }
    public class UnityWebRequest {
        public enum Result { ConnectionError, ProtocolError, Success }
        public Result result;
        public string error;
        public DownloadHandler downloadHandler = new DownloadHandler();
        public static UnityWebRequest Get(string path) { return new UnityWebRequest(); }
        public UnityWebRequestAsyncOperation SendWebRequest() { return new UnityWebRequestAsyncOperation(); }
    }
}
class Harness {
    static void Check(bool test, string name) { if (!test) throw new Exception(name); }
    static string SignedOld(string payload) {
        byte[] key = Encoding.UTF8.GetBytes("OrangePCSim-SaveInt-2026::do-not-edit");
        string signature;
        using (var hmac = new HMACSHA256(key))
            signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        return SaveUtility.EncryptDecrypt(SaveUtility.Tag + signature + "\n" + payload);
    }
    public static void Main(string[] args) {
        string text = "{\"name\":\"Привет 🖥\"}\n" + new string('x', 1048576);
        string decoded;
        string modern = SaveUtility.Encode(text);
        Check(modern.StartsWith(SaveUtility.CompressedTag), "missing OPC3 header");
        Check(modern.Length < text.Length / 8, "repeated JSON was not compressed");
        Check(SaveUtility.Decode(modern, out decoded) == SaveUtility.SaveIntegrity.Ok && decoded == text,
              "OPC3 round-trip / Unicode");
        Check(SaveUtility.DecodeHeader(modern, out decoded) == SaveUtility.SaveIntegrity.Ok &&
              decoded == "{\"name\":\"Привет 🖥\"}", "OPC3 header-only preview");
        var modified = modern.ToCharArray(); modified[modified.Length - 10] =
            modified[modified.Length - 10] == 'A' ? 'B' : 'A';
        Check(SaveUtility.Decode(new string(modified), out decoded) == SaveUtility.SaveIntegrity.Tampered,
              "tampered OPC3 accepted");
        Check(SaveUtility.DecodeHeader(new string(modified), out decoded) == SaveUtility.SaveIntegrity.Tampered,
              "tampered OPC3 header accepted");
        Check(SaveUtility.Decode(SignedOld(text), out decoded) == SaveUtility.SaveIntegrity.Ok && decoded == text,
              "OPC2 backwards compatibility");
        var media = new byte[262144]; new Random(138).NextBytes(media);
        string binaryHeavy = "{\"pic\":\"" + Convert.ToBase64String(media) + "\"}\n";
        string oldMedia = SignedOld(binaryHeavy);
        string newMedia = SaveUtility.Encode(binaryHeavy);
        Check(Encoding.UTF8.GetByteCount(newMedia) < Encoding.UTF8.GetByteCount(oldMedia) * .8,
              "OPC3 failed to shrink already-compressed image data");
        Check(SaveUtility.Decode(newMedia, out decoded) == SaveUtility.SaveIntegrity.Ok && decoded == binaryHeavy,
              "image-heavy OPC3 round-trip");
        Check(SaveUtility.Decode(SaveUtility.EncryptDecrypt(text), out decoded) ==
              SaveUtility.SaveIntegrity.None && decoded == text, "unsigned legacy backwards compatibility");
        var path = Path.Combine(args[0], "test.opc");
        var loader = new DataLoader(path, new GameData()); loader.Content = text;
        loader.WriteToFile();
        Check(File.ReadAllText(path).StartsWith(SaveUtility.CompressedTag), "loader did not write OPC3");
        var preview = new DataLoader(path); preview.LoadHeaderFromPath();
        Check(preview.GameData != null, "header-only loader failed");
        Check(preview.Content == text, "lazy world hydration failed");
        var reopened = new DataLoader(path); reopened.LoadFromPath();
        Check(reopened.Content == text && reopened.GameData != null, "DataLoader reload failed");
        Console.WriteLine("PASS: compressed OPC3 roundtrip, HMAC, OPC2/legacy reads, Unicode, DataLoader");
    }
}
'''
with tempfile.TemporaryDirectory(prefix='pcos-save-test-') as tmp:
    test = Path(tmp) / 'test.cs'
    test.write_text(harness)
    exe = Path(tmp) / 'test.exe'
    subprocess.run(['mcs', '-out:' + str(exe), str(test),
                    str(root / 'SaveManagement/SaveUtility.cs'),
                    str(root / 'SaveManagement/DataLoader.cs'), str(root / 'GameData.cs')],
                   check=True)
    subprocess.run(['mono', str(exe), tmp], check=True)
