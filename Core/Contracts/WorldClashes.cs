namespace HakutaiKagami.Core.Contracts;

public sealed record WorldClashStep(EntityId Army,long From,long At,long Revision,EngagementResult Result);
public sealed record WorldClashState(int Schema,string Contract,WorldClashStep[] Steps);
// Local player-field encounter stage. World truth is not exposed as the observer's knowledge.
public static partial class WorldClashes
{
    public const string Policy="actual-owned-armies-player-field-clock-steps-simultaneous-effects-v1";
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.WorldArmyPolicy is NpcArmies.CombatPolicy or NpcArmies.CompactCombatPolicy;
    static void Need(bool b,string why){if(!b)throw new InvalidDataException(why);}
    public static RegionalLoss[] Losses(ContractWorld w,long at)=>w.WorldClashes?.Steps.Where(s=>s.At<=at).SelectMany(s=>s.Result.Losses).ToArray()??[];
    public static bool AttacksNow(ContractWorld w,EntityId unit)=>Enabled(w)&&w.WorldClashes?.Steps.LastOrDefault() is {} step&&step.At==w.ElapsedSeconds&&step.Result.Strikes.Any(h=>h.Attacker==unit);
    public static int SoldiersLost(ContractWorld w,EntityId unit,long at)=>Losses(w,at).Where(l=>l.Unit==unit).Sum(l=>l.Soldiers);
    public static int MoraleLost(ContractWorld w,EntityId unit,long at)=>Losses(w,at).Where(l=>l.Unit==unit).Sum(l=>l.Morale);
    internal static void Tick(ContractWorld w,long from)
    {
        if(!Enabled(w)||!NewGameMilitary.DeployedAt(w,from)||w.NewMilitary==null)return;
        Need(w.ElapsedSeconds==from+1,"現地接触は既存の毎秒軍務境界で実行します");
        var state=w.WorldClashes??throw new InvalidDataException("実戦履歴がありません");
        Need(!state.Steps.Any(s=>s.At==w.ElapsedSeconds),"接触境界を二重に適用できません");
        var result=WorldEngagement.Resolve(w);if(Sparse(w)&&result.Strikes.Length==0)return;
        w.WorldClashes=state with{Steps=[..state.Steps,new(w.NewMilitary.Terms.Army,from,w.ElapsedSeconds,w.CommitSequence,result)]};
    }
    static ContractWorld Before(ContractWorld w,WorldClashStep step,int count)
    {
        long revision=Sparse(w)?step.Revision:step.Revision-1;
        var c=ContractJson.Copy(w);c.WorldClashes=c.WorldClashes! with{Steps=c.WorldClashes.Steps.Take(count).ToArray()};
        c.NpcArmies=c.NpcArmies! with{
            Musters=c.NpcArmies.Musters.Where(m=>m.At<step.At||m.At==step.At&&m.Revision<=revision).ToArray(),
            Marches=c.NpcArmies.Marches.Where(m=>m.At<step.At||m.At==step.At&&m.Revision<=revision).ToArray(),
            Returns=c.NpcArmies.Returns.Where(m=>m.At<step.At||m.At==step.At&&m.Revision<=revision).ToArray()};
        c=NewGameMilitary.WorldEngagementBefore(c,step.Army,step.At,revision);NpcArmies.Sync(c);return c;
    }
    public static void Validate(ContractWorld w,ContractWorld? trustedPrior=null)
    {
        Need(Enabled(w)==(w.WorldClashes!=null),"実在軍の明示版と接触履歴が一致しません");if(w.WorldClashes is not {} s)return;
        Need(s.Schema==1&&s.Contract==(Sparse(w)?SparsePolicy:Policy),"未知の実戦履歴です");if(Sparse(w)){ValidateSparse(w,trustedPrior);return;}
        var sorties=NewGameMilitary.LedgerStates(w).SelectMany(l=>l.Sorties.Select(q=>(Army:l.Terms.Army,Sortie:q))).ToArray();
        Need(s.Steps.LongLength==sorties.Sum(q=>(q.Sortie.Until??w.ElapsedSeconds)-q.Sortie.From),"実出陣中の接触境界が欠落・重複しています");
        for(int n=0;n<s.Steps.Length;n++){
            var step=s.Steps[n];Need(step.At==step.From+1&&step.At<=w.ElapsedSeconds&&step.Revision>0&&step.Revision<=w.CommitSequence&&step.Result.At==step.At&&
                (n==0||step.At>s.Steps[n-1].At&&step.Revision>s.Steps[n-1].Revision)&&sorties.Any(q=>q.Army==step.Army&&q.Sortie.From<=step.From&&step.At<=(q.Sortie.Until??w.ElapsedSeconds)),"接触境界の時刻・軍・順序が不正です");
            if(trustedPrior?.WorldClashes is {} old&&n<old.Steps.Length&&ContractJson.CanonicalHash(old.Steps[n])==ContractJson.CanonicalHash(step))continue;
            var prior=Before(w,step,n);var result=WorldEngagement.Resolve(prior);
            Need(ContractJson.CanonicalHash(step.Result)==ContractJson.CanonicalHash(result),"実際の所有軍・位置・受領原本から損害を再現できません");
        }
    }
}
