using System;
using System.Linq;
using Godot;
using HakutaiKagami.Core.Contracts;

public partial class NewGameRuntime
{
    void ShowSiege() => RenderSiege("");
    void RenderSiege(string reply)
    {
        if (IsSaving || IsClockControlling) return;
        var (box, rev) = SocialWindow("攻城・海上補給");
        worksReviewButtons.Clear();
        var world = Snapshot();
        var actor = world.PlayerControl;
        if (reply.Length > 0) SocialText(box, reply);
        void Choice(string key, string label, Func<CommandResult> command)
        {
            var button = new Button { Text = label };
            box.AddChild(button);
            worksReviewButtons[key] = button;
            button.Pressed += () =>
            {
                if (IsSaving || IsClockControlling) return;
                CancelAdvancePrefetch();
                var result = command();
                Refresh();
                RenderSiege(result.VisibleReason);
            };
        }
        var army = ArmyRoadMarch.View(world);
        if (army is { Marching: false, Assembly: {} site } && world.NewMilitary is {} duty &&
            world.NewGame!.Start.Castles.Any(c => c.Id == site.Value && c.Faction != null && c.Faction != duty.Terms.Owner.Value) &&
            MaterialMarkets.Sites(world).Contains(site))
        {
            SocialText(box, NewGameWorld.SiteName(site.Value));
            var begin = CastleSiege.Active(world, site, world.ElapsedSeconds, rev + 1);
            var rows = world.Sieges?.Orders.Where(q => begin != null && (q.Input.Id == begin.Input.Id || q.Input.Original == begin.Input.Id) && q.Actor == actor).ToArray() ?? [];
            void Order(SiegeAction action, string label, CastleSection? section = null)
            {
                var input = new SiegeInput(new("request:siege-ui/" + rev + "/" + action), action, site, begin?.Input.Id, section);
                Choice("siege-" + action, label, () => Session.ManageSiege(input, rev));
            }
            if (begin == null) Order(SiegeAction.Begin, "部隊を展開して城を囲む");
            else if (rows.LastOrDefault() is {} last)
            {
                SocialText(box, last.Outcome);
                if (last.Input.Action != SiegeAction.Assault)
                {
                    if (rows.Any(q => q.Outcome == "城兵の助命と退去に合意"))
                    {
                        if (last.Input.Action == SiegeAction.DeliverGate) Order(SiegeAction.DeliverKeep, "本丸の引渡しに立ち会う");
                        else Order(SiegeAction.DeliverGate, "城門の引渡しに立ち会う");
                    }
                    else
                    {
                        var section = (CastleSection)rows.Count(q => q.Outcome == "区画を確保");
                        Order(SiegeAction.Assault, CastleWorks.Name(section) + "を攻める", section);
                        Order(SiegeAction.Parley, "助命と退去を条件に開城を求める");
                    }
                }
                Order(SiegeAction.Withdraw, "攻囲を解く");
            }
        }
        if (SocialWorld.Site(world, actor) is {} port && MaterialMarkets.Sites(world).Contains(port))
        {
            var ships = PublicWorks.Ships(world).Where(ship => ship.Port == port && ship.Maintained &&
                PublicWorks.KnownProjects(world, actor).Any(p => p.Owner == ship.Owner && p.Input.Site == port));
            foreach (var ship in ships)
            foreach (var target in SiegeFleet.Ports(world, port))
            {
                var home = world.NewGame!.Start.Castles.Single(c => c.Id == target.Value).Faction == ship.Owner.Value;
                if (home != (ship.Kind == WorksKind.TradeShip)) continue;
                if (!home && !FactionStances.Known(world, actor).Any(q => q.FromOwner == ship.Owner &&
                    q.ToOwner.Value == world.NewGame.Start.Castles.Single(c => c.Id == target.Value).Faction &&
                    q.EffectiveFrom <= world.ElapsedSeconds && world.ElapsedSeconds < q.Until)) continue;
                var action = home ? FleetAction.SendSupply : FleetAction.StationWarship;
                var input = new FleetInput(new("request:fleet-ui/" + rev), action, ship.Id, port, target, home ? 15 : 0);
                var text = NewGameWorld.SiteName(target.Value) + (home ? "へ兵糧15kgを送る" : "へ軍船を派遣する");
                Choice("fleet-" + ship.Id.Value.Replace(':','-') + "-" + target.Value.Replace(':','-'), text, () => Session.ManageSiegeFleet(input, rev));
            }
        }
        if (worksReviewButtons.Count == 0) SocialText(box, "攻城には現地の部隊、船の派遣には出港地で保守した船が必要です。");
    }
}