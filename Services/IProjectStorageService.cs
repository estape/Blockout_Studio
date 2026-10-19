using BlockoutStudio.Services.Assets;

namespace BlockoutStudio.Services;

public interface IProjectStorageService
{
    string DefaultProjectsDir { get; }
    string? SaveProjectDialog(string jsonContent, string defaultFileName = "Level_Blockout.ueblock");
    (string? Path, string? Content) LoadProjectDialog();
    string? CreateProjectDialog(string defaultProjectName = "NewGameProject");
    (string ProjectRoot, ProjectManifest Manifest) CreateNewProject(string rootDir, string projectName, string? starterSceneJson = null);
    (string ProjectRoot, ProjectManifest Manifest) OpenProject(string projectPathOrDir);
    void EnsureProjectStructure(string rootDir, ProjectManifest manifest, string? starterSceneJson = null);
}

