namespace CodeSearch
{
    public class Globals
    {
        public const string CTags = "bin\\ctags.exe";
        public const string TagsDir = "bin";
        public const string ConfigurationXmlConst = "configuration";
        public const string ConfigurationConstXmlExtension = "xml";
        public static string ConfigurationXml;
        public const string OpenGrokJar = "lib\\opengrok.jar";
        public const string DataRootConst = "data";
        public static string DataRoot;
        public const string JettyRunnerJar = "lib\\jetty-runner-9.3.9.v20160517.jar";
        public const string TfsRootConst = "tfs";
        public static string TfsRoot;
        public const string JavaExe = "bin\\java.exe";
        public const string WebDir = "lib\\source";
        public const string WorkspacePrefix = "codesearch";
        public static uint WorkspaceNumericPrefix = 0;
        public const string WebhostServiceName = "CodeSearch.Webhost.Service";
        public const string WebhostDisplayName = "CodeSearch webhost service";
        public const string UpdaterServiceName = "CodeSearch.Indexer.Service";
        public const string UpdaterDisplayName = "CodeSearch indexer service";
        public const string WebConfigBaseName = "web.xml";
        public const string WebConfigPath = @"\lib\source\WEB-INF";
        public const string Port = "8102";

    }
}