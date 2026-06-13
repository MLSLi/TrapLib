namespace TrapLib.Utilities;

public static class LocaleHelper
{
    public static bool IsChinese()
    {
        var name = (Locale.currentLangName ?? "").ToLowerInvariant();
        if (name == "zh" || name == "cn" || name == "zhs" || name == "zht"
            || name.StartsWith("zh-") || name.StartsWith("cn-"))
            return true;

        var langName = (Locale.currentLang?.name ?? "").ToLowerInvariant();
        return langName.Contains("中文") || langName.Contains("简体") || langName.Contains("chinese");
    }
}
