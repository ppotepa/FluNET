using FluNET.Execution.Commands;

namespace FluNET.Language;

public sealed class SurfaceDirectoryLanguageModule : IFluNetModule
{
    public void Register(FluNetModuleBuilder module)
    {
        module
            .Command<CreateDirectoryCommand, DirectoryInfo>("CREATEDIRECTORY", "Directory")
            .FrameId("filesystem.directory.create")
            .CommandId("flunet.filesystem.directory.create")
            .Positional<DirectoryInfo>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<CreateDirectoryCommandBinder>()
            .HandleWith<CreateDirectoryCommandHandler>();

        module
            .Command<StatPathCommand, System.Text.Json.JsonElement>("STATPATH", "Json")
            .FrameId("surface.files.stat")
            .CommandId("flunet.filesystem.stat")
            .Positional<System.Text.Json.JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<StatPathCommandBinder>()
            .HandleWith<StatPathCommandHandler>();

        module
            .Command<CopyDirectoryCommand, DirectoryInfo>("COPYDIRECTORY", "Directory")
            .FrameId("filesystem.directory.copy")
            .CommandId("flunet.filesystem.directory.copy")
            .Positional<DirectoryInfo>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<string>(SemanticRole.Goal, "TO")
            .BindWith<CopyDirectoryCommandBinder>()
            .HandleWith<CopyDirectoryCommandHandler>();

        module
            .Command<MoveDirectoryCommand, DirectoryInfo>("MOVEDIRECTORY", "Directory")
            .FrameId("filesystem.directory.move")
            .CommandId("flunet.filesystem.directory.move")
            .Positional<DirectoryInfo>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<string>(SemanticRole.Goal, "TO")
            .BindWith<MoveDirectoryCommandBinder>()
            .HandleWith<MoveDirectoryCommandHandler>();

        module
            .Command<TrashDirectoryCommand, DirectoryInfo>("TRASHDIRECTORY", "Directory")
            .FrameId("filesystem.directory.trash")
            .CommandId("flunet.filesystem.directory.trash")
            .Positional<DirectoryInfo>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .BindWith<TrashDirectoryCommandBinder>()
            .HandleWith<TrashDirectoryCommandHandler>();

        module
            .Command<RestoreFileCommand, FileInfo>("RESTOREFILE", "File")
            .FrameId("filesystem.trash.restore.file")
            .CommandId("flunet.filesystem.trash.restore.file")
            .Positional<FileInfo>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<string>(SemanticRole.Goal, "TO")
            .BindWith<RestoreFileCommandBinder>()
            .HandleWith<RestoreFileCommandHandler>();

        module
            .Command<RestoreDirectoryCommand, DirectoryInfo>("RESTOREDIRECTORY", "Directory")
            .FrameId("filesystem.trash.restore.directory")
            .CommandId("flunet.filesystem.trash.restore.directory")
            .Positional<DirectoryInfo>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM")
            .Marked<string>(SemanticRole.Goal, "TO")
            .BindWith<RestoreDirectoryCommandBinder>()
            .HandleWith<RestoreDirectoryCommandHandler>();
    }

    public void Register(LanguageBuilder language)
    {
        language.Module("flunet.filesystem.directory");
        language.Command<CreateDirectoryCommand, DirectoryInfo>("MKDIR", "Directory")
            .FrameId("filesystem.directory.create")
            .Positional<DirectoryInfo>(SemanticRole.Output, SlotDirection.Output);
        language.Command<StatPathCommand, System.Text.Json.JsonElement>("STAT", "Json")
            .FrameId("surface.files.stat")
            .Positional<System.Text.Json.JsonElement>(SemanticRole.Output, SlotDirection.Output)
            .Marked<string>(SemanticRole.Source, "FROM");
    }
}

public sealed class MakeDirectory
{
    public string Text => "MKDIR";
}

public sealed class Stat
{
    public string Text => "STAT";
}
