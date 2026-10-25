using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class OfficerLibraryStoreTests
{
    static OfficerLibrary Library()=>new(1,[new("created:a",null,"検証武将",1520,new(1,2,3,4),StartTemplate.Ronin,"castle:a",null,null)],[]);
    static string Folder()=>Path.Combine(Path.GetTempPath(),"hakutai-officer-library-test",Guid.NewGuid().ToString("N"));
    [Fact]public void SaveAndReopenPreserveAllBytes(){var folder=Folder();var store=new OfficerLibraryStore(folder);var data=Library();var hash=store.Save(data,null);Assert.Equal(hash,ContractJson.CanonicalHash(new OfficerLibraryStore(folder).Load()));Assert.Equal(ContractJson.Write(data),File.ReadAllBytes(Path.Combine(folder,"officers.json")));}
    [Fact]public void StaleEditorCannotOverwriteNewerEdit(){var folder=Folder();var store=new OfficerLibraryStore(folder);string first=store.Save(Library(),null);var edit=Library() with{Groups=[new("group:a","別シナリオ",[])]};string second=store.Save(edit,first);Assert.Throws<InvalidDataException>(()=>store.Save(Library(),first));Assert.Equal(second,ContractJson.CanonicalHash(store.Load()));}
    [Fact]public void CreateCannotOverwriteAnExistingLibrary(){var folder=Folder();var store=new OfficerLibraryStore(folder);store.Save(Library(),null);Assert.Throws<InvalidDataException>(()=>store.Save(new(1,[],[]),null));Assert.Single(store.Load().Ungrouped);}
    [Fact]public void CorruptLibraryIsKeptAndNotOverwritten(){var folder=Folder();Directory.CreateDirectory(folder);var path=Path.Combine(folder,"officers.json");File.WriteAllText(path,"broken");var store=new OfficerLibraryStore(folder);Assert.ThrowsAny<Exception>(()=>store.Save(Library(),"old-hash"));Assert.Equal("broken",File.ReadAllText(path));}
    [Fact]public void ImportRejectsCollisionWithoutChangingExisting(){var a=Library();string before=ContractJson.CanonicalHash(a);Assert.Throws<InvalidDataException>(()=>OfficerLibraryStore.Import(a,Library()));Assert.Equal(before,ContractJson.CanonicalHash(a));}
    [Fact]public void ImportAddsIndependentDefinitions(){var a=Library();var b=Library();b.Ungrouped[0]=b.Ungrouped[0] with{Id="created:b"};var merged=OfficerLibraryStore.Import(a,b);merged.Ungrouped[0]=merged.Ungrouped[0] with{Name="変更"};Assert.Equal(2,merged.Ungrouped.Length);Assert.Equal("検証武将",a.Ungrouped[0].Name);}
    [Theory][InlineData("null")][InlineData("schema")][InlineData("duplicate")]
    public void InvalidLibraryNeverReplacesDisk(string kind){var folder=Folder();var store=new OfficerLibraryStore(folder);string hash=store.Save(Library(),null);var invalid=kind=="schema"?new OfficerLibrary(2,[],[]):kind=="null"?new OfficerLibrary(1,null!,[]):new OfficerLibrary(1,[Library().Ungrouped[0],Library().Ungrouped[0]],[]);Assert.Throws<InvalidDataException>(()=>store.Save(invalid,hash));Assert.Equal(hash,ContractJson.CanonicalHash(store.Load()));}
}
