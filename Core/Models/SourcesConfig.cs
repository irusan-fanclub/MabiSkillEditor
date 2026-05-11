using System.Collections.Generic;

namespace MabiSkillEditor.Core.Models;

public class SourcesConfig
{
    public string SkillInfoIt                  { get; set; } = "data_00640.it";
    public string SkillInfoInnerPath           { get; set; } = "data/db/Skill/SkillInfo.xml";
    public string LocalizationIt               { get; set; } = "data_00756.it";
    public string LocalizationInnerPath        { get; set; } = "data/local/xml/SkillInfo.taiwan.txt";
    public string WeaponIt                     { get; set; } = "data_00633.it";
    public string WeaponInnerPath              { get; set; } = "data/db/itemDB_Weapon.xml";
    public string WeaponLocalizationInnerPath  { get; set; } = "data/local/xml/itemdb_weapon.taiwan.txt";
    public string ItemDbInnerPath              { get; set; } = "data/db/ItemDB.xml";
    public string ItemDbLocalizationInnerPath  { get; set; } = "data/local/xml/ItemDB.taiwan.txt";

    /// <summary>
    /// 已知 salt 清單（共用：所有 IT 解包都依序嘗試）
    /// 程式會在每次成功解包後把實際 work 的 salt 排到首位並寫回 sources.json
    /// 來源：mabi_it_workspace/salts.txt（新到舊）
    /// </summary>
    public List<string> KnownSalts { get; set; } = new()
    {
        "@6QeTuOaDgJlZcBm#9",
        "s_U[ht6c%!5gG4NZ|b",
        "F1#/e~MKiAP>|ksz/<",
        "]0/N}ofxT<K83MA]fO",
        "AAC(*()S&&**&*(A**",
        "C3)eWj]1D6_4?{ZF5d",
        "[^Uz6~kxX(j%w2q<X8",
        "/O^K7}^i*p)!Y)3_5&",
        "aT2d_jL%aX9s5j<7Kk",
        "oD2hPSDm]9QP_!tKy{",
        ":vEf?4wrglFd$rA$nc",
        "m5'hA,`aY*fx7opRL7",
        "3H;-s.E9^Txlt17}JD",
        "0bABB`[YIWF34K!mxz",
        "J'7TL!AGKHGI]5`;(j",
        "9t+.<N,jtbznQNrOzE",
        "Rzf;Q0v?,oXQQ[YE5m",
        "C+V?q-W>?;=iT81qvg",
        "EqCN'nOCGNaw<8NJ0{",
        "`K3;Z5~too=|XhHtmh",
        "@wvK#}'Xp)7DEA_2:#",
        "})wWb4?-sVGHNoPKpc",
        "1&w2!&w{Q)Fkz4e&p0",
        "xGqK]W+_eM5u3[8-8u",
        "smh=Pdw+%?wk?m4&(y",
        "3@6|3a[@<Ex:L=eN|g",
        "C(K^x&pBEeg7A5;{G9",
        "}F33F0}_7X^;b?PM/;",
        "DaXU_Vx9xy;[ycFz{1",
        "CuAVPMZx:E96:(Rxdw",
    };

    // ── 舊格式相容：若 sources.json 仍帶舊欄位，反序列化會走這些 setter，
    //    把舊值合併進 KnownSalts，避免使用者自定 salt 在升級時遺失 ──
    public string? SkillInfoItSalt
    {
        set { if (!string.IsNullOrEmpty(value) && !KnownSalts.Contains(value)) KnownSalts.Insert(0, value); }
    }
    public string? LocalizationItSalt
    {
        set { if (!string.IsNullOrEmpty(value) && !KnownSalts.Contains(value)) KnownSalts.Insert(0, value); }
    }
    public List<string>? SkillInfoItSalts
    {
        set
        {
            if (value == null) return;
            foreach (var s in value)
                if (!string.IsNullOrEmpty(s) && !KnownSalts.Contains(s)) KnownSalts.Insert(0, s);
        }
    }
    public List<string>? LocalizationItSalts
    {
        set
        {
            if (value == null) return;
            foreach (var s in value)
                if (!string.IsNullOrEmpty(s) && !KnownSalts.Contains(s)) KnownSalts.Insert(0, s);
        }
    }
}
