using FluNET.Capabilities;
using FluNET.Execution.Commands;
using FluNET.Language.Values;
using System.Text.Json;

namespace FluNET.Language;

public sealed class SurfaceLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        ArgumentNullException.ThrowIfNull(module);
        module.Language.Module("flunet.surface").Type<SecretValue>("Secret");
        module.Command<LoadJsonGlobCommand, JsonElement[]>("LOADGLOB", "JsonGlob").FrameId("surface.load.glob.json").CommandId("flunet.surface.loadglob").Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output).Marked<string>(SemanticRole.Source, "FROM").BindWith<LoadJsonGlobCommandBinder>().HandleWith<LoadJsonGlobCommandHandler>();
        module.Command<GetHttpJsonCommand, JsonElement>("GETHTTP", "Json").FrameId("surface.get.http.json").CommandId("flunet.surface.gethttp").Positional<JsonElement>(SemanticRole.Output, SlotDirection.Output).Marked<Uri>(SemanticRole.Source, "FROM").BindWith<GetHttpJsonCommandBinder>().HandleWith<GetHttpJsonCommandHandler>();
        module.Command<GetEnvironmentCommand, string>("GETENV", "Text").FrameId("surface.get.environment").CommandId("flunet.surface.getenv").Positional<string>(SemanticRole.Output, SlotDirection.Output).Marked<string>(SemanticRole.Source, "FROM").BindWith<GetEnvironmentCommandBinder>().HandleWith<GetEnvironmentCommandHandler>();
        module.Command<GetSecretCommand, SecretValue>("GETSECRET", "Secret").FrameId("surface.get.secret").CommandId("flunet.surface.getsecret").Positional<SecretValue>(SemanticRole.Output, SlotDirection.Output).Marked<string>(SemanticRole.Source, "FROM").BindWith<GetSecretCommandBinder>().HandleWith<GetSecretCommandHandler>();

        module.Command<FilterJsonCommand, JsonElement[]>("FILTERJSON", "JsonList").FrameId("surface.data.filter.json").CommandId("flunet.surface.filterjson").Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output).Marked<JsonElement[]>(SemanticRole.Source, "FROM").Marked<string>(new FrameRoleId("Predicate"), "USING").BindWith<FilterJsonCommandBinder>().HandleWith<FilterJsonCommandHandler>();
        module.Command<SortJsonCommand, JsonElement[]>("SORTJSON", "JsonList").FrameId("surface.data.sort.json").CommandId("flunet.surface.sortjson").Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output).Marked<JsonElement[]>(SemanticRole.Source, "FROM").Marked<string>(new FrameRoleId("Key"), "USING").BindWith<SortJsonCommandBinder>().HandleWith<SortJsonCommandHandler>();
        module.Command<TakeJsonCommand, JsonElement[]>("TAKEJSON", "JsonList").FrameId("surface.data.take.json").CommandId("flunet.surface.takejson").Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output).Marked<JsonElement[]>(SemanticRole.Source, "FROM").Marked<int>(new FrameRoleId("Count"), "USING").BindWith<TakeJsonCommandBinder>().HandleWith<TakeJsonCommandHandler>();
        module.Command<ProjectJsonCommand, JsonElement[]>("PROJECTJSON", "JsonList").FrameId("surface.data.project.json").CommandId("flunet.surface.projectjson").Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output).Marked<JsonElement[]>(SemanticRole.Source, "FROM").Marked<string>(new FrameRoleId("Projection"), "USING").BindWith<ProjectJsonCommandBinder>().HandleWith<ProjectJsonCommandHandler>();
        module.Command<DefaultJsonCommand, JsonElement[]>("DEFAULTJSON", "JsonList").FrameId("surface.data.default.json").CommandId("flunet.surface.defaultjson").Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output).Marked<JsonElement[]>(SemanticRole.Source, "FROM").Marked<string>(new FrameRoleId("Default"), "USING").BindWith<DefaultJsonCommandBinder>().HandleWith<DefaultJsonCommandHandler>();
        module.Command<ForEachJsonCommand, JsonElement[]>("FOREACHJSON", "JsonList").FrameId("surface.flow.foreach.json").CommandId("flunet.surface.foreachjson").Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output).Marked<JsonElement[]>(SemanticRole.Source, "FROM").Marked<string>(new FrameRoleId("Template"), "USING").BindWith<ForEachJsonCommandBinder>().HandleWith<ForEachJsonCommandHandler>();
        module.Command<GroupJsonCommand, JsonElement[]>("GROUPJSON", "JsonList").FrameId("surface.data.group.json").CommandId("flunet.surface.groupjson").Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output).Marked<JsonElement[]>(SemanticRole.Source, "FROM").Marked<string>(new FrameRoleId("Key"), "USING").BindWith<GroupJsonCommandBinder>().HandleWith<GroupJsonCommandHandler>();
        module.Command<SumJsonCommand, decimal>("SUMJSON", "Number").FrameId("surface.data.sum.json").CommandId("flunet.surface.sumjson").Positional<decimal>(SemanticRole.Output, SlotDirection.Output).Marked<JsonElement[]>(SemanticRole.Source, "FROM").Marked<string>(new FrameRoleId("Value"), "USING").BindWith<SumJsonCommandBinder>().HandleWith<SumJsonCommandHandler>();
        module.Command<JoinJsonCommand, JsonElement[]>("JOINJSON", "JsonList").FrameId("surface.data.join.json").CommandId("flunet.surface.joinjson").Positional<JsonElement[]>(SemanticRole.Output, SlotDirection.Output).Marked<JsonElement[]>(SemanticRole.Source, "FROM").Marked<JsonElement[]>(SemanticRole.Goal, "TO").Marked<string>(new FrameRoleId("Match"), "USING").BindWith<JoinJsonCommandBinder>().HandleWith<JoinJsonCommandHandler>();
        module.Conversion<JsonElement, JsonElement[], JsonToJsonListConversion>();
    }
}

public static class SurfaceLanguage
{
    public static FluNetRuntimeDefinition CreateRuntime() => new FluNetModuleBuilder().AddModule(new StandardLanguageModule()).AddModule(new SurfaceLanguageModule()).Build();
}
