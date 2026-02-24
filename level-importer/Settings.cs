
namespace LevelImporterEditorTool;

public partial class Settings
{
    // Investigate ProjectSettings for future config https://docs.godotengine.org/en/stable/classes/class_projectsettings.html
    public const string levelNameIndicator = "Level";
    public const char engineAttributeIndicator = '='; // The indicator preceding a special import attribute within the names of objects.
    public const string noCollisionAttribute = "NoCol"; // No collision features should be enabled on objects with this attribute.
    public const string convexCollisionAttribute = "ConvexCol"; // Generate simpler convex collider based on the object's mesh.
    public const string collisionLayerAttribute = "Col"; // Collision layer assignment (bits).
    public const string collisionMaskLayerAttribute = "ColMask"; // Collision mask layer assignment (bits).
    public const string noRenderAttribute = "NoRender"; // Mesh copied as collider mesh and original mesh removed from rendering.
    public const string renderLayerAttribute = "Layer"; // Render layer (bits).
    public const string noLightBakeAttribute = "NoBake"; // Disable from being included in static GI baking.
    public const string texelMultiplierAttribute = "Texel"; // Specify multiplier to texel size.
    public const string noShadowAttribute = "NoShadow"; // Disable casting shadows.
    public const char valueAttributeStart = '{'; // Start of value for an attribute.
    public const char valueAttributeEnd = '}'; // End of value for an attribute.
    public const string materialPath = "Assets/Level/Material/"; // Location of Materials to replace those in the import based on matching names.
    public const string replaceablePath = "Game/LevelPackedScene/";  // Location of PackedScenes to replace MeshInstances in the import based on matching names.
    public const float texelSize = 0.2f; // Texel size from project settings.
}
