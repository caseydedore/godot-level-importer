# Godot Level Importer

This plugin prepares imported scenes as a playable level in a post-process step in a GLB object's import.

Levels are built in an external modeling tool and imported to Godot with basic level requirements already configured. This includes object replacements (ie, replace a placeholder with a logic driven version), material assignments, collision mesh setup and lightmap setup.

Requires a Godot 4 .NET release.

## Expectations
* Most level geometry is static and has collisions.
* Geometry is not too complex or dense. For example, target early-mid 2000s era complexity.
* Geometry is split into individual meshes where logical.
* Most geometry is lightmapped.
* Logic driven, interactive, or particularly complex objects are represented by placeholders and replaced by PackedScenes during import.
* Lighting is managed in Godot.
* Logic is managed in Godot.
* Level objects are not modified directly after import. The level object, or scene in Godot terms, is added to another scene as a child. All remaining work is achieved under a parent.

## Setup
* Add plugin to `addons` directory in the Godot project root.
* Enable the Plugin in Godot project settings.
* Review plugin settings hardcoded into `LevelImporterProcessor`.

## Settings
* Adjust settings in `LevelImporterProcessor` as needed and recompile the project.
* Scenes are processed by the importer based on naming convention. Scene names containing `Level` by default will be processed automatically during import.
* Plugin paths must be configured for the Godot project.
  * Automatic replacement of scene assets requires paths to the replacements.
  * Replacement asset directories should be reserved for such assets only.

## Object replacement with PackedScenes.
* A special directory in the project may contain PackedScenes that will replace objects in the imported scene.
* Objects are replaced by name, when the object's name contains the complete PackedScene name, not including file extension.
* For example, with the replaceable path configured to `Assets/Scene/Replacement`, imported scene objects with names `InteractableDoor` and `InteractableDoor.001`are replaced by the PackedScene in the project at `Assets/Scene/Replacement/InteractableDoor.tscn`.

## Material assignment.
Materials in imports may be replaced with Materials created in the Godot project based on matching names. Replacement Material location must match the configured Material replacement directory in the plugin.

## Static collision trimesh generation.
Collision meshes are generated for objects not replaced by PackedScenes.

## Lightmap UVs are generated for the static meshes based on configuration.
Lightmap UVs are not generated for objects replaced by PackedScenes.

## Special Attributes.
Objects in the imported scene with names containing attributes are processed specially. Attributes are additions to the object name and may be located anywhere within the name. To be recognized, attributes are preceded by the attribute indicator `=`.

### Attributes
Collision
* `NoCol` No collision mesh generation.
* `OnlyCol` Removal of render mesh (used for invisible colliders).
* `ConvexCol` Simplified collision mesh generation (convex instead of concave).
* `Col{n}` Collision layer assignment (bits).
* `ColMask{n}` Collision mask assignment (bits).

Rendering
* `NoBake` Set to Dynamic instead of Static and do not generate Lightmap UV.
* `Texel{n}` Multiplier for Texel size. Larger values result in lower resolution.
* `NoShadow` Disable mesh's ability to cast shadows.