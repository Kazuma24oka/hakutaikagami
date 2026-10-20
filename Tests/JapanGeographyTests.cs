using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.World;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class JapanGeographyTests
{
    static JapanCatalog Catalog()=>JapanGeography.Read(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"japan-map-v1.json")));
    [Fact] public void PinnedCatalogHasCoastAndThirtyQualifiedPublicCandidates(){var c=Catalog();Assert.Equal(109,c.Polygons.Length);Assert.Equal(30,c.Landmarks.Length);Assert.All(c.Landmarks,l=>{Assert.StartsWith("geo:Q",l.Id);Assert.StartsWith("DisplayCandidateOnly",l.HistoricalStatus);Assert.True(long.TryParse(l.SourceRevision,out _));});Assert.Equal(2,c.Sources.Length);Assert.All(c.Sources,s=>Assert.True(ContractValidation.IsHash(s.Sha256)));}
    [Theory][InlineData(418008330,1400994440)][InlineData(352170000,1368440000)][InlineData(262170306,1277194750)][InlineData(459999999,1459999999)]
    public void CanonicalCoordinatesRoundTripToOneE7Unit(int lat,int lon){var p=new GeoCoordinate(lat,lon);var q=JapanGeography.Inverse(JapanGeography.Project(p));Assert.InRange(Math.Abs(q.LatitudeE7-lat),0,1);Assert.InRange(Math.Abs(q.LongitudeE7-lon),0,1);}
    [Fact] public void MillimeterOriginSubtractionRetainsLocalDetailAtNationScale(){var c=Catalog();foreach(var l in c.Landmarks){var origin=JapanGeography.Project(l.Coordinate);var delta=JapanGeography.Relative(new(origin.EastMm+1234,origin.NorthMm-5678),origin);Assert.Equal(1.234,delta.EastMeters);Assert.Equal(-5.678,delta.NorthMeters);}}
    [Fact] public void NorthEastAndProjectionOriginAreExplicit(){Assert.Equal(new GeoMillimeters(0,0),JapanGeography.Project(new(360000000,1380000000)));var p=JapanGeography.Project(new(370000000,1390000000));Assert.InRange(p.NorthMm,111194000,111196000);Assert.InRange(p.EastMm,89958000,89960000);}
    [Fact] public void DuplicateAndUnqualifiedCandidatesFail(){var c=Catalog();Assert.Throws<InvalidDataException>(()=>JapanGeography.Read(ContractJson.Write(c with{Landmarks=[c.Landmarks[0],c.Landmarks[0]]})));Assert.Throws<InvalidDataException>(()=>JapanGeography.Read(ContractJson.Write(c with{Landmarks=[c.Landmarks[0] with{HistoricalStatus="confirmed"}]})));}
    [Fact] public void MissingSourceAndWrongProjectionFail(){var c=Catalog();Assert.Throws<InvalidDataException>(()=>JapanGeography.Read(ContractJson.Write(c with{Sources=[]})));Assert.Throws<InvalidDataException>(()=>JapanGeography.Read(ContractJson.Write(c with{Projection="legacy-cell"})));}
    [Theory][InlineData(900000001,0)][InlineData(0,1800000001)][InlineData(-900000001,0)]
    public void InvalidCoordinatesFail(int lat,int lon)=>Assert.Throws<InvalidDataException>(()=>JapanGeography.Project(new(lat,lon)));
    [Fact] public void CatalogIsNotSimulationSiteAssignment(){var c=Catalog();var w=ContractFixture.Create();Assert.DoesNotContain(w.Definitions.Sites,s=>c.Landmarks.Any(l=>l.Id==s.Id.Value));}
}
