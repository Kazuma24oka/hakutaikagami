using Godot;
using HakutaiKagami.Core.Contracts;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public partial class DiplomaticLetterCheck : Node
{
    async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    async Task WaitSave(NewGameRuntime game)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (game.IsSaving && DateTime.UtcNow < deadline) await Frame();
        Check(!game.IsSaving && game.LastSaveError == null, "保存完了");
    }

    static void Check(bool ok, string reason)
    {
        if (!ok) throw new InvalidDataException(reason);
    }

    public override async void _Ready()
    {
        try
        {
            var entry = new NewGameFrontEnd();
            AddChild(entry);
            await Frame();
            var start = entry.CreateSiegeReview(Path.Combine(Path.GetTempPath(), "diplomacy-" + Guid.NewGuid()));
            entry.QueueFree();
            var game = new NewGameRuntime();
            game.Configure(start.Store, start.Profile, false);
            AddChild(game);
            await Frame();
            await WaitSave(game);
            var own = new EntityId("created:siege-house");
            var target = new EntityId("created:keep-house");
            var declaration = new FactionStanceInput(new("decision:letter-check"), FactionStanceAction.DeclareHostile,
                own, target, 0, 30 * 86400);
            var result = game.Session.ProclaimFactionStance(new(declaration,new(new("request:letter-check"),declaration.Id)),
                game.Session.PlayerControl,game.Session.Revision);
            Check(result.Code == ResultCode.Accepted,result.VisibleReason);
            long Due(string message)
            {
                var state = game.Session.DiagnosticSnapshot();
                return state.Couriers!.Missions.Single(c => state.Messaging.Attempts.Any(a => a.Id == c.Attempt &&
                    a.MessageId == message)).ReadyAt;
            }
            var original = game.Session.DiagnosticSnapshot().FactionNotices!.Requests.Single();
            game.Advance(Due(original.Message) + 2);
            var reply = game.Session.DiagnosticSnapshot().FactionNotices!.Requests.Single(r => r.Recipient == game.Session.PlayerControl);
            game.Advance(Due(reply.Message) + 1);
            game.SelectUnreadMail(new(reply.Message));
            game.Press("open");
            await Frame();
            game.Press("work-faction-map");
            game.Press("save");
            await WaitSave(game);
            var hash = ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot());
            game.Press("load");
            await Frame();
            Check(hash == ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()), "開封後の復旧");
            void Click(string text)
            {
                var button = game.FindChildren("*","Button",true,false).OfType<Button>()
                    .Single(b => b.Text == text && b.IsVisibleInTree() && !b.Disabled);
                button.EmitSignal(Button.SignalName.Pressed);
            }
            game.Press("chronicle");
            await Frame();
            Click("対外方針の書状を読み返す");
            game.Press("work-faction-truce");
            await Frame();
            Check(game.Session.TruceView().Length == 0, "検討画面を開く前に提案が作成された");
            Click("この条件を本拠へ発送");
            await Frame();
            Check(game.Session.TruceView().Any(c => c.Other == target), "通知元との不戦提案");
            game.Press("save");
            await WaitSave(game);
            var sent = ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot());
            game.Press("load");
            await Frame();
            Check(sent == ContractJson.CanonicalHash(game.Session.DiagnosticSnapshot()) &&
                game.Session.TruceView().Single().Other == target, "発送後の復旧で条件・宛先・件数が一致");
            GD.Print("DIPLOMATIC_LETTER_PASS");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PrintErr(ex); GetTree().Quit(1); }
    }
}
