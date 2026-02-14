#if TOOLS
using Godot;

namespace LevelImporterEditorTool;

[Tool]
public partial class LevelImporter : EditorPlugin
{
    LevelImporterProcessor plugin;

    public override void _EnterTree()
    {
        plugin = new LevelImporterProcessor();
        AddScenePostImportPlugin(plugin);
        base._EnterTree();
    }

    public override void _ExitTree()
    {
        RemoveScenePostImportPlugin(plugin);
        base._ExitTree();
    }
}
#endif
