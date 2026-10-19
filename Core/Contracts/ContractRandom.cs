using System.Buffers.Binary;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

public static class ContractRandom
{
    public static ulong InitialState(ulong seed,string purpose)
    {
        if(string.IsNullOrWhiteSpace(purpose))throw new ArgumentException("Random purpose is required.");
        var text=Encoding.UTF8.GetBytes(purpose);var bytes=new byte[checked(12+text.Length)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes,seed);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8),checked((uint)text.Length));
        text.CopyTo(bytes,12);
        return BinaryPrimitives.ReadUInt64LittleEndian(System.Security.Cryptography.SHA256.HashData(bytes));
    }
}
