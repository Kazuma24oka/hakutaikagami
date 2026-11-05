using System.Reflection;
namespace HakutaiKagami.Core.Contracts;
// NPCの候補実行が失敗したとき、途中の記録や支払だけを世界へ残さない。
// 通常のWorldSession.Changeと同様、成功したコピーだけを採用する。
internal static class TransactionalWorld
{
 static readonly PropertyInfo[] Properties=typeof(ContractWorld).GetProperties(BindingFlags.Instance|BindingFlags.Public).Where(p=>p.CanRead&&p.CanWrite&&p.GetIndexParameters().Length==0).ToArray();
 internal static bool TryApply(ContractWorld world,Action<ContractWorld> action)
 {
  var candidate=ContractJson.Copy(world);
  try{candidate.CommitSequence=checked(candidate.CommitSequence+1);action(candidate);}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException){return false;}
  foreach(var property in Properties)property.SetValue(world,property.GetValue(candidate));return true;
 }
}
