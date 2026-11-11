using Godot;
using System.Collections.Generic;
using System.Linq;
using HakutaiKagami.Core.Contracts;
// 兵のドット絵。部隊は本人の家の足軽、寺社の武装勢力は僧兵、賊は浪人の甲冑。
// 3Dの人形が演じている動きと時刻をそのまま読み取り、同じ位置に同じ動きの絵を出す。
public partial class NewGameRuntime
{
    readonly Dictionary<Officer3DCustomActor,DotOfficerActor> soldierDots=new();
    static readonly string[] UnitWeapons=["yari","teppo","yumi"];
    void SyncSoldierDots(ContractWorld w)
    {
        foreach(var d in soldierDots.Values)d.Visible=false;
        if(!UsesDotOfficers)return;
        string? house=w.NewGame!.Start.People.SingleOrDefault(p=>p.Id==w.PlayerControl.Value)?.Faction;
        for(int i=0;i<militaryModels.Count;i++)Mirror(militaryModels[i],DotOfficerLooks.Soldier(house,UnitWeapons[i%UnitWeapons.Length]));
        for(int i=0;i<banditModels.Count;i++)Mirror(banditModels[i],$"generic/ronin/{(i%2==0?"prime":"young")}/{i%3}/war/鉄錆");
        if(templeModel!=null)Mirror(templeModel,DotOfficerLooks.WarriorMonk);
    }
    void Mirror(Officer3DCustomActor host,string key)
    {
        if(!host.Visible)return;
        if(!soldierDots.TryGetValue(host,out var dot)){dot=new(){PixelSize=.13f};stage.AddChild(dot);soldierDots[host]=dot;}
        if(!dot.Apply(key))return;
        string clip=host.Player.CurrentAnimation;string anim=host.Clips.FirstOrDefault(c=>c.Value==clip).Key??"idle";
        dot.Position=host.Position;dot.Visible=true;host.Visible=false;dot.Face(host.Basis.Z,camera);
        dot.Freeze(anim,host.Player.CurrentAnimationPosition);
    }
    internal IEnumerable<object> SoldierDotPose()=>soldierDots.Values.Where(d=>d.Visible).Select(d=>(object)new{d.Key,Position=V(d.Position)}).OrderBy(x=>x.ToString());
}
