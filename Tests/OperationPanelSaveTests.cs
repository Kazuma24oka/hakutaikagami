using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class OperationPanelSaveTests
{
    static string Folder()=>Path.Combine(Path.GetTempPath(),"hakutai-operation-panel",Guid.NewGuid().ToString("N"));
    [Theory][InlineData("peace")][InlineData("military")][InlineData("mail")]
    public void SelectedOperationPanelBelongsToTheSameSavedGeneration(string panel)
    {
        var s=new WorldSession(ContractFixture.Create());var w=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Folder(),w.Definitions);var result=store.Save(s,"manual",w.WorldId,new(.25,10,OperationPanel:panel));var prepared=store.Prepare("manual");Assert.Equal(result.GenerationId,prepared.GenerationId);Assert.Equal(panel,prepared.Presentation.OperationPanel);Assert.Equal(.25,prepared.Presentation.DisplayFraction);Assert.Equal(10,prepared.Presentation.DisplaySpeed);
    }
    [Theory][InlineData("")][InlineData("unknown")]
    public void InvalidPanelCannotReplaceAnExistingGeneration(string panel)
    {
        var s=new WorldSession(ContractFixture.Create());var w=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Folder(),w.Definitions);var first=store.Save(s,"manual",w.WorldId,new(0,1,OperationPanel:"military"));Assert.Throws<InvalidDataException>(()=>store.Save(s,"manual",w.WorldId,new(0,1,OperationPanel:panel)));Assert.Equal(first.GenerationId,store.Prepare("manual").GenerationId);
    }
    [Fact]public void OldPresentationBytesStayOmittedAndReadable(){var bytes=System.Text.Encoding.UTF8.GetBytes("{\"DisplayFraction\":0,\"DisplaySpeed\":1}");var old=ContractJson.Read<SavePresentation>(bytes);Assert.Null(old.OperationPanel);Assert.Equal(bytes,ContractJson.Write(old));}
}
