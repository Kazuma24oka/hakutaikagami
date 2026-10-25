using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed partial class UnitDirectiveCancelRetryTests
{
 sealed class RetrySaveFolder:IDisposable
 {
  readonly string parent=Path.GetFullPath(Path.GetTempPath());public string Root{get;}=Path.Combine(Path.GetTempPath(),"hakutaikagami-cancel-retry-"+Guid.NewGuid().ToString("N"));
  public RetrySaveFolder()=>Directory.CreateDirectory(Root);
  public void Dispose(){var path=Path.GetFullPath(Root);if(!path.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(path).StartsWith("hakutaikagami-cancel-retry-",StringComparison.Ordinal))throw new InvalidOperationException("Unsafe own test folder");if(Directory.Exists(path))Directory.Delete(path,true);}
 }
 [Fact]public void SelectedCancellationIsSavedWithSameWorldGenerationAndOldJsonRemainsReadable(){using var f=new RetrySaveFolder();var s=Away();var cancel=MissedCancel(s);var w=s.DiagnosticSnapshot();var command=w.NewMilitary!.UnitDirective!.Dispatches.First();var store=new GenerationSaveStore(f.Root,w.Definitions);var result=store.Save(s,"manual",w.WorldId,new SavePresentation(0,1,MilitaryTarget:cancel.Recipient.Value,MilitaryOriginal:command.Id.Value,MilitaryCancellation:cancel.Id.Value));Assert.True(result.Published);var candidate=store.Prepare("manual");Assert.Equal(s.Revision,result.CapturedRevision);Assert.Equal(result.GenerationId,candidate.GenerationId);Assert.Equal(cancel.Id.Value,candidate.Presentation.MilitaryCancellation);Assert.Equal(command.Id.Value,candidate.Presentation.MilitaryOriginal);var host=new WorldSessionHost(new WorldSession(w));Assert.Equal(ResultCode.Accepted,host.CommitLoad(candidate,host.Current,host.Current.Revision));Assert.Contains(NewGameMilitary.CancellationOriginalsFor(host.Current.DiagnosticSnapshot(),command.Id),id=>id==cancel.Id);Assert.DoesNotContain("MilitaryCancellation",System.Text.Encoding.UTF8.GetString(ContractJson.Write(new SavePresentation(0,1))));var old=ContractJson.Read<SavePresentation>(System.Text.Encoding.UTF8.GetBytes("{\"DisplayFraction\":0,\"DisplaySpeed\":1}"));Assert.Null(old.MilitaryCancellation);}
 [Theory][InlineData("")][InlineData("not-an-id")][InlineData("request:")]public void InvalidCancellationSelectionCannotBePublished(string id){using var f=new RetrySaveFolder();var s=new WorldSession(ContractFixture.Create());var w=s.DiagnosticSnapshot();var store=new GenerationSaveStore(f.Root,w.Definitions);Assert.Throws<InvalidDataException>(()=>store.Save(s,"manual",w.WorldId,new SavePresentation(0,1,MilitaryCancellation:id)));}
}
