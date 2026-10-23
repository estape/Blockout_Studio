using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services;

/// <summary>
/// Service abstraction managing project workspace directory scaffolding, file I/O dialogs, and project manifests.
/// </summary>
public interface IProjectStorageService
{
    /// <summary>Gets the default filesystem directory where new projects are scaffolded.</summary>
    string DefaultProjectsDir { get; }

    /// <summary>
    /// Displays a native OS Save File dialog and writes the level JSON content to the selected path.
    /// </summary>
    /// <param name="jsonContent">Serialized scene JSON text.</param>
    /// <param name="defaultFileName">Default suggested file name.</param>
    /// <returns>The destination file path if saved, or null if cancelled.</returns>
    string? SaveProjectDialog(string jsonContent, string defaultFileName = ProjectFileType.DefaultFileName);

    /// <summary>
    /// Displays a native OS Save File dialog and writes the level JSON content to the selected path using a typed project file type struct.
    /// </summary>
    /// <param name="jsonContent">Serialized scene JSON text.</param>
    /// <param name="defaultFileType">Default project file type struct.</param>
    /// <returns>The destination file path if saved, or null if cancelled.</returns>
    string? SaveProjectDialog(string jsonContent, ProjectFileType defaultFileType) => SaveProjectDialog(jsonContent, defaultFileType.Value);

    /// <summary>
    /// Displays a native OS Open File dialog and reads level JSON content from disk.
    /// </summary>
    /// <returns>A tuple containing the loaded file path and string content.</returns>
    (string? Path, string? Content) LoadProjectDialog();

    /// <summary>
    /// Displays a folder picker dialog to select the parent folder for a new project workspace.
    /// </summary>
    /// <param name="defaultProjectName">Initial project folder name suggestion.</param>
    /// <returns>The full path for the new project folder, or null if cancelled.</returns>
    string? CreateProjectDialog(string defaultProjectName = "NewGameProject");

    /// <summary>
    /// Scaffolds a complete project workspace with standard folders, manifests, and starter assets.
    /// </summary>
    /// <param name="rootDir">Parent directory containing the new project.</param>
    /// <param name="projectName">Unique project name.</param>
    /// <param name="starterSceneJson">Optional initial scene JSON to save as default level.</param>
    /// <returns>Tuple containing the project root path and parsed <see cref="ProjectManifest"/>.</returns>
    (string ProjectRoot, ProjectManifest Manifest) CreateNewProject(string rootDir, string projectName, string? starterSceneJson = null);

    /// <summary>
    /// Opens an existing project folder, reading and validating its ProjectManifest.json.
    /// </summary>
    /// <param name="projectPathOrDir">Path to the project folder or manifest file.</param>
    /// <returns>Tuple of project root path and parsed manifest.</returns>
    (string ProjectRoot, ProjectManifest Manifest) OpenProject(string projectPathOrDir);

    /// <summary>
    /// Ensures all required subdirectories (Assets, Library, Saved, ProjectSettings) and templates exist.
    /// </summary>
    /// <param name="rootDir">Project root directory.</param>
    /// <param name="manifest">Active project manifest.</param>
    /// <param name="starterSceneJson">Optional default scene JSON.</param>
    void EnsureProjectStructure(string rootDir, ProjectManifest manifest, string? starterSceneJson = null);
}
