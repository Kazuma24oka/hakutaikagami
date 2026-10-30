using Godot;
using System;
using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameRuntime
{
    void ShowCharacterDevelopment()=>ShowCharacterDevelopmentFor(Session.PlayerControl);
    void ShowCharacterDevelopmentFor(EntityId person)
    {
        var card=Session.CharacterCard(person);if(card==null)return;
        var popup=new PopupPanel();AddChild(popup);
        var scroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};popup.AddChild(scroll);scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var box=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};box.AddThemeConstantOverride("separation",8);scroll.AddChild(box);
        void Text(string text)=>box.AddChild(new Label{Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart});
        void Button(string text,Action action,bool disabled=false){var b=new Button{Text=text,Disabled=disabled,CustomMinimumSize=new Vector2(0,34)};box.AddChild(b);b.Pressed+=action;}
        Text(PersonName(person)+(card.Own?" — 自分の能力と熟練":" — 自分が知っている人物情報"));
        var chooser=new OptionButton();box.AddChild(chooser);var people=Snapshot().NewGame!.Start.People;
        // Names already belong to the scenario roster; the selected card never exposes private values.
        for(int n=0;n<people.Length;n++){chooser.AddItem(people[n].Name);if(people[n].Id==person.Value)chooser.Select(n);}
        chooser.ItemSelected+=n=>{popup.Hide();ShowCharacterDevelopmentFor(new(people[(int)n].Id));};
        Text("基礎能力");
        foreach(var value in card.Abilities)Text(value.Name+"："+(value.Value?.ToString()??"未確認"));
        Text("技能（経験・勲功・役職とは別）");
        foreach(var value in card.Skills)Text(value.Name+"："+(value.Value?.ToString()??value.Confidence)+(card.Own?$" ／ 経験 {value.Experience}":value.ObservedAt is {} at?$" ／ {TreatyDate(at)}・{value.Source}":""));
        Text(card.Note);
        if(CharacterRelations.View(Snapshot(),person) is {} relation){
            Text("この相手との、知っている出来事");
            if(relation.Known.Length==0)Text("確認できるやり取りの記録はまだありません。");
            foreach(var known in relation.Known.Reverse().Take(8))Text(known.Description+" ／ 出来事 "+TreatyDate(known.ObservedAt)+"・認識 "+TreatyDate(known.LearnedAt));
            Text(relation.Note);
        }
        if(card.Own){
            var world=Snapshot();var active=world.Tasks.LastOrDefault(t=>t.Executor==person&&t.Action==ActionKind.Train&&t.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed));long shown=Session.Revision;
            var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);
            void Done(CommandResult r){if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=r.VisibleReason;return;}popup.Hide();Refresh();ShowCharacterDevelopment();}
            if(active!=null){Text("修行の受付・実行中です。時間は地図と同じ時計で進みます。");Button("修行を中断する（未完了分は加算しません）",()=>Done(Session.StopPractice(active.Id,shown)));}
            else{
                bool busy=world.Tasks.Any(t=>t.Executor==person&&t.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed));
                bool atSite=world.Locations.Any(l=>l.Actor==person&&l.Kind==LocationKind.AtSite);
                Text("無料の自主修行 — 30分。世界を止めずに取り組みます。");
                foreach(var skill in Enum.GetValues<DevelopmentSkill>())Button(CharacterDevelopment.Name(skill)+"を修行する（参照："+CharacterDevelopment.Name(CharacterDevelopment.Ability(skill))+"・同技能）",()=>{
                    try{Done(Session.Submit(CharacterDevelopment.Practice(Snapshot(),"request:development-ui/"+shown+"/"+skill,skill),shown));}
                    catch(System.IO.InvalidDataException e){feedback.Text=e.Message;}
                },busy||!atSite||PersonalLife.Resting(world,world.ElapsedSeconds,world.CommitSequence+1));
            }
        }
        Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(610,590));
    }
}
