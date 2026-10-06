using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace CloudMusicRemote
{
    public sealed class Shortcut
    {
        public int Key { get; set; }
        // 1 = Ctrl, 2 = Alt, 4 = Shift. Windows system shortcuts are excluded.
        public int Modifiers { get; set; }
        public string Label
        {
            get
            {
                string key = Key == 0x26 ? "↑" : Key == 0x28 ? "↓" : Key == 0x25 ? "←" : Key == 0x27 ? "→" : Key == 0x20 ? "Space" : Key >= 112 && Key <= 123 ? "F" + (Key - 111) : ((char)Key).ToString();
                return ((Modifiers & 1) != 0 ? "Ctrl + " : "") + ((Modifiers & 2) != 0 ? "Alt + " : "") + ((Modifiers & 4) != 0 ? "Shift + " : "") + key;
            }
        }
    }
    public sealed class PreferenceData
    {
        public int Side { get; set; }
        public Dictionary<string, Shortcut> Keys { get; set; }
    }
    public static class Preferences
    {
        public static readonly string[] Actions = { "toggle", "previous", "next", "volumeUp", "volumeDown" };
        static readonly object gate = new object();
        static PreferenceData current = Defaults();
        static readonly string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CloudMusicRemote");
        static readonly string path = Path.Combine(directory, "preferences.json");
        public static PreferenceData Defaults()
        {
            int[] codes = { 90, 65, 83, 38, 40 };
            var data = new PreferenceData { Side = 0, Keys = new Dictionary<string, Shortcut>() };
            for (int i = 0; i < Actions.Length; i++) data.Keys[Actions[i]] = new Shortcut { Key = codes[i], Modifiers = 3 };
            return data;
        }
        public static void Validate(PreferenceData data)
        {
            if (data == null || data.Side < 0 || data.Side > 2 || data.Keys == null || data.Keys.Count != Actions.Length) throw new ArgumentException("配置格式不正确");
            var combinations = new HashSet<string>();
            foreach (string action in Actions)
            {
                Shortcut shortcut;
                if (!data.Keys.TryGetValue(action, out shortcut) || shortcut == null) throw new ArgumentException("快捷键配置不完整");
                int key = shortcut.Key;
                if (shortcut.Modifiers < 1 || shortcut.Modifiers > 7 || !((key >= 48 && key <= 57) || (key >= 65 && key <= 90) || (key >= 112 && key <= 123) || (key >= 37 && key <= 40) || key == 32)) throw new ArgumentException("请使用 Ctrl / Alt / Shift 加字母、数字、方向键、空格或 F1–F12");
                if (!combinations.Add(shortcut.Modifiers + ":" + key)) throw new ArgumentException("该快捷键已用于其他操作");
            }
        }
        public static PreferenceData Snapshot()
        {
            lock (gate)
            {
                var copy = new PreferenceData { Side = current.Side, Keys = new Dictionary<string, Shortcut>() };
                foreach (var item in current.Keys) copy.Keys[item.Key] = new Shortcut { Key = item.Value.Key, Modifiers = item.Value.Modifiers };
                return copy;
            }
        }
        public static Shortcut Get(string action)
        {
            lock (gate)
            {
                Shortcut shortcut;
                if (!current.Keys.TryGetValue(action, out shortcut)) throw new ArgumentException("未知操作");
                return new Shortcut { Key = shortcut.Key, Modifiers = shortcut.Modifiers };
            }
        }
        public static string Load()
        {
            try
            {
                if (File.Exists(path))
                {
                    var data = new JavaScriptSerializer().Deserialize<PreferenceData>(File.ReadAllText(path));
                    Validate(data); lock (gate) current = data;
                }
                else
                {
                    string legacy = Path.Combine(directory, "settings.txt");
                    if (File.Exists(legacy))
                    {
                        string value = File.ReadAllText(legacy);
                        current.Side = value.EndsWith("off") ? 2 : value.StartsWith("1") ? 1 : 0;
                    }
                }
                return null;
            }
            catch { return "配置无法读取，已使用默认值；原文件未被删除。"; }
        }
        public static void Save(PreferenceData data)
        {
            Validate(data);
            lock (gate)
            {
                Directory.CreateDirectory(directory);
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(data));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                current = data;
            }
        }
        public static bool ShouldTrigger(int side, int button)
        {
            return side == 0 ? button == 2 : side == 1 && button == 1;
        }
    }
}
