#if TOOLS
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

namespace LevelImporterEditorTool;
/// <summary>
/// Prepares the scene as a playable level upon import into Godot, when attached as the Post Import script in the scene import settings.
/// <br/>
///
/// Objects in the scene are replaced by PackedScenes if the object's name contains the complete PackedScene name. This ignores the PackedScene file extension.
/// Eligible PackedScenes must be a child of the configured directory.
/// <br/>
/// Materials are assigned based on matching names. This ignores the Material file extension.
/// Eligible Materials must be a child of the configured directory.
/// <br/>
/// Static collision trimeshes are generated for all meshes not replaced by PackedScenes.
/// <br/>
/// Lightmaps are generated for the static meshes based on configuration.
/// </summary>
[Tool]
public partial class LevelImporterProcessor : EditorScenePostImportPlugin
{
    // Investigate ProjectSettings for future config https://docs.godotengine.org/en/stable/classes/class_projectsettings.html
    const string levelNameIndicator = "Level";
    const char engineAttributeIndicator = '='; // The indicator preceding a special import attribute within the names of objects.
    const string noCollisionAttribute = "NoCol"; // No collision features should be enabled on objects with this attribute.
    const string convexCollisionAttribute = "ConvexCol"; // Generate simpler convex collider based on the object's mesh.
    const string collisionLayerAttribute = "Col"; // Collision layer assignment (bits).
    const string collisionMaskLayerAttribute = "ColMask"; // Collision mask layer assignment (bits).
    const string collisionOnlyAttribute = "OnlyCol"; // Mesh copied as collider mesh and original mesh removed from rendering.
    const string noLightBakeAttribute = "NoBake"; // Disable from being included in static GI baking.
    const char valueAttributeStart = '{'; // Start of value for an attribute.
    const char valueAttributeEnd = '}'; // End of value for an attribute.
    const string materialPath = "Assets/Level/Material/"; // Location of Materials to replace those in the import based on matching names.
    const string replaceablePath = "Game/LevelPackedScene/";  // Location of PackedScenes to replace MeshInstances in the import based on matching names.
    const float texelSize = 0.2f * 5f; // Texel size in project settings * arbitrary multiplier.

    public override void _PostProcess(Node scene)
    {
        var isLevel = scene.Name.ToString().Contains(levelNameIndicator);
        if (!isLevel)
        {
            return;
        }
        ConvertToGameScene(scene);
        GD.Print($"Import processed {scene.Name} as a level.");
    }

    static void ConvertToGameScene(Node scene)
    {
        var nodes = scene.GetChildren();
        var staticNodes = nodes.Where(n =>
            !GetReplaceablePackages().Any(s =>
                ((string)n.Name).Contains(s.name, System.StringComparison.CurrentCultureIgnoreCase)));
        staticNodes.ToList().ForEach(n =>
        {
            AddStaticCollisionMeshes(scene, n);
            RemoveMeshIfColliderOnly(n);
            EnableLightmapUvs(n);
            ReplaceMaterials(n);
        });
        var replaceableNodes = nodes.Except(staticNodes);
        replaceableNodes.ToList().ForEach(n =>
        {
            ReplaceWithPackageScenes(scene, n);
        });
    }

    static void AddStaticCollisionMeshes(Node scene, Node node)
    {
        var noColAttr = $"{engineAttributeIndicator}{noCollisionAttribute}";
        var convexColAttr = $"{engineAttributeIndicator}{convexCollisionAttribute}";
        var valueStart = Regex.Escape($"{valueAttributeStart}");
        var valueEnd = Regex.Escape($"{valueAttributeEnd}");
        var colLayerAttr = $"{engineAttributeIndicator}{collisionLayerAttribute}{valueStart}[0-9]+{valueEnd}";
        var colMaskAttr = $"{engineAttributeIndicator}{collisionMaskLayerAttribute}{valueStart}[0-9]+{valueEnd}";

        var nodesWithCollision = NodeUtility.GetAllChildrenWithSelf(node)
            .Where(c => c is MeshInstance3D)
            .Where(c => !c.Name.ToString().Contains(noColAttr))
            .Select(c => c as MeshInstance3D)
            .ToList();
        nodesWithCollision.ForEach(m =>
        {
            var isConcave = !m.Name.ToString().Contains(convexColAttr);
            var col = isConcave
                ? CreateConcaveCollisionShape(m)
                : CreateConvexCollisionShape(m);
            var body = new StaticBody3D();
            body.AddChild(col);
            node.AddChild(body);
            body.Owner = scene;
            col.Owner = scene;

            var colLayerSpecified = Regex.Match(m.Name, colLayerAttr);
            if (colLayerSpecified.Success)
            {
                var values = Regex.Split(colLayerSpecified.Captures.First().Value, $"[{valueStart}{valueEnd}]");
                var parsed = uint.TryParse(values.Skip(1).FirstOrDefault(), out uint layer);
                body.CollisionLayer = parsed ? layer : 0;
            }
            var colMaskSpecified = Regex.Match(m.Name, colMaskAttr);
            if (colMaskSpecified.Success)
            {
                var values = Regex.Split(colMaskSpecified.Captures.First().Value, $"[{valueStart}{valueEnd}]");
                var parsed = uint.TryParse(values.Skip(1).FirstOrDefault(), out uint layer);
                body.CollisionMask = parsed ? layer : 0;
            }
        });
    }

    static void RemoveMeshIfColliderOnly(Node node)
    {
        var colOnlyAttr = $"{engineAttributeIndicator}{collisionOnlyAttribute}";

        var nodes = NodeUtility.GetAllChildrenWithSelf(node);
        var colOnlyNodes = nodes
            .Where(c => c is MeshInstance3D)
            .Where(c => c.Name.ToString().Contains(colOnlyAttr))
            .Select(c => c as MeshInstance3D)
            .ToList();
        // Better would be remove the entire visual node instead of just clearing the mesh. Similar issue to making Replacement swap-outs.
        colOnlyNodes.ForEach(m => m.Mesh = null);
    }

