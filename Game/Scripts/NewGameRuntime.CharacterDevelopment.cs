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
        Text(PersonName(person));
        {var w0=Snapshot();if(CentralStanding.KnownTitles(w0,Session.PlayerControl,person) is {Length:>0} titles)Text(string.Join("　",titles));
         if(CentralStanding.SeatAtSite(w0,person) is {} seat)Text($"席次　{seat.Seat}／{seat.Of}");
         if(person==Session.PlayerControl&&SocialWorld.Site(w0,person) is {} here&&Territory2.Profile(w0,here) is {} sp)Text($"{w0.Definitions.Sites.Single(x=>x.Id==here).Name}　人口{sp.Population}　田畑{sp.Agriculture}　町{sp.Commerce}　工房{sp.Workshop}　牧{sp.Pasture}　民心{sp.Morale}　治安{sp.Security}"+(sp.Monto>0?$"　門徒{sp.Monto}":"")+(sp.Sections>0?$"　区画{sp.Sections}":""));}
        // 志と兵科適性（一覧の Persons2）。作成武将の本人は志を選べる。
        {var w2=Snapshot();
         if(Persons2.Aspiration(w2,person) is {} asp)Text($"志　{asp.Value}");
         if(Persons2.On(w2)){Text(Persons2.Aptitude(w2,person));
          if(person==Session.PlayerControl&&w2.NewGame!.Registered.Any(r=>r.Id==person.Value)){var row=new HBoxContainer();box.AddChild(row);long rv=Session.Revision;
           foreach(var a in Persons2.Aspirations){var b=new Button{Text=a};b.Pressed+=()=>{if(Session.ChooseAspiration(a,rv).Code==ResultCode.Accepted){popup.Hide();Refresh();ShowCharacterDevelopment();}};row.AddChild(b);}}}}
        // 家の記録と、当主なら家の方針・年貢・口入れ（一覧の Domestic2）。
        {var w1=Snapshot();var faction=w1.NewGame!.Start.People.SingleOrDefault(p=>p.Id==person.Value)?.Faction;
         if(faction!=null)foreach(var line in Domestic2.FamilyRecord(w1,Session.PlayerControl,new(faction)))Text(line);
         if(person==Session.PlayerControl&&Domestic2.On(w1)&&faction!=null&&HouseSuccession.Head(w1,new(faction))==person)
         {
             long shown=Session.Revision;var note=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};
             void Order(string kind,string? target,int value){var r=Session.DecideDomestic(kind,target,value,shown);if(r.Code!=ResultCode.Accepted){note.Text=r.VisibleReason;return;}popup.Hide();Refresh();ShowCharacterDevelopment();}
             var policies=new HBoxContainer();box.AddChild(policies);
             foreach(var p in new[]{Domestic2.Farming,Domestic2.Trade,Domestic2.Arms,Domestic2.Building,Domestic2.Thrift}){var b=new Button{Text=p};b.Pressed+=()=>Order("policy",p,0);policies.AddChild(b);}
             var rates=new HBoxContainer();box.AddChild(rates);
             foreach(var (label,v) in new[]{("四公六民",40),("五公五民",50),("六公四民",60)}){var b=new Button{Text=label};b.Pressed+=()=>Order("decree",null,v);rates.AddChild(b);}
             box.AddChild(note);
             // 同盟の申し入れ（一覧の Politics2）。
             foreach(var p in Politics2.Proposals(w1,person)){Text(p.Text);var row=new HBoxContainer();box.AddChild(row);
              foreach(var (label,yes) in new[]{("受ける",true),("断る",false)}){var b=new Button{Text=label};b.Pressed+=()=>{var r=Session.AnswerProposal(p.Id,yes,shown);if(r.Code!=ResultCode.Accepted){note.Text=r.VisibleReason;return;}popup.Hide();Refresh();ShowCharacterDevelopment();};row.AddChild(b);}}
         }
         if(person==Session.PlayerControl)foreach(var job in Domestic2.Board(w1,person))Text($"{job.Kind}　{job.IssuerName}　{job.Fee}文");
         // 探物（一覧の Logistics2）：引き受け・見つける・精算。
         if(person==Session.PlayerControl&&Logistics2.On(w1))
         {
             long seen=Session.Revision;var hint=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};
             void Work(string step,string target){var r=Session.TreasureWork(step,target,seen);if(r.Code!=ResultCode.Accepted){hint.Text=r.VisibleReason;return;}popup.Hide();Refresh();ShowCharacterDevelopment();}
             foreach(var (issuer,t,fee) in Logistics2.Offers(w1,person))Button($"{t.Name}の探物を引き受ける",()=>Work("accept",t.Id));
             foreach(var j in Logistics2.Jobs(w1).Where(j=>j.Worker==person&&j.Phase!="精算済み"))
             {var t=Logistics2.Treasures(w1).Single(x=>x.Id==j.Treasure);
              if(j.Phase=="引受中")Button($"{t.Name}を{(j.Buy?"買う":"見つける")}",()=>Work("find",j.Id));else Button($"{t.Name}の探物を精算する",()=>Work("settle",j.Id));}
             box.AddChild(hint);
         }}
        var chooser=new OptionButton();box.AddChild(chooser);var people=Snapshot().NewGame!.Start.People;
        // Names already belong to the scenario roster; the selected card never exposes private values.
        for(int n=0;n<people.Length;n++){chooser.AddItem(people[n].Name);if(people[n].Id==person.Value)chooser.Select(n);}
        chooser.ItemSelected+=n=>{popup.Hide();ShowCharacterDevelopmentFor(new(people[(int)n].Id));};
        Text("基礎能力");
        foreach(var value in card.Abilities)Text(value.Name+"　"+(value.Value?.ToString()??(value.ObservedAt is {} seen?value.Confidence+" ／ "+TreatyDate(seen)+"・"+value.Source:"不明")));
        Text("技能");
        foreach(var value in card.Skills)Text(value.Name+"："+(value.Value?.ToString()??value.Confidence)+(card.Own?$" ／ 経験 {value.Experience}":value.ObservedAt is {} at?$" ／ {TreatyDate(at)}・{value.Source}":""));
        if(CharacterRelations.View(Snapshot(),person) is {} relation){
            Text("出来事");
            if(relation.Known.Length==0){}
            foreach(var known in relation.Known.Reverse().Take(8))Text(known.Description+" ／ 出来事 "+TreatyDate(known.ObservedAt)+"・認識 "+TreatyDate(known.LearnedAt));
        }
        if(card.Own){
            var world=Snapshot();var active=world.Tasks.LastOrDefault(t=>t.Executor==person&&t.Action==ActionKind.Train&&t.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed));long shown=Session.Revision;
            var feedback=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(feedback);
            void Done(CommandResult r){if(r.Code is not(ResultCode.Accepted or ResultCode.AlreadyAccepted)){feedback.Text=r.VisibleReason;return;}popup.Hide();Refresh();ShowCharacterDevelopment();}
            if(active!=null){{}Button("修行を中断する",()=>Done(Session.StopPractice(active.Id,shown)));}
            else{
                bool busy=world.Tasks.Any(t=>t.Executor==person&&t.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed));
                bool atSite=world.Locations.Any(l=>l.Actor==person&&l.Kind==LocationKind.AtSite);
                {}
                foreach(var skill in CharacterDevelopment.Skills(Snapshot()))Button(CharacterDevelopment.Name(skill)+"を修行する",()=>{
                    try{Done(Session.Submit(CharacterDevelopment.Practice(Snapshot(),"request:development-ui/"+shown+"/"+skill,skill),shown));}
                    catch(System.IO.InvalidDataException e){feedback.Text=e.Message;}
                },busy||!atSite||PersonalLife.Resting(world,world.ElapsedSeconds,world.CommitSequence+1));
                // 師事：その技能が本人より高い人が同じ拠点にいれば、その人に教わる。
                foreach(var skill in CharacterDevelopment.Skills(Snapshot()))foreach(var teacher in CharacterDevelopment.Teachers(Snapshot(),skill).Take(1)){var t=teacher;var k=skill;
                    Button(CharacterDevelopment.Name(k)+"："+(FamilyBirth.People(Snapshot()).SingleOrDefault(x=>x.Id==t.Value)?.Name??t.Value)+"に師事",()=>{
                        try{Done(Session.Submit(CharacterDevelopment.Learn(Snapshot(),"request:development-ui/"+shown+"/learn/"+k,k,t),shown));}
                        catch(System.IO.InvalidDataException e){feedback.Text=e.Message;}
                    },busy||!atSite||PersonalLife.Resting(world,world.ElapsedSeconds,world.CommitSequence+1));}
            }
        }
        Button("地図へ戻る",()=>popup.Hide());popup.PopupHide+=()=>popup.QueueFree();popup.PopupCentered(new Vector2I(610,590));
    }
}
