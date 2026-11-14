using System.IO;
using Godot;

// 検証記録の置き場所（2026-11-14）。Docs の各工程の runtime-local は D ドライブへのつなぎ（ジャンクション）で、
// 保存の仕組みはつなぎを通る保存先を拒む（安全のため）。つなぎを通る場所は、つなぎの先の本当の場所に直して渡す。
// つなぎを通らない場所はそのまま返す。
public static class RecordPath
{
    public static string Globalize(string resource)=>Real(ProjectSettings.GlobalizePath(resource));
    public static string Real(string path)
    {
        string full=Path.GetFullPath(path);
        for(var d=new DirectoryInfo(full);d!=null;d=d.Parent)
        {
            if(!d.Exists||(d.Attributes&FileAttributes.ReparsePoint)==0)continue;
            if(d.ResolveLinkTarget(true) is not {} target)break;
            string rest=Path.GetRelativePath(d.FullName,full);
            return Real(rest=="."?target.FullName:Path.Combine(target.FullName,rest));
        }
        return full;
    }
}
