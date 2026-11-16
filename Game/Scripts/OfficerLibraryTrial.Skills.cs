using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
// 作成武将の技能の自由な配分（32技能、1技能50まで、合計は予算の内）。経歴の一覧の最後の「自由」で開く。
public partial class OfficerLibraryTrial
{
    public const string FreeBackground="自由";
    GridContainer? freeGrid;VBoxContainer? freeBox;Label? freeLeft;readonly SpinBox[] freeSkills=new SpinBox[CharacterDevelopment.Skills32List.Length];
    void BuildFreeSkills(GridContainer grid)
    {
        background.AddItem(FreeBackground);
        // 表（4列）の外、表のすぐ下に置く。
        freeBox=new VBoxContainer{Name="FreeSkillsBox"};grid.AddSibling(freeBox);
        freeLeft=new Label{Name="FreeSkillsLeft"};freeBox.AddChild(freeLeft);
        freeGrid=new GridContainer{Name="FreeSkills",Columns=16};freeBox.AddChild(freeGrid);
        for(int n=0;n<freeSkills.Length;n++)
        {
            freeGrid.AddChild(new Label{Text=CharacterDevelopment.Name(CharacterDevelopment.Skills32List[n])});
            var box=new SpinBox{MinValue=0,MaxValue=CharacterDevelopment.CreatedCap,Step=1,CustomMinimumSize=new(70,0)};freeSkills[n]=box;freeGrid.AddChild(box);box.ValueChanged+=_=>ShowFreeLeft();
        }
        background.ItemSelected+=_=>ShowFreeSkills();ShowFreeSkills();
    }
    bool FreeChosen=>background.Selected==CharacterDevelopment.Backgrounds.Length+1;
    void ShowFreeSkills(){if(freeBox==null)return;freeBox.Visible=FreeChosen;ShowFreeLeft();}
    void ShowFreeLeft(){if(freeLeft==null)return;long left=CharacterDevelopment.CreatedBudget-CharacterDevelopment.Spent(FreeSkills());freeLeft.Text="残り "+(left/3600)+"時間";}
    OfficerSkill[] FreeSkills()=>freeSkills.Select((b,n)=>new OfficerSkill(CharacterDevelopment.Skills32List[n],(int)b.Value)).Where(x=>x.Level>0).ToArray();
    void RestoreFreeSkills(RegisteredOfficer p)
    {
        for(int n=0;n<freeSkills.Length;n++)freeSkills[n].SetValueNoSignal(p.Background==FreeBackground?p.StartSkills?.SingleOrDefault(x=>x.Skill==CharacterDevelopment.Skills32List[n])?.Level??0:0);
        if(p.Background==FreeBackground)background.Select(CharacterDevelopment.Backgrounds.Length+1);ShowFreeSkills();
    }
    // 家系と信仰（未設定は推測しない）。
    OptionButton lineage=null!,faith=null!;
    void BuildLineageFaith(GridContainer grid)
    {
        lineage=new OptionButton{Name="OfficerLineage"};grid.AddChild(new Label{Text="家系"});grid.AddChild(lineage);lineage.AddItem("未設定");foreach(var x in RegisteredOfficer.Lineages)lineage.AddItem(x);
        faith=new OptionButton{Name="OfficerFaith"};grid.AddChild(new Label{Text="信仰"});grid.AddChild(faith);faith.AddItem("未設定");foreach(var x in RegisteredOfficer.Faiths)faith.AddItem(x);
    }
    void RestoreLineageFaith(RegisteredOfficer? p){lineage.Select(p?.Lineage==null?0:1+Array.IndexOf(RegisteredOfficer.Lineages,p.Lineage));faith.Select(p?.Faith==null?0:1+Array.IndexOf(RegisteredOfficer.Faiths,p.Faith));}
    string? ChosenLineage=>lineage.Selected<=0?null:RegisteredOfficer.Lineages[lineage.Selected-1];
    string? ChosenFaith=>faith.Selected<=0?null:RegisteredOfficer.Faiths[faith.Selected-1];
    (string? Background,OfficerSkill[]? Skills) ChosenSkills()
    {
        if(FreeChosen){var s=FreeSkills();CharacterDevelopment.CheckCreated(s);return (FreeBackground,s.Length==0?null:s);}
        return background.Selected<=0?(null,null):(CharacterDevelopment.Backgrounds[background.Selected-1].Name,CharacterDevelopment.Backgrounds[background.Selected-1].Skills);
    }
}
