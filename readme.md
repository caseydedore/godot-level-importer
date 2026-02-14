# Godot Level Importer

This plugin prepares imported scenes as a playable level in a post-process step in a GLB object's import.

Levels are built in an external modeling tool and imported to Godot with basic level requirements already configured. This includes Object replacements (ie, replace a placeholder object with a logic driven objects), Material assignments, collision mesh setup and lightmap UV setup.

Requires a Godot 4 .NET release.

## Expectations
* Most level geometry is static and has collisions.
* Geometry is not too complex or dense. For example, target early-mid 2000s era complexity.
* Geometry is split into individual meshes where logical.
* Logic and interactive objects are self-contained placeholders (replaced by PackedScenes during import).
* Lighting is managed in Godot.
* Logic is managed in Godot.

## Setup
* Add plugin to `addons` directory in the Godot project root.
* Enable the Plugin in Godot project settings.
* Review plugin settings hardcoded into `LevelImporterProcessor`.

## Settings
* Adjust settings in `LevelImporterProcessor` as needed and recompile the project.
* Scenes are determined to be levels based on naming convention.
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
* `NoCol` No collision mesh generation.
* `OnlyCol` Removal of render mesh (used for invisible colliders).
* `ConvexCol` Simplified collision mesh generation.
* `Col{n}` Collision layer assignment (bits).
* `ColMask{n}` Collision mask assignment (bits).
* `NoBake` Set to Dynamic instead of Static and do not generate Lightmap UV.