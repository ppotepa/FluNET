using FluNET.Syntax;

namespace FluNet.Languages.English.Verbs
{
    internal class GetText :
        IGet<string[], Uri>,
        IGet<string[], FileInfo>
    {
        public string[] What { get; set; }
        FileInfo IGet<string[], FileInfo>.From { get; set; }
        Uri IGet<string[], Uri>.From { get; set; }
    }

    internal class GetBytes :
        IGet<byte[], Uri>,
        IGet<byte[], FileInfo>
    {
        public byte[] What { get; set; }
        FileInfo IGet<byte[], FileInfo>.From { get; set; }
        Uri IGet<byte[], Uri>.From { get; set; }
    }
}