    static CollisionShape3D CreateConcaveCollisionShape(MeshInstance3D mesh)
    {
        var trimesh = new ConcavePolygonShape3D();
        trimesh.SetFaces(mesh.Mesh.GetFaces());
        var col = new CollisionShape3D()
        {
            Shape = trimesh
        };
        return col;
    }

    static CollisionShape3D CreateConvexCollisionShape(MeshInstance3D mesh) => new()
    {
        Shape = mesh.Mesh.CreateConvexShape()
    };

    static void EnableLightmapUvs(Node node)
    {
        var noLightBakeAttr = $"{engineAttributeIndicator}{noLightBakeAttribute}";

        var meshes = NodeUtility.GetAllChildrenWithSelf(node)
            .Where(c => c is MeshInstance3D)
            .Select(c => c as MeshInstance3D)
            .Where(c => c.Mesh != null)
            .ToList();
        meshes.ForEach(m =>
        {
            var oldMesh = m.Mesh;
            var newMesh = new ArrayMesh();
            foreach (var surfaceId in Enumerable.Range(0, oldMesh.GetSurfaceCount()))
            {
                newMesh.AddSurfaceFromArrays(
                    Mesh.PrimitiveType.Triangles,
                    oldMesh.SurfaceGetArrays(surfaceId)
                );
                var material = oldMesh.SurfaceGetMaterial(surfaceId);
                newMesh.SurfaceSetMaterial(surfaceId, material);
            }
            newMesh.LightmapUnwrap(m.Transform, texelSize);
            m.Mesh = newMesh;
            m.CastShadow = GeometryInstance3D.ShadowCastingSetting.DoubleSided;
        });
        var nonLightmapMeshes = meshes
            .Where(c => c.Name.ToString().Contains(noLightBakeAttr))
            .ToList();
        nonLightmapMeshes.ForEach(m => m.GIMode = GeometryInstance3D.GIModeEnum.Dynamic);
    }

    static void ReplaceMaterials(Node node)
    {
        var meshes = NodeUtility.GetAllChildrenWithSelf(node)
            .Where(c => c is MeshInstance3D)
            .Select(c => c as MeshInstance3D)
            .Where(c => c.Mesh != null)
            .ToList();
        meshes.ForEach(m =>
        {
            var surfaceCount = m.Mesh.GetSurfaceCount();
            var surfaces = Enumerable.Range(0, surfaceCount)
                .Select(s => m.Mesh.SurfaceGetMaterial(s))
                .ToList();
            surfaces.ForEach(s =>
            {
                var replacement = GetMaterialReplacement(s.ResourceName);
                m.Mesh.SurfaceSetMaterial(surfaces.IndexOf(s), replacement);
            });
        });
    }

    static void ReplaceWithPackageScenes(Node scene, Node node)
    {
        var replacement = GetPackageReplacement(node.Name);
        // Adding replacement as a child of the original node instead of replacing it entirely is not optimal. Modifying transforms during post-import is currently unknown.
        node.GetChildren().ToList().ForEach(c => c.QueueFree());
        (node as MeshInstance3D).Mesh = null;
        node.AddChild(replacement);
        replacement.Owner = scene;
    }

    static Material GetMaterialReplacement(string material)
    {
        var replacements = GetReplaceableMaterials()
            .Where(n => material.Contains(n.name))
            .OrderByDescending(n => n.name.Length)
            .ToList();
        var replacement = replacements.Count > 0
            ? ResourceLoader.Load<Material>($"{materialPath}{replacements.First().file}")
            : new Material();
        return replacement;
    }

    static (string name, string file)[] GetReplaceableMaterials() => DirAccess.Open(materialPath)
        .GetFiles()
        .Where(n => n.Contains(".tres"))
        .Select(n => (n.Split('.')[0], n)).ToArray();

    static Node3D GetPackageReplacement(string original)
    {
        var replacements = GetReplaceablePackages()
            .Where(n => original.Contains(n.name))
            .OrderByDescending(n => n.name.Length)
            .ToList();
        var replacement = replacements.Count > 0
            ? ResourceLoader.Load<PackedScene>($"{replaceablePath}{replacements.First().file}").Instantiate() as Node3D
            : new();
        replacement.Name = $"{original} (replaced)";
        return replacement;
    }

    static (string name, string file)[] GetReplaceablePackages() => DirAccess.Open(replaceablePath)
        .GetFiles()
        .Where(n => !n.Contains(".import"))
        .Select(n => (n.Split('.')[0], n)).ToArray();
}

static class NodeUtility
{
    /// <summary>
    /// Retrieve all children of the node, recursively.
    /// </summary>
    /// <param name="node">The root node to retrieve children for.</param>
    /// <returns>Array of Node children.</returns>
    public static Node[] GetAllChildren(Node node)
    {
        var children = node.GetChildren(true);
        var childrenDeep = children.SelectMany(GetAllChildren).ToArray();
        return children.Concat(childrenDeep).ToArray();
    }

    /// <summary>
    /// Retrieve the node and all children of the node, recursively.
    /// </summary>
    /// <param name="node">The root node to retrieve.</param>
    /// <returns>Array of node children as well as the input node.</returns>
    public static Node[] GetAllChildrenWithSelf(Node node)
    {
        var hierarchy = new List<Node>() { node };
        var children = GetAllChildren(node);
        hierarchy.AddRange(children);
        return hierarchy.ToArray();
    }
}
#endif