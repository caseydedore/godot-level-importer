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
    public override void _PostProcess(Node scene)
    {
        var isLevel = scene.Name.ToString().Contains(Settings.levelNameIndicator);
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
            RemoveMeshIfNoRender(n);
            EnableLightmapUvs(n);
            UpdateShadowMode(n);
            UpdateRenderLayer(n);
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
        var noColAttr = $"{Settings.engineAttributeIndicator}{Settings.noCollisionAttribute}";
        var convexColAttr = $"{Settings.engineAttributeIndicator}{Settings.convexCollisionAttribute}";
        var valueStart = Regex.Escape($"{Settings.valueAttributeStart}");
        var valueEnd = Regex.Escape($"{Settings.valueAttributeEnd}");
        var colLayerAttr = $"{Settings.engineAttributeIndicator}{Settings.collisionLayerAttribute}{valueStart}[0-9]+{valueEnd}";
        var colMaskAttr = $"{Settings.engineAttributeIndicator}{Settings.collisionMaskLayerAttribute}{valueStart}[0-9]+{valueEnd}";

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

    static void RemoveMeshIfNoRender(Node node)
    {
        var noRenderAttr = $"{Settings.engineAttributeIndicator}{Settings.noRenderAttribute}";

        var nodes = NodeUtility.GetAllChildrenWithSelf(node);
        var colOnlyNodes = nodes
            .Where(c => c is MeshInstance3D)
            .Where(c => c.Name.ToString().Contains(noRenderAttr))
            .Select(c => c as MeshInstance3D)
            .ToList();
        colOnlyNodes.ForEach(m =>
        {
            m.Mesh = null; // Better would be remove the entire visual node instead of the mesh. Shared issue with the Replacement swap-outs.
            m.IgnoreOcclusionCulling = true;
        });
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
        var noLightBakeAttr = $"{Settings.engineAttributeIndicator}{Settings.noLightBakeAttribute}";
        var valueStart = Regex.Escape($"{Settings.valueAttributeStart}");
        var valueEnd = Regex.Escape($"{Settings.valueAttributeEnd}");
        var texelMultAttr = $"{Settings.engineAttributeIndicator}{Settings.texelMultiplierAttribute}{valueStart}[0-9]+{valueEnd}";

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
            var texelMultSpecified = Regex.Match(m.Name, texelMultAttr);
            float GetTexelMultiplier()
            {
                var values = Regex.Split(texelMultSpecified.Captures.First().Value, $"[{valueStart}{valueEnd}]");
                var parsed = float.TryParse(values.Skip(1).FirstOrDefault(), out float layer);
                return parsed ? layer : 1;
            }
            var texelMultiplier = texelMultSpecified.Success ? GetTexelMultiplier() : 1;
            newMesh.LightmapUnwrap(m.Transform, Settings.texelSize * texelMultiplier);
            m.Mesh = newMesh;
        });
        var nonLightmapMeshes = meshes
            .Where(c => c.Name.ToString().Contains(noLightBakeAttr))
            .ToList();
        nonLightmapMeshes.ForEach(m => m.GIMode = GeometryInstance3D.GIModeEnum.Dynamic);
    }

    static void UpdateShadowMode(Node node)
    {
        var noShadowAttr = $"{Settings.engineAttributeIndicator}{Settings.noShadowAttribute}";
        var meshes = NodeUtility.GetAllChildrenWithSelf(node)
            .Where(c => c is MeshInstance3D)
            .Select(c => c as MeshInstance3D)
            .Where(c => c.Mesh != null);
        var shadowCasters = meshes.Where(m => !m.Name.ToString().Contains(noShadowAttr)).ToList();
        var nonCasters = meshes.Except(shadowCasters).ToList();
        shadowCasters.ForEach(m => m.CastShadow = GeometryInstance3D.ShadowCastingSetting.DoubleSided);
        nonCasters.ForEach(m => m.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off);
    }

    static void UpdateRenderLayer(Node node)
    {
        var valueStart = Regex.Escape($"{Settings.valueAttributeStart}");
        var valueEnd = Regex.Escape($"{Settings.valueAttributeEnd}");
        var layerAttr = $"{Settings.engineAttributeIndicator}{Settings.renderLayerAttribute}{valueStart}[0-9]+{valueEnd}";
        var meshesWithAttr = NodeUtility.GetAllChildrenWithSelf(node)
            .Where(c => c is MeshInstance3D)
            .Select(c => c as MeshInstance3D)
            .Where(c => c.Mesh != null)
            .Select(c => (Mesh: c, Match: Regex.Match(c.Name, layerAttr)))
            .Where(m => m.Match.Success);
        meshesWithAttr.ToList().ForEach(m =>
        {
            var values = Regex.Split(m.Match.Captures.First().Value, $"[{valueStart}{valueEnd}]");
            var parsed = uint.TryParse(values.Skip(1).FirstOrDefault(), out uint layer);
            m.Mesh.Layers = parsed ? layer : 1;
        });
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
            ? ResourceLoader.Load<Material>($"{Settings.materialPath}{replacements.First().file}")
            : new Material();
        return replacement;
    }

    static (string name, string file)[] GetReplaceableMaterials() => DirAccess.Open(Settings.materialPath)
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
            ? ResourceLoader.Load<PackedScene>($"{Settings.replaceablePath}{replacements.First().file}").Instantiate() as Node3D
            : new();
        replacement.Name = $"{original} (replaced)";
        return replacement;
    }

    static (string name, string file)[] GetReplaceablePackages() => DirAccess.Open(Settings.replaceablePath)
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