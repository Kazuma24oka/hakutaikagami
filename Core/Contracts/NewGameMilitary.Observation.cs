namespace HakutaiKagami.Core.Contracts;
public sealed record MilitaryObservationFrame(long At,long Revision,MilitarySeenUnit[] Units);
public sealed record MilitaryObservationState(string Contract,EntityId Observer,MilitaryObservationFrame[] Frames);
public static partial class NewGameMilitary
{
    public const string ObservationContract="actor-direct-observation-dated-unit-memory-physical-report-v1";
    static NewMilitaryRequest? ObservationStart(ContractWorld w)=>w.NewMilitary?.Requests.FirstOrDefault(q=>q.Input.ObservationPolicy==ObservationContract);
    internal static void RecordMilitaryObservation(ContractWorld w)
    {
        if(ObservationStart(w)==null)return;
        var s=w.NewMilitary!;
        var observed=s.Observation??new(ObservationContract,w.PlayerControl,[]);
        Need(observed.Observer==w.PlayerControl,"他人の観測記録を本人の記憶へ移せません");
        var frame=new MilitaryObservationFrame(w.ElapsedSeconds,w.CommitSequence,DirectMilitarySamples(w));
        Need(!observed.Frames.Any(f=>f.Revision==frame.Revision),"同じ状態確定で観測を重ねません");
        w.NewMilitary=s with{Observation=observed with{Frames=[..observed.Frames,frame]}};
    }
    static MilitaryObservationState? ObservationBefore(NewMilitaryState s,long at,long revision)
    {
        if(!s.Requests.Any(q=>q.Input.ObservationPolicy==ObservationContract&&Before(q.At,q.Revision,at,revision)))return null;
        return s.Observation is {} o?o with{Frames=o.Frames.Where(f=>Before(f.At,f.Revision,at,revision)).ToArray()}:null;
    }
    static void ValidateMilitaryObservation(ContractWorld w,ContractWorld? trustedPrior,bool currentLedger=true)
    {
        var start=ObservationStart(w);var state=w.NewMilitary?.Observation;
        Need((start!=null)==(state!=null),"観測開始の原判断と記録が一致しません");if(state==null)return;
        Need(state.Contract==ObservationContract&&state.Observer==w.PlayerControl&&state.Frames.Length>0,"観測契約・本人・記録が不正です");
        Need(state.Frames.Select(f=>f.Revision).Distinct().Count()==state.Frames.Length&&state.Frames.Select((f,i)=>f.Revision>=start!.Revision&&f.At>=start.At&&f.Revision<=w.CommitSequence&&f.At<=w.ElapsedSeconds&&(i==0||f.Revision>state.Frames[i-1].Revision&&f.At>=state.Frames[i-1].At)).All(x=>x),"観測の時点が重複・逆順です");
        var tail=state.Frames[^1];if(currentLedger)Need(tail.At==w.ElapsedSeconds&&tail.Revision==w.CommitSequence&&Same(tail.Units,DirectMilitarySamples(w)),"現在の本人視認と保存記録が違います");
        int first=0;
        if(trustedPrior?.NewMilitary?.Observation is {} prior){
            Need(state.Frames.Length>=prior.Frames.Length&&Same(prior.Frames,state.Frames.Take(prior.Frames.Length).ToArray()),"既知の観測記憶を変更・削除できません");first=prior.Frames.Length;
        }
        // Full loads have no trusted prior. Each frame must be justified by the world history at its own clock/revision.
        foreach(var f in state.Frames.Skip(first)){
            var historical=BeforeState(w,f.At,f.Revision,observationOnly:true);
            Need(Same(f.Units,DirectMilitarySamples(historical)),"実際に観測した部隊情報を再現できません");
        }
        if(trustedPrior==null){
            foreach(var q in w.NewMilitary!.Requests.Where(q=>q.Revision>=start!.Revision))Need(state.Frames.Any(f=>f.At==q.At&&f.Revision==q.Revision),"軍務判断後の目撃記録が欠落しています");
            foreach(var sortie in w.NewMilitary.Sorties){
                long a=Math.Max(sortie.From,start!.At),b=sortie.Until??w.ElapsedSeconds;
                if(a>b)continue;
                long seconds=state.Frames.Where(f=>f.At>=a&&f.At<=b).Select(f=>f.At).Distinct().LongCount();
                Need(seconds==checked(b-a+1),"野外行動中の観測時刻が欠落しています");
            }
        }
    }
}
