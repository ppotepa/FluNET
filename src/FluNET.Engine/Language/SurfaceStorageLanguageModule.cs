using FluNET.Execution.Commands;
using System.Text.Json;

namespace FluNET.Language;

public sealed class SurfaceStorageLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module
            .Command<PutValueCommand, string>("PUTVALUE", "Text")
            .FrameId("storage.put.value")
            .CommandId("flunet.storage.put")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<string>(SemanticRole.Theme, "USING")
            .BindWith<PutValueCommandBinder>()
            .HandleWith<PutValueCommandHandler>();

        module
            .Command<ReadValueCommand, string>("READVALUE", "Text")
            .FrameId("storage.read.value")
            .CommandId("flunet.storage.read")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<ReadValueCommandBinder>()
            .HandleWith<ReadValueCommandHandler>();

        module
            .Command<ListValuesCommand, JsonElement[]>("LISTVALUES", "JsonList")
            .FrameId("storage.list.values")
            .CommandId("flunet.storage.list")
            .Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<ListValuesCommandBinder>()
            .HandleWith<ListValuesCommandHandler>();

        module
            .Command<DeleteValueCommand, string>("DELETEVALUE", "Text")
            .FrameId("storage.delete.value")
            .CommandId("flunet.storage.delete")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<DeleteValueCommandBinder>()
            .HandleWith<DeleteValueCommandHandler>();
    }

    public void Register(LanguageBuilder language)
    {
        language.Module("flunet.storage");
        language.Command<PutValueCommand, string>("STORE", "Text")
            .FrameId("storage.put.value")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output);
        language.Command<ReadValueCommand, string>("READ", "Text")
            .FrameId("storage.read.value")
            .Positional<string>(SemanticRole.Output, SlotDirection.Output);
    }
}

public sealed class StoreValue
{
    public string Text => "STORE";
}

public sealed class ReadValue
{
    public string Text => "READ";
}
