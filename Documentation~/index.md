# VR Builder - Open source toolkit for VR creation
See the attached PDF.

## Process storage

Choose **Project Settings > VR Builder > Process Storage > Process Asset Strategy**:

- **Single File** (default) stores the entire process in one file.
- **Split Chapters** stores each chapter separately and writes a process manifest,
  which can make version-control merges easier.

The project setting applies when saving processes. Existing processes continue to
load using their recorded strategy; changing the setting does not convert every
process automatically. A custom editor configuration that overrides
`ProcessAssetStrategy` takes precedence, and the selector is disabled in that case.

## Chapter templates

Open a process in the Process Editor and expand its chapter sidebar.

- Click the **Export Chapter Template** icon after Rename on a chapter row to save a `.chaptertemplate` file.
  Store it in the project's `Assets` folder or another location for reuse or sharing.
- Click **Import Chapter** beside Add Chapter and Duplicate Chapter, then select a saved file to insert a chapter after the
  selected chapter. The imported chapter becomes selected and supports undo/redo.
- Each import creates fresh entity IDs and remaps references within the copied
  chapter. Importing the same template repeatedly creates independent chapters.

Templates use the configured process serializer and include the chapter's steps,
behaviors, conditions, nested subchapters, and graph metadata. Export serializes a
copy without modifying the chapter being edited. Invalid or unreadable imports
report an error before changing the process.

Templates do not bundle scene objects, Unity assets, localization tables, or custom
behavior implementations. Those dependencies must exist in the destination project.
Scene object references keep their GUIDs; review and rebind references when using a
template in another scene or project. References outside the exported chapter are
not bundled and may require reconnection. Use compatible serializer and package
versions in both projects.

Only import files from trusted sources: process deserialization constructs the
serialized entity types. Template actions are disabled during Play mode.

## Build validation

Every player build validates the selected process in each scene from the actual
build request, including custom build scene lists and build profiles. Scenes without
an active RuntimeConfigurator are skipped. Multiple active configurators, an empty
selection, or an unreadable process block the build.

Validation checks nested chapters and step groups, unset and missing references,
missing required scene components, unreachable steps, invalid chapter entries, and
empty or duplicate entity IDs. Scene references, including groups and inactive
objects, must resolve in their own build scene; another open scene cannot satisfy
those references. Duplicate scene object IDs also block the build.

Empty behavior lists and unset transition targets remain valid. An unset transition
target means the end of the chapter. Errors identify the scene, process, chapter,
step, and field where applicable.

The build saves the open process through the existing process save hook before
validation. It inspects saved scene snapshots without replacing the open editor
scenes. Additional, non-selected processes are not checked.