using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;

partial class Build : NukeBuild
{
    readonly AbsolutePath ArtifactsDirectory = RootDirectory / "output";

    readonly string[] CompiledAssemblies = { "Serilog.Sinks.GoogleAnalytics.dll" };

    [GitRepository]
    readonly GitRepository GitRepository;

    [Solution(GenerateProjects = true)]
    Solution Solution;

    public static int Main() => Execute<Build>(x => x.Clean);

}
