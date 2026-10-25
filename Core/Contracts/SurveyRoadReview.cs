using HakutaiKagami.Core.World;
namespace HakutaiKagami.Core.Contracts;
public sealed record SurveyMapPixel(int X,int Y);
public sealed record SurveyReference(string Id,string Url,string Sha256,string Role);
public sealed record SurveyRoadCandidate(string Id,string TargetEdge,int WorldYear,string CoordinateFrame,string? Datum,int? ScaleDenominator,bool EndpointsIdentified,bool AlignmentIdentified,bool HistoricalPeriodSupported,bool CoordinateControlVerified,bool IndependentCheckPassed,long? MaximumErrorMm,RoutePoint[] MetricVertices,SurveyMapPixel[] PixelVertices,SurveyReference[] Sources);
public sealed record SurveyRoadDecision(bool CanReplace,string[] Reasons,string EvidenceHash);
public sealed record SurveyRoadGeometry(RoutePoint[] Vertices,long[] SegmentLengthsMm,long TotalLengthMm,string EvidenceHash);
// Source-image pixels are not world coordinates. A designation boundary is not automatically a surveyed road centerline.
public static class SurveyRoadReview
{
    public const long MaximumAcceptedErrorMm=5000;
    public static SurveyRoadCandidate Futamura(){using var s=typeof(SurveyRoadReview).Assembly.GetManifestResourceStream("FutamuraSurveyReviewV1")??throw new InvalidDataException("Missing survey review");using var m=new MemoryStream();s.CopyTo(m);return ContractJson.Read<SurveyRoadCandidate>(m.ToArray());}
    public static SurveyRoadDecision Evaluate(SurveyRoadCandidate c,int worldYear)
    {
        var reasons=new List<string>();
        if(c.WorldYear!=worldYear)reasons.Add("world-year-mismatch");
        if(!c.HistoricalPeriodSupported)reasons.Add("target-era-alignment-unproven");
        if(!c.EndpointsIdentified||!c.AlignmentIdentified)reasons.Add("endpoints-or-centerline-unidentified");
        if(c.CoordinateFrame!=JapanGeography.Projection||c.Datum is not("JGD2011" or "WGS84"))reasons.Add("world-coordinate-reference-missing");
        if(!c.CoordinateControlVerified||!c.IndependentCheckPassed)reasons.Add("coordinate-control-not-verified");
        if(c.MaximumErrorMm is not {} error||error<0||error>MaximumAcceptedErrorMm)reasons.Add("coordinate-error-unbounded-or-too-large");
        if(c.MetricVertices.Length<2||c.MetricVertices.Zip(c.MetricVertices.Skip(1)).Any(p=>p.First==p.Second))reasons.Add("metric-centerline-missing-or-degenerate");
        if(c.Sources.Length==0||c.Sources.Select(s=>s.Id).Distinct().Count()!=c.Sources.Length||c.Sources.Any(s=>!ContractValidation.IsHash(s.Sha256)||!Uri.TryCreate(s.Url,UriKind.Absolute,out var u)||u.Scheme!="https"))reasons.Add("source-binding-invalid");
        if(!c.Sources.Any(s=>s.Role is "survey-coordinate-table" or "georeferenced-designation-centerline"))reasons.Add("coordinate-authority-missing");
        if(c.ScaleDenominator is <=0)reasons.Add("invalid-map-scale");
        return new(reasons.Count==0,reasons.ToArray(),ContractJson.CanonicalHash(c));
    }
    public static SurveyRoadGeometry Replacement(SurveyRoadCandidate c,int worldYear)
    {
        var d=Evaluate(c,worldYear);if(!d.CanReplace)throw new InvalidDataException("Survey adoption denied: "+string.Join(",",d.Reasons));
        var lengths=c.MetricVertices.Zip(c.MetricVertices.Skip(1)).Select(p=>{var dx=(double)p.Second.EastMm-p.First.EastMm;var dy=(double)p.Second.NorthMm-p.First.NorthMm;var length=Math.Sqrt(dx*dx+dy*dy);if(!double.IsFinite(length)||length<1||length>1000000000)throw new InvalidDataException("Survey segment outside supported range");return checked((long)Math.Round(length,MidpointRounding.AwayFromZero));}).ToArray();
        return new(c.MetricVertices.Select(p=>p with{}).ToArray(),lengths,checked(lengths.Sum()),d.EvidenceHash);
    }
}
