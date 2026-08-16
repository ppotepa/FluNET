using FluNET.Execution.Commands;
using FluNET.Language.Resources;
using System.Text.Json;

namespace FluNET.Language;

public sealed class SurfaceBlobStorageLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module.ResourceProvider<BlobResourceProvider>();

        module
            .Command<GetBlobCommand, string>("GETBLOB", "Text")
            .FrameId("storage.blob.get")
            .CommandId("flunet.storage.blob.get")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<GetBlobCommandBinder>()
            .HandleWith<GetBlobCommandHandler>();

        module
            .Command<PutBlobCommand, string>("PUTBLOB", "Text")
            .FrameId("storage.blob.put")
            .CommandId("flunet.storage.blob.put")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<string>(SemanticRole.Theme, "USING")
            .BindWith<PutBlobCommandBinder>()
            .HandleWith<PutBlobCommandHandler>();

        module
            .Command<DeleteBlobCommand, string>("DELETEBLOB", "Text")
            .FrameId("storage.blob.delete")
            .CommandId("flunet.storage.blob.delete")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<DeleteBlobCommandBinder>()
            .HandleWith<DeleteBlobCommandHandler>();

        module
            .Command<ListBlobCommand, JsonElement[]>("LISTBLOB", "JsonList")
            .FrameId("storage.blob.list")
            .CommandId("flunet.storage.blob.list")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<ListBlobCommandBinder>()
            .HandleWith<ListBlobCommandHandler>();
    }

    public void Register(LanguageBuilder language)
    {
        language.Module("flunet.storage.blob");
    }
}
